namespace KidZen.API.DTOs;

// Auth DTOs
public record RegisterParentDto(string FullName, string Email, string Password);
public record LoginDto(string Email, string Password);
public record AuthResponseDto(string Token, int UserId, string FullName, string Email, string Role);

// Child DTOs
public record CreateChildDto(string FullName, string Email, string Password);
public record ChildSummaryDto(int ChildId, string FullName, string Email, List<DeviceSummaryDto> Devices);

// Device DTOs
public record RegisterDeviceDto(string DeviceIdentifier, string DeviceName, string Platform = "Android");
public record DeviceSummaryDto(int DeviceId, string DeviceIdentifier, string DeviceName, bool IsOnline, DateTime LastSeen);

// App DTOs
public record InstalledAppItemDto(string PackageName, string AppName, string? AppIconBase64);
public record SyncAppsDto(int DeviceId, List<InstalledAppItemDto> Apps);
public record ChildAppControlDto(
    int AppId,
    string PackageName,
    string AppName,
    string? AppIconBase64,
    bool IsLocked,
    int DailyLimitMinutes,
    int TodayUsedMinutes,
    bool IsLimitExceeded
);

// Rule DTOs
public record ToggleLockDto(int DeviceId, string PackageName, bool IsLocked);
public record SetLimitDto(int DeviceId, string PackageName, int DailyLimitMinutes);
public record TemporaryUnlockDto(int DeviceId, string PackageName, int DurationMinutes, string Reason = "Parent Approved");

// Usage DTOs
public record ReportUsageItemDto(string PackageName, int DurationMinutes);
public record SyncUsageDto(int DeviceId, List<ReportUsageItemDto> Usages);
public record UsageStatDto(string PackageName, string AppName, int DurationMinutes, DateTime Date);

// Realtime SignalR Command DTO
public record DeviceRuleSyncDto(string PackageName, bool IsLocked, int DailyLimitMinutes, DateTime? TemporaryUnlockUntil = null);
