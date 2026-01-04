using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindmapAPI.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool IsPro { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    // --- CÁC TRƯỜNG HỖ TRỢ LÀM CHUNG (COLLABORATION) ---

    // Màu sắc đại diện cho User này trên bản đồ (Ví dụ: #FF5733)
    public string? UserColor { get; set; }

    // Thuộc tính tính toán để lấy chữ cái đầu (Ví dụ: Hue Tran -> HT)
    // Dùng [NotMapped] để SQLite không tạo cột này vì nó chỉ dùng để hiển thị lẹ
    [NotMapped]
    public string Initials => string.IsNullOrEmpty(DisplayName)
        ? Email.Substring(0, 1).ToUpper()
        : string.Concat(DisplayName.Split(' ').Select(s => s[0])).ToUpper();
}