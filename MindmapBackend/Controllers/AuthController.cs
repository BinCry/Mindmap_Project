using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MindmapAPI.Data;
using MindmapAPI.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MindmapAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;

    private static readonly string[] UserColors =
    {
        "#4299E1", "#48BB78", "#F6E05E", "#F687B3", "#9F7AEA", "#ED64A1", "#F6AD55"
    };

    public AuthController(AppDbContext context) => _context = context;

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user == null || user.PasswordHash != req.Password)
        {
            return Unauthorized(new { message = "Sai email hoặc mật khẩu" });
        }

        user.LastLoginAt = DateTime.UtcNow;
        if (string.IsNullOrEmpty(user.UserColor))
        {
            user.UserColor = UserColors[new Random().Next(UserColors.Length)];
        }

        await _context.SaveChangesAsync();
        return Ok(user);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (await _context.Users.AnyAsync(u => u.Email == req.Email))
        {
            return BadRequest(new { message = "Email đã tồn tại" });
        }

        var random = new Random();
        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Email = req.Email,
            PasswordHash = req.Password,
            DisplayName = req.DisplayName,
            UserColor = UserColors[random.Next(UserColors.Length)],
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();
        return Ok(newUser);
    }

    // 🔥 API MỚI: Đặt lại mật khẩu
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user == null) return NotFound(new { message = "Email không tồn tại" });

        // Logic kiểm tra OTP (Tạm thời bỏ qua hoặc bạn tự thêm logic so khớp OTP ở đây)
        // if (user.Otp != req.Otp) return BadRequest(...)

        // Cập nhật mật khẩu mới
        user.PasswordHash = req.NewPassword;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Đổi mật khẩu thành công" });
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user == null) return NotFound(new { message = "Email không tồn tại" });
        if (!string.Equals(user.PasswordHash, req.CurrentPassword, StringComparison.Ordinal))
            return Unauthorized(new { message = "Mật khẩu hiện tại không đúng" });

        user.PasswordHash = req.NewPassword;
        await _context.SaveChangesAsync();
        return Ok(new { message = "Đổi mật khẩu thành công" });
    }
}

// Các Model DTO
public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Password, string? DisplayName);
public record ResetPasswordRequest(string Email, string Otp, string NewPassword); // Class mới
public record ChangePasswordRequest(string Email, string CurrentPassword, string NewPassword);
