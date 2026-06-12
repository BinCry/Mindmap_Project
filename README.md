# Mindmap Backend

[![.NET 8](https://img.shields.io/badge/.NET_8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![SignalR](https://img.shields.io/badge/SignalR-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction)
[![Entity Framework Core](https://img.shields.io/badge/EF_Core-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://learn.microsoft.com/en-us/ef/core/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-316192?style=for-the-badge&logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Visual Studio](https://img.shields.io/badge/Visual_Studio-5C2D91?style=for-the-badge&logo=visual-studio&logoColor=white)](https://visualstudio.microsoft.com/)

Hệ thống Backend Web API xử lý và đồng bộ hóa dữ liệu bản đồ tư duy (Mindmap) theo thời gian thực (Real-time).

---

## ✨ Tính năng cốt lõi

- **Real-time Synchronization:** Sử dụng SignalR Hub để truyền tải các thao tác chỉnh sửa node, kết nối trên mindmap tức thời đến mọi phiên làm việc của user.
- **Quản lý thực thể:** Cấu trúc cơ sở dữ liệu chặt chẽ quản lý thông tin tài khoản người dùng (`User`) và dữ liệu các sơ đồ (`Mindmap`).
- **Xác thực và phân quyền:** API endpoints xử lý luồng Authentication (`AuthController`) bảo mật.
- **ORM Hiện đại:** Thao tác dữ liệu thông qua Entity Framework Core nhanh chóng, an toàn, hỗ trợ quản lý qua DbContext[cite: 6].

