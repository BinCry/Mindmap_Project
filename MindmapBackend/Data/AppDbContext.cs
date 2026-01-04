using Microsoft.EntityFrameworkCore;
using MindmapAPI.Models;

namespace MindmapAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Mindmap> Mindmaps { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình bảng Users
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();

                // Ép kiểu DateTime để SQLite không bị lỗi format
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // Cấu hình bảng Mindmap (nếu cần)
            modelBuilder.Entity<Mindmap>(entity =>
            {
                entity.HasKey(e => e.Id);
            });
        }
    }
}