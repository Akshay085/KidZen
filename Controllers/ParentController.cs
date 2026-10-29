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
[Authorize(Roles = "Parent")]
public class ParentController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ParentController(ApplicationDbContext db)
    {
        _db = db;
    }

    private int GetCurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out var id) ? id : 0;
    }

    [HttpPost("children")]
    public async Task<IActionResult> CreateChild([FromBody] CreateChildDto dto)
    {
        var parentId = GetCurrentUserId();

        if (await _db.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower()))
        {
            return BadRequest(new { message = "Email already registered for another account." });
        }

        var child = new User
        {
            FullName = dto.FullName,
            Email = dto.Email.ToLower().Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = "Child",
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(child);
        await _db.SaveChangesAsync();

        var link = new ParentChild
        {
            ParentId = parentId,
            ChildId = child.UserId,
            LinkedAt = DateTime.UtcNow
        };
        _db.ParentChildren.Add(link);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Child account created successfully",
            childId = child.UserId,
            childEmail = child.Email,
            childName = child.FullName
        });
    }

    [HttpGet("children")]
    public async Task<IActionResult> GetChildren()
    {
        var parentId = GetCurrentUserId();

        var children = await _db.ParentChildren
            .Where(pc => pc.ParentId == parentId)
            .Include(pc => pc.Child)
            .Select(pc => new
            {
                ChildId = pc.Child!.UserId,
                FullName = pc.Child.FullName,
                Email = pc.Child.Email,
                Devices = _db.Devices
                    .Where(d => d.ChildId == pc.Child.UserId)
                    .Select(d => new DeviceSummaryDto(
                        d.DeviceId,
                        d.DeviceIdentifier,
                        d.DeviceName,
                        d.IsOnline,
                        d.LastSeen
                    )).ToList()
            })
            .ToListAsync();

        return Ok(children);
    }

    [HttpGet("devices/{deviceId}/apps")]
    public async Task<IActionResult> GetChildApps(int deviceId)
    {
        var parentId = GetCurrentUserId();

        // Verify parent owns this child's device
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
        if (device == null) return NotFound(new { message = "Device not found" });

        var isLinked = await _db.ParentChildren.AnyAsync(pc => pc.ParentId == parentId && pc.ChildId == device.ChildId);
        if (!isLinked) return Forbid();

        var today = DateTime.UtcNow.Date;

        var apps = await _db.Applications
            .Where(a => a.DeviceId == deviceId)
            .OrderBy(a => a.AppName)
            .ToListAsync();

        var rules = await _db.AppRules
            .Where(r => r.DeviceId == deviceId)
            .ToDictionaryAsync(r => r.PackageName);

        var usages = await _db.UsageRecords
            .Where(u => u.DeviceId == deviceId && u.RecordDate == today)
            .ToDictionaryAsync(u => u.PackageName);

        var result = apps.Select(app =>
        {
            rules.TryGetValue(app.PackageName, out var rule);
            usages.TryGetValue(app.PackageName, out var usage);

            var isLocked = rule?.IsLocked ?? false;
            var limit = rule?.DailyLimitMinutes ?? 0;
            var used = usage?.DurationMinutes ?? 0;
            var isExceeded = limit > 0 && used >= limit;

            return new ChildAppControlDto(
                app.AppId,
                app.PackageName,
                app.AppName,
                app.AppIconBase64,
                isLocked,
                limit,
                used,
                isExceeded
            );
        }).ToList();

        return Ok(result);
    }
}
