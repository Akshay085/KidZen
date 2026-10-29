using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KidZen.API.Data;
using KidZen.API.DTOs;
using KidZen.API.Entities;
using System.Security.Claims;

namespace KidZen.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsageController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public UsageController(ApplicationDbContext db)
    {
        _db = db;
    }

    private int GetCurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out var id) ? id : 0;
    }

    [HttpPost("sync")]
    [Authorize(Roles = "Child")]
    public async Task<IActionResult> SyncUsage([FromBody] SyncUsageDto dto)
    {
        var childId = GetCurrentUserId();
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceId == dto.DeviceId && d.ChildId == childId);
        if (device == null) return NotFound(new { message = "Device not found" });

        var today = DateTime.UtcNow.Date;

        var existingRecords = await _db.UsageRecords
            .Where(u => u.DeviceId == dto.DeviceId && u.RecordDate == today)
            .ToListAsync();
        var recordDict = existingRecords.ToDictionary(u => u.PackageName);

        foreach (var item in dto.Usages)
        {
            if (recordDict.TryGetValue(item.PackageName, out var existing))
            {
                existing.DurationMinutes = item.DurationMinutes;
                existing.LastUpdated = DateTime.UtcNow;
            }
            else
            {
                _db.UsageRecords.Add(new UsageRecord
                {
                    DeviceId = dto.DeviceId,
                    PackageName = item.PackageName,
                    RecordDate = today,
                    DurationMinutes = item.DurationMinutes,
                    LastUpdated = DateTime.UtcNow
                });
            }
        }

        device.LastSeen = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Usage recorded successfully" });
    }

    [HttpGet("device/{deviceId}/today")]
    public async Task<IActionResult> GetTodayUsage(int deviceId)
    {
        var today = DateTime.UtcNow.Date;

        var usages = await (from u in _db.UsageRecords
                            join a in _db.Applications on new { u.DeviceId, u.PackageName } equals new { a.DeviceId, a.PackageName } into appGroup
                            from app in appGroup.DefaultIfEmpty()
                            where u.DeviceId == deviceId && u.RecordDate == today
                            select new UsageStatDto(
                                u.PackageName,
                                app != null ? app.AppName : u.PackageName,
                                u.DurationMinutes,
                                u.RecordDate
                            )).ToListAsync();

        var totalMinutes = usages.Sum(u => u.DurationMinutes);

        return Ok(new
        {
            deviceId,
            date = today,
            totalMinutes,
            totalHours = Math.Round((double)totalMinutes / 60, 1),
            apps = usages.OrderByDescending(u => u.DurationMinutes)
        });
    }
}
