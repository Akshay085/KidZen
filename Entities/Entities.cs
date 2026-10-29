using System.ComponentModel.DataAnnotations;

namespace KidZen.API.Entities;

public class User
{
    [Key]
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Parent"; // "Parent" or "Child"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<ParentChild> ParentChildren { get; set; } = new List<ParentChild>();
    public ICollection<Device> Devices { get; set; } = new List<Device>();
}

public class ParentChild
{
    [Key]
    public int Id { get; set; }
    public int ParentId { get; set; }
    public User? Parent { get; set; }

    public int ChildId { get; set; }
    public User? Child { get; set; }

    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
}

public class Device
{
    [Key]
    public int DeviceId { get; set; }
    public int ChildId { get; set; }
    public User? Child { get; set; }

    public string DeviceIdentifier { get; set; } = string.Empty; // UUID
    public string DeviceName { get; set; } = string.Empty;
    public string Platform { get; set; } = "Android";
    public bool IsOnline { get; set; } = false;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Application> Applications { get; set; } = new List<Application>();
    public ICollection<AppRule> AppRules { get; set; } = new List<AppRule>();
    public ICollection<UsageRecord> UsageRecords { get; set; } = new List<UsageRecord>();
    public ICollection<TemporaryUnlock> TemporaryUnlocks { get; set; } = new List<TemporaryUnlock>();
}

public class Application
{
    [Key]
    public int AppId { get; set; }
    public int DeviceId { get; set; }
    public Device? Device { get; set; }

    public string PackageName { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public string? AppIconBase64 { get; set; }
    public DateTime LastDetected { get; set; } = DateTime.UtcNow;
}

public class AppRule
{
    [Key]
    public int RuleId { get; set; }
    public int DeviceId { get; set; }
    public Device? Device { get; set; }

    public string PackageName { get; set; } = string.Empty;
    public bool IsLocked { get; set; } = false;
    public int DailyLimitMinutes { get; set; } = 0; // 0 = no limit
    public bool IsEnabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class UsageRecord
{
    [Key]
    public int UsageId { get; set; }
    public int DeviceId { get; set; }
    public Device? Device { get; set; }

    public string PackageName { get; set; } = string.Empty;
    public DateTime RecordDate { get; set; } = DateTime.UtcNow.Date;
    public int DurationMinutes { get; set; } = 0;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class TemporaryUnlock
{
    [Key]
    public int UnlockId { get; set; }
    public int DeviceId { get; set; }
    public Device? Device { get; set; }

    public string PackageName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime EndTime { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; } = "Parent Granted";
}
