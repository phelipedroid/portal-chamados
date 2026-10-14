using System.ComponentModel.DataAnnotations;

namespace PortalSanMarco.Services;

public class PortalMonitorOptions
{
    [Required]
    public string HealthUrl { get; set; } = "http://localhost/health";

    [Required]
    public string BackupDirectory { get; set; } =
        @"D:\Backups\PortalSanMarco";

    [Range(1, 168)]
    public int MaxBackupAgeHours { get; set; } = 26;

    [Range(1, 90)]
    public int MinimumFreeDiskPercent { get; set; } = 15;

    [Range(1, 72)]
    public int RepeatAlertHours { get; set; } = 12;

    [Required]
    public string StateFile { get; set; } =
        @"C:\ProgramData\PortalSanMarco\monitor-state.json";

    public List<string> Recipients { get; set; } = [];

    public List<string> Drives { get; set; } = ["C", "D"];
}
