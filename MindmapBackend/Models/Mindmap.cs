using System.ComponentModel.DataAnnotations;

namespace MindmapAPI.Models;

public class Mindmap
{
    [Key]
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ContentJson { get; set; } = "{}"; // Chứa toàn bộ Node/Connection dạng JSON
    public string ShareCode { get; set; } = string.Empty; // Mã ngắn (VD: A1B2) để người khác nhập
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}