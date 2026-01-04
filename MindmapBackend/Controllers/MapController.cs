using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MindmapAPI.Data;
using MindmapAPI.Models;

namespace MindmapAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MapController : ControllerBase
{
    private readonly AppDbContext _context;

    public MapController(AppDbContext context) => _context = context;

    // API: Lấy map về máy khi nhập mã Share
    [HttpGet("shared/{code}")]
    public async Task<IActionResult> GetSharedMap(string code)
    {
        var map = await _context.Mindmaps
            .FirstOrDefaultAsync(m => m.Id.ToString() == code || m.ShareCode == code);

        if (map == null) return NotFound("Không tìm thấy Map");

        return Ok(map);
    }

    // API: Lưu map lên Cloud (để tạo mã Share)
    [HttpPost]
    public async Task<IActionResult> SaveMap([FromBody] Mindmap map)
    {
        var existing = await _context.Mindmaps.FindAsync(map.Id);
        if (existing != null)
        {
            existing.ContentJson = map.ContentJson;
            existing.Title = map.Title;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Nếu chưa có mã share thì tạo mã 6 ký tự ngẫu nhiên
            if (string.IsNullOrEmpty(map.ShareCode))
                map.ShareCode = Guid.NewGuid().ToString().Substring(0, 6).ToUpper();

            _context.Mindmaps.Add(map);
        }
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Saved", ShareCode = map.ShareCode });
    }
}