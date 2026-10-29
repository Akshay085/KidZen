using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KidZen.API.Data;
using KidZen.API.DTOs;
using KidZen.API.Entities;
using KidZen.API.Services;
using System.Security.Claims;

namespace KidZen.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly TokenService _tokenService;

    public AuthController(ApplicationDbContext db, TokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    [HttpPost("register-parent")]
    public async Task<IActionResult> RegisterParent([FromBody] RegisterParentDto dto)
    {
        if (await _db.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower()))
        {
            return BadRequest(new { message = "Email is already registered." });
        }

        var parent = new User
        {
            FullName = dto.FullName,
            Email = dto.Email.ToLower().Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = "Parent",
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(parent);
        await _db.SaveChangesAsync();

        var token = _tokenService.CreateToken(parent);
        return Ok(new AuthResponseDto(token, parent.UserId, parent.FullName, parent.Email, parent.Role));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.ToLower().Trim());
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var token = _tokenService.CreateToken(user);
        return Ok(new AuthResponseDto(token, user.UserId, user.FullName, user.Email, user.Role));
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        return Ok(new { user.UserId, user.FullName, user.Email, user.Role });
    }
}
