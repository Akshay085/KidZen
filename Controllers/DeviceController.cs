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
[Route("api/devices")]
[Authorize]
public class DeviceController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public DeviceController(ApplicationDbContext db)
    {
        _db = db;
    }

    private int GetCurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out var id) ? id : 0;
    }

    [HttpPost("register")]
    [Authorize(Roles = "Child")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceDto dto)
    {
        var childId = GetCurrentUserId();

        var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceIdentifier == dto.DeviceIdentifier);
        if (device == null)
        {
            device = new Device
            {
                ChildId = childId,
                DeviceIdentifier = dto.DeviceIdentifier,
                DeviceName = dto.DeviceName,
                Platform = dto.Platform,
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            _db.Devices.Add(device);
        }
        else
        {
            device.ChildId = childId;
            device.DeviceName = dto.DeviceName;
            device.IsOnline = true;
            device.LastSeen = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok(new { deviceId = device.DeviceId, message = "Device registered successfully" });
    }

    [HttpPost("{deviceId}/apps/sync")]
    [Authorize(Roles = "Child")]
    public async Task<IActionResult> SyncApps(int deviceId, [FromBody] SyncAppsDto dto)
    {
        var childId = GetCurrentUserId();
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId && d.ChildId == childId);
        if (device == null) return NotFound(new { message = "Device not found or not owned by child" });

        var existingApps = await _db.Applications.Where(a => a.DeviceId == deviceId).ToListAsync();
        var existingDict = existingApps.ToDictionary(a => a.PackageName);

        foreach (var incoming in dto.Apps)
        {
            if (existingDict.TryGetValue(incoming.PackageName, out var existing))
            {
                existing.AppName = incoming.AppName;
                if (!string.IsNullOrEmpty(incoming.AppIconBase64))
                {
                    existing.AppIconBase64 = incoming.AppIconBase64;
                }
                existing.LastDetected = DateTime.UtcNow;
            }
            else
            {
                _db.Applications.Add(new Application
                {
                    DeviceId = deviceId,
                    PackageName = incoming.PackageName,
                    AppName = incoming.AppName,
                    AppIconBase64 = incoming.AppIconBase64,
                    LastDetected = DateTime.UtcNow
                });
            }
        }

        device.LastSeen = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = $"Synchronized {dto.Apps.Count} applications successfully" });
    }

    [HttpGet("{deviceId}/rules")]
    public async Task<IActionResult> GetDeviceRules(int deviceId)
    {
        var device = await _db.Devices.FindAsync(deviceId);
        var now = DateTime.UtcNow;

        var targetDeviceIds = new List<int> { deviceId };
        if (device != null)
        {
            var peerIds = await _db.Devices
                .Where(d => d.ChildId == device.ChildId)
                .Select(d => d.DeviceId)
                .ToListAsync();
            targetDeviceIds = peerIds;
        }

        var activeUnlocks = await _db.TemporaryUnlocks
            .Where(u => targetDeviceIds.Contains(u.DeviceId) && u.EndTime > now)
            .OrderByDescending(u => u.EndTime)
            .ToListAsync();
        var unlockDict = activeUnlocks
            .GroupBy(u => u.PackageName)
            .ToDictionary(g => g.Key, g => g.First().EndTime);

        var rulesList = await _db.AppRules
            .Where(r => targetDeviceIds.Contains(r.DeviceId) && r.IsEnabled)
            .OrderByDescending(r => r.UpdatedAt)
            .ToListAsync();

        var rules = rulesList
            .GroupBy(r => r.PackageName)
            .Select(g =>
            {
                var topRule = g.First();
                return new DeviceRuleSyncDto(
                    topRule.PackageName,
                    topRule.IsLocked,
                    topRule.DailyLimitMinutes,
                    unlockDict.TryGetValue(topRule.PackageName, out var endTime) ? endTime : null
                );
            }).ToList();

        return Ok(rules);
    }

    [HttpPut("{deviceId}/heartbeat")]
    public async Task<IActionResult> Heartbeat(int deviceId)
    {
        var device = await _db.Devices.FindAsync(deviceId);
        if (device != null)
        {
            device.LastSeen = DateTime.UtcNow;
            device.IsOnline = true;
            await _db.SaveChangesAsync();
        }
        return Ok(new { status = "alive" });
    }
}
