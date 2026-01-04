using MindmapAPI.Data;
using MindmapAPI.Hubs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình SQLite cho Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=mindmap_server.db"));

// 2. Đăng ký dịch vụ
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 3. Cấu hình CORS (Cho phép máy 2 gọi vào máy 1)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// 4. Khởi tạo Database (Reset sạch sẽ mỗi lần chạy)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // 🔥 Dòng này xóa sạch DB cũ nếu có (Fix lỗi schema cũ)
    db.Database.EnsureDeleted();

    // Tạo lại DB mới tinh có cột UserColor
    db.Database.EnsureCreated();
}

// 5. Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();

// 6. Định tuyến
app.MapControllers();
app.MapHub<MindmapHub>("/mindmaphub");

// 🚀 Log thông tin
Console.WriteLine("=======================================");
Console.WriteLine("BACKEND STATUS: ONLINE");
Console.WriteLine("IP ADDRESS: http://10.247.157.244:5076"); // Nhớ update nếu IP thay đổi
Console.WriteLine("SWAGGER: http://10.247.157.244:5076/swagger");
Console.WriteLine("=======================================");

app.Run();