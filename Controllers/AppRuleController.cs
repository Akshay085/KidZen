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

        var rule = await _db.AppRules.FirstOrDefaultAsync(r => r.DeviceId == dto.DeviceId && r.PackageName == dto.PackageName);
        if (rule == null)
        {
            rule = new AppRule
            {
                DeviceId = dto.DeviceId,
                PackageName = dto.PackageName,
                IsLocked = dto.IsLocked,
                DailyLimitMinutes = 0,
                IsEnabled = true,
                UpdatedAt = DateTime.UtcNow
            };
            _db.AppRules.Add(rule);
        }
        else
        {
            rule.IsLocked = dto.IsLocked;
            rule.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        // Broadcast directly to Child Device via SignalR WebSocket
        await _hubContext.Clients.Group($"device_{dto.DeviceId}").SendAsync("OnRuleUpdated", new
        {
            packageName = dto.PackageName,
            isLocked = dto.IsLocked,
            dailyLimitMinutes = rule.DailyLimitMinutes,
            action = dto.IsLocked ? "LOCKED" : "UNLOCKED"
        });

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

        var rule = await _db.AppRules.FirstOrDefaultAsync(r => r.DeviceId == dto.DeviceId && r.PackageName == dto.PackageName);
        if (rule == null)
        {
            rule = new AppRule
            {
                DeviceId = dto.DeviceId,
                PackageName = dto.PackageName,
                IsLocked = false,
                DailyLimitMinutes = dto.DailyLimitMinutes,
                IsEnabled = true,
                UpdatedAt = DateTime.UtcNow
            };
            _db.AppRules.Add(rule);
        }
        else
        {
            rule.DailyLimitMinutes = dto.DailyLimitMinutes;
            rule.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        // Broadcast to Child Device via SignalR
        await _hubContext.Clients.Group($"device_{dto.DeviceId}").SendAsync("OnRuleUpdated", new
        {
            packageName = dto.PackageName,
            isLocked = rule.IsLocked,
            dailyLimitMinutes = dto.DailyLimitMinutes,
            action = "LIMIT_UPDATED"
        });

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
        var unlock = new TemporaryUnlock
        {
            DeviceId = dto.DeviceId,
            PackageName = dto.PackageName,
            StartTime = now,
            EndTime = now.AddMinutes(dto.DurationMinutes),
            Reason = dto.Reason
        };

        _db.TemporaryUnlocks.Add(unlock);
        await _db.SaveChangesAsync();

        // Broadcast temporary unlock permission to child device
        await _hubContext.Clients.Group($"device_{dto.DeviceId}").SendAsync("OnTemporaryUnlockGranted", new
        {
            packageName = dto.PackageName,
            durationMinutes = dto.DurationMinutes,
            expiresAt = unlock.EndTime
        });

        return Ok(new { message = $"Temporary unlock granted for {dto.DurationMinutes} minutes." });
    }
}
