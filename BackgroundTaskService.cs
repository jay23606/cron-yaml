using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using YamlDotNet.Serialization;
namespace cron_yaml;

public class BackgroundTaskService : BackgroundService
{
    //readonly ILogger<BackgroundTaskService> _logger;
    List<GroupClass> _groups;
    FileSystemWatcher? _watcher;

    public BackgroundTaskService(ILogger<BackgroundTaskService> logger)
    {
        //_logger = logger;
        _groups = ParseYamlData();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Execute each group in parallel
            await Task.WhenAll(_groups.Where(g => g.Active ?? true).Select(g => ExecuteGroupAsync(g, stoppingToken)));
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    async Task ExecuteGroupAsync(GroupClass group, CancellationToken stoppingToken)
    {
        foreach (var job in group.Job!.Where(j => (j.Active ?? true) && j.IsDue)) 
        {
            await ExecuteJobAsync(group, job, group.Name!, stoppingToken);
            job.ResetNextRuntime();
        }
    }

    async Task ExecuteJobAsync(GroupClass group, JobClass job, string groupName, CancellationToken stoppingToken)
    {
        foreach (var task in job.Task!.Where(t => t.Active ?? true)) await ExecuteTaskAsync(group, job, task);
    }

    async Task ExecuteTaskAsync(GroupClass group, JobClass job, TaskClass task)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = task.FileName,
                Arguments = task.Arguments,
                WorkingDirectory = task.WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };
        
        process.OutputDataReceived += async (sender, args) =>
        {
            if (args.Data == null) return;
            var logPath = $"{task.Name}.log".GetPath(group.Name!, job.Name!); //Creates directories
            DateTime time = DateTime.UtcNow;
            if (job.TimeZone != null) time = TimeZoneInfo.ConvertTimeFromUtc(time, TimeZoneInfo.FindSystemTimeZoneById(job.TimeZone!));
            var data = $"[{time.ToString("yy-MM-dd HH:mm:ss")}] {args.Data}";
            await data.LogTo(logPath, task.MaxLogLines ?? 1000); //Custom logging
        };
        process.Start();
        process.BeginOutputReadLine();
        await process.WaitForExitAsync();
    }

    List<GroupClass> ParseYamlData()
    {
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), Environment.GetCommandLineArgs()[1]);

        if (_watcher != null) _watcher.Dispose();
        _watcher = new FileSystemWatcher();
        _watcher.Path = Path.GetDirectoryName(filePath)!;
        _watcher.Filter = Path.GetFileName(filePath);
        _watcher.NotifyFilter = NotifyFilters.LastWrite;
        _watcher.Changed += OnFileChanged;
        _watcher.EnableRaisingEvents = true;

        var yaml = File.ReadAllText(filePath);
        var deserializer = new DeserializerBuilder().Build();
        var data = deserializer.Deserialize<List<GroupClass>>(yaml);
        return data;
    }

    private readonly object _lockObject = new object();
    void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            Console.WriteLine($"Reloading YAML file {e.Name} due to a change");
            // Use a lock to ensure thread safety
            lock (_lockObject)
            {
                var newGroups = ParseYamlData();
                newGroups.UpdateNextRuntime(_groups); //update NextRuntime if PreserveNextRuntime is set (true by default)
                _groups = newGroups;
            }
        }
        catch (Exception ex) { Console.WriteLine($"Error reloading YAML file {e.Name}: {ex.Message}"); }
    }
}