using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace cron_yaml;

public static class Extensions
{
    public static string ReplaceInvalidFilenameChars(this string filename)
    {
        string regexString = string.Format("[{0}]", Regex.Escape(new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars())));
        Regex regex = new Regex(regexString);
        return regex.Replace(filename, "_");
    }

    public static string ReplaceInvalidPathChars(this string directory)
    {
        string regexString = string.Format("[{0}]", Regex.Escape(new string(Path.GetInvalidPathChars())));
        Regex regex = new Regex(regexString);
        return regex.Replace(directory, "_");
    }

    //makes it easier to create subfolders in current dir
    public static string GetPath(this string fileName, params string[] args)
    {
        var dir = Directory.GetCurrentDirectory();
        args.ToList().ForEach(arg =>
        {
            dir = Path.Combine(dir, arg.ReplaceInvalidPathChars());
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        });
        return Path.Combine(dir, fileName.ReplaceInvalidFilenameChars());
    }
    public static void UpdateNextRuntime(this List<GroupClass> newGroups, List<GroupClass> _groups)
    {
        var newJobsByKey = newGroups.SelectMany(g => g.Job!.Select(j => new { GroupName = g.Name, Job = j }))
                                        .ToDictionary(j => $"{j.GroupName}|{j.Job.Name}");
        foreach (var group in _groups)
        {
            foreach (var job in group.Job!.Where(j => j.PreserveNextRuntime))
                if (newJobsByKey.TryGetValue($"{group.Name}|{job.Name}", out var newJob))
                    newJob.Job.NextRuntime = job.NextRuntime;
        }
    }

    //Log string data to a path in a reusable way
    public static async Task LogTo(this string data, string logPath, int maxLogLines)
    {
        int bufferSize = 4096, lineCount = 0;
        var lines = new List<string>();
        using (var fileStream = new FileStream(logPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, bufferSize, true))
        {
            using (var streamReader = new StreamReader(fileStream))
            using (var streamWriter = new StreamWriter(fileStream))
            {
                //var maxLogLines = task.MaxLogLines ?? 1000;
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
    }
}
