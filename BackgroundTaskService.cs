using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using YamlDotNet.Serialization;
namespace cron_yaml;

public class TaskClass
{
    public string? Name { get; set; }
    public string? FileName { get; set; }
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public bool? Active { get; set; }
    public int? MaxLogLines { get; set; }
}

public class GroupClass
{
    public string? Name { get; set; }
    public bool? Active { get; set; }
    public List<JobClass>? Job { get; set; }
}

public class JobClass
{
    public string? Name { get; set; }
    public bool? Active { get; set; }
    public int? Minutely { get; set; }
    public int? Hourly { get; set; }
    public int? Daily { get; set; }
    public List<TaskClass>? Task { get; set; }
    public string? TimeZone { get; set; }
    public TimeSpan NextRunTime { get; set; } = TimeSpan.Zero;
    public bool IsDue
    {
        get
        {
            var nextRunDateTime = DateTime.Today.Add(NextRunTime);
            return nextRunDateTime <= DateTime.Now;
        }
    }
    public void ResetNextRunTime()
    {
        var now = DateTime.Now;
        int interval = 60 * 24; //1 day by default
        if (Minutely != null) interval = (int)Minutely;
        else if (Hourly != null) interval = (int)Hourly * 60;
        else if (Daily != null) interval = (int)Daily * 60 * 24;
        var nextRunTime = now.AddMinutes(interval);
        var minutesSinceMidnight = (int)(nextRunTime - nextRunTime.Date).TotalMinutes;
        var minuteOffset = ((minutesSinceMidnight % interval) + interval) % interval;
        nextRunTime = nextRunTime.AddMinutes(-minuteOffset);
        NextRunTime = nextRunTime.TimeOfDay;
    }
}



public class BackgroundTaskService : BackgroundService
{
    //readonly ILogger<BackgroundTaskService> _logger;
    readonly List<GroupClass> _groups;

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

            // Wait for the next cycle
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    async Task ExecuteGroupAsync(GroupClass group, CancellationToken stoppingToken)
    {
        foreach (var job in group.Job!.Where(j => j.Active ?? true && j.IsDue))
        {
            await ExecuteJobAsync(group, job, group.Name!, stoppingToken);
            job.ResetNextRunTime();
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

        var logPath = Path.Combine(Directory.GetCurrentDirectory(), $"{group.Name}-{job.Name}-{task.Name}.log");
        process.OutputDataReceived += async (sender, args) =>
        {
            //Custom logging
            if (args.Data == null) return;
            DateTime time = DateTime.UtcNow;
            if (job.TimeZone != null)
            {
                TimeZoneInfo mountainZone = TimeZoneInfo.FindSystemTimeZoneById(job.TimeZone!);
                time = TimeZoneInfo.ConvertTimeFromUtc(time, mountainZone);
            }
            var data = $"[{time.ToString("yy-MM-dd HH:mm:ss")}] {args.Data}";
            int bufferSize = 4096, lineCount = 0;
            var lines = new List<string>();
            using (var fileStream = new FileStream(logPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, bufferSize, true))
            {
                using (var streamReader = new StreamReader(fileStream))
                using (var streamWriter = new StreamWriter(fileStream))
                {
                    var maxLogLines = task.MaxLogLines ?? 1000;
                    // Read the existing lines up to the maximum log lines
                    while (!streamReader.EndOfStream && lineCount < maxLogLines)
                    {
                        var line = await streamReader.ReadLineAsync();
                        lines.Add(line!);
                        lineCount++;
                    }
                    if (lines.Count >= maxLogLines)
                    {
                        var removeCount = lines.Count - maxLogLines + 1;
                        lines.RemoveRange(0, removeCount);
                        // Seek to the beginning of the file and truncate it
                        fileStream.Seek(0, SeekOrigin.Begin);
                        fileStream.SetLength(0);
                        // Write the trimmed lines back to the beginning of the file
                        foreach (var line in lines) await streamWriter.WriteLineAsync(line);
                    }
                    // Write the new log data to the end of the file
                    await streamWriter.WriteLineAsync(data);
                    Console.WriteLine(data);
                    await streamWriter.FlushAsync();
                }
            }
        };
        process.Start();
        process.BeginOutputReadLine();
        await process.WaitForExitAsync();
    }

    static int GetFileLineCount(string path)
    {
        int lineCount = 0;
        using StreamReader reader = new StreamReader(path);
        while (reader.ReadLine() != null) lineCount++;
        return lineCount;
    }

    List<GroupClass> ParseYamlData()
    {
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), Environment.GetCommandLineArgs()[1]);
        var yaml = File.ReadAllText(filePath);
        var deserializer = new DeserializerBuilder().Build();
        var data = deserializer.Deserialize<List<GroupClass>>(yaml);
        return data;
    }
}