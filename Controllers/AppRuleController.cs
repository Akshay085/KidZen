using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using KidZen.API.Data;
using KidZen.API.DTOs;
using KidZen.API.Entities;
using KidZen.API.Hubs;
using System.Security.Claims;

namespace KidZen.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/rules")]
[Authorize(Roles = "Parent")]
public class AppRuleController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<DeviceHub> _hubContext;

    public AppRuleController(ApplicationDbContext db, IHubContext<DeviceHub> hubContext)
    {
        _db = db;
        _hubContext = hubContext;
    }

    private int GetCurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out var id) ? id : 0;
    }

    [HttpPost("lock-toggle")]
    public async Task<IActionResult> ToggleLock([FromBody] ToggleLockDto dto)
    {
        var parentId = GetCurrentUserId();

        // Verify parent owns this child device
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceId == dto.DeviceId);
        if (device == null) return NotFound(new { message = "Device not found" });

        var isLinked = await _db.ParentChildren.AnyAsync(pc => pc.ParentId == parentId && pc.ChildId == device.ChildId);
        if (!isLinked) return Forbid();

        var childDevices = await _db.Devices.Where(d => d.ChildId == device.ChildId).ToListAsync();
        foreach (var dev in childDevices)
        {
            var r = await _db.AppRules.FirstOrDefaultAsync(x => x.DeviceId == dev.DeviceId && x.PackageName == dto.PackageName);
            if (r == null)
            {
                r = new AppRule
                {
                    DeviceId = dev.DeviceId,
                    PackageName = dto.PackageName,
                    IsLocked = dto.IsLocked,
                    DailyLimitMinutes = 0,
                    IsEnabled = true,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.AppRules.Add(r);
            }
            else
            {
                r.IsLocked = dto.IsLocked;
                r.UpdatedAt = DateTime.UtcNow;
            }

            await _hubContext.Clients.Group($"device_{dev.DeviceId}").SendAsync("OnRuleUpdated", new
            {
                packageName = dto.PackageName,
                isLocked = dto.IsLocked,
                dailyLimitMinutes = r.DailyLimitMinutes,
                action = dto.IsLocked ? "LOCKED" : "UNLOCKED"
            });
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = $"App {dto.PackageName} is now {(dto.IsLocked ? "Locked" : "Unlocked")}" });
    }

    [HttpPost("set-limit")]
    public async Task<IActionResult> SetDailyLimit([FromBody] SetLimitDto dto)
    {
        var parentId = GetCurrentUserId();

        var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceId == dto.DeviceId);
        if (device == null) return NotFound(new { message = "Device not found" });

        var isLinked = await _db.ParentChildren.AnyAsync(pc => pc.ParentId == parentId && pc.ChildId == device.ChildId);
        if (!isLinked) return Forbid();

        var childDevices = await _db.Devices.Where(d => d.ChildId == device.ChildId).ToListAsync();
        foreach (var dev in childDevices)
        {
            var r = await _db.AppRules.FirstOrDefaultAsync(x => x.DeviceId == dev.DeviceId && x.PackageName == dto.PackageName);
            if (r == null)
            {
                r = new AppRule
                {
                    DeviceId = dev.DeviceId,
                    PackageName = dto.PackageName,
                    IsLocked = false,
                    DailyLimitMinutes = dto.DailyLimitMinutes,
                    IsEnabled = true,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.AppRules.Add(r);
            }
            else
            {
                r.DailyLimitMinutes = dto.DailyLimitMinutes;
                r.UpdatedAt = DateTime.UtcNow;
            }

            await _hubContext.Clients.Group($"device_{dev.DeviceId}").SendAsync("OnRuleUpdated", new
            {
                packageName = dto.PackageName,
                isLocked = r.IsLocked,
                dailyLimitMinutes = dto.DailyLimitMinutes,
                action = "LIMIT_UPDATED"
            });
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = $"Daily limit of {dto.DailyLimitMinutes} minutes set for {dto.PackageName}" });
    }

    [HttpPost("temporary-unlock")]
    public async Task<IActionResult> TemporaryUnlock([FromBody] TemporaryUnlockDto dto)
    {
        var parentId = GetCurrentUserId();

        var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceId == dto.DeviceId);
        if (device == null) return NotFound(new { message = "Device not found" });

        var isLinked = await _db.ParentChildren.AnyAsync(pc => pc.ParentId == parentId && pc.ChildId == device.ChildId);
        if (!isLinked) return Forbid();

        var now = DateTime.UtcNow;
        var unlockEnd = now.AddMinutes(dto.DurationMinutes);

        var childDevices = await _db.Devices.Where(d => d.ChildId == device.ChildId).ToListAsync();
        foreach (var dev in childDevices)
        {
            var unlock = new TemporaryUnlock
            {
                DeviceId = dev.DeviceId,
                PackageName = dto.PackageName,
                StartTime = now,
                EndTime = unlockEnd,
                Reason = dto.Reason
            };
            _db.TemporaryUnlocks.Add(unlock);

            await _hubContext.Clients.Group($"device_{dev.DeviceId}").SendAsync("OnTemporaryUnlockGranted", new
            {
                packageName = dto.PackageName,
                durationMinutes = dto.DurationMinutes,
                expiresAt = unlockEnd
            });
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = $"Temporary unlock granted for {dto.DurationMinutes} minutes." });
    }
}
