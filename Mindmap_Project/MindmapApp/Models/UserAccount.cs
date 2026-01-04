using System;
using System.Linq;

namespace MindmapApp.Models;

public class UserAccount
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool IsPro { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // 🔥 MỚI: Màu sắc đại diện (Đồng bộ từ Server về)
    public string? UserColor { get; set; }

    // Logic lấy chữ cái đầu làm Avatar (Chỉ hiển thị, không lưu DB)
    public string Initials => string.IsNullOrEmpty(DisplayName)
        ? (string.IsNullOrEmpty(Email) ? "?" : Email.Substring(0, 1).ToUpper())
        : string.Concat(DisplayName.Split(' ').Select(s => s[0])).ToUpper();
}