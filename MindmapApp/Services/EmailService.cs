using System;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace MindmapApp.Services;

public class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "Mindmap App";
    public string SenderPassword { get; set; } = string.Empty;
}

public class EmailService
{
    private readonly EmailSettings _settings;

    public EmailService(EmailSettings settings)
    {
        _settings = settings;
    }

    public async Task SendOtpAsync(string recipientEmail, string code)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = "Mã OTP khôi phục mật khẩu";
        message.Body = new TextPart("html")
        {
            Text = $@"<p>Xin chào,</p>
<p>Bạn đã yêu cầu đặt lại mật khẩu trên MindmapApp.</p>
<p>Mã OTP của bạn là: <strong>{code}</strong></p>
<p>Mã chỉ có hiệu lực trong 10 phút. Nếu bạn không yêu cầu, vui lòng bỏ qua email này.</p>
<p>Trân trọng,<br/>MindmapApp</p>"
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, _settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);
        if (!string.IsNullOrWhiteSpace(_settings.SenderPassword))
        {
            await client.AuthenticateAsync(_settings.SenderEmail, _settings.SenderPassword);
        }
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
