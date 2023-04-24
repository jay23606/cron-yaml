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
    public DateTime NextRuntime { get; set; } = DateTime.Now;
    public bool PreserveNextRuntime { get; set; } = true;
    public bool IsDue =>
        NextRuntime <= DateTime.Now;

    public void ResetNextRuntime()
    {
        if (Minutely != null)
            NextRuntime = DateTime.Now.AddMinutes((double)Minutely);
        else if (Hourly != null)
            NextRuntime = DateTime.Now.AddHours((double)Hourly);
        else if (Daily != null)
            NextRuntime = DateTime.Now.AddDays((double)Daily);
    }
}