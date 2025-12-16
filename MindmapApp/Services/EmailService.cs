using System;
using System.Net;
using System.Net.Mail;
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

    // Fallback credentials from user's sample code (used only if settings and env are not provided)
    private const string DefaultSmtpHost = "smtp.gmail.com";
    private const int DefaultSmtpPort = 587;
    private const bool DefaultUseSsl = true;
    private const string DefaultSenderEmail = "anhkietphan2402.tg@gmail.com";
    private const string DefaultSenderName = "MindmapApp";
    private const string DefaultSenderPassword = "vbfzwjqaklkirlmn"; // provided by user sample

    public EmailService(EmailSettings settings)
    {
        _settings = settings ?? new EmailSettings();
    }

    public async Task SendOtpAsync(string recipientEmail, string code)
    {
        // Determine effective settings (fall back to defaults if not configured)
        var host = string.IsNullOrWhiteSpace(_settings.SmtpHost) ? DefaultSmtpHost : _settings.SmtpHost;
        var port = _settings.SmtpPort != 0 ? _settings.SmtpPort : DefaultSmtpPort;
        var useSsl = _settings.UseSsl;
        if (_settings.SmtpHost == null || _settings.SmtpHost.Length == 0)
            useSsl = DefaultUseSsl;
        var senderEmail = string.IsNullOrWhiteSpace(_settings.SenderEmail) ? DefaultSenderEmail : _settings.SenderEmail;
        var senderName = string.IsNullOrWhiteSpace(_settings.SenderName) ? DefaultSenderName : _settings.SenderName;
        var senderPassword = string.IsNullOrWhiteSpace(_settings.SenderPassword) ? DefaultSenderPassword : _settings.SenderPassword;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(senderName, senderEmail));
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

        // First try MailKit (recommended)
        try
        {
            using var client = new MailKit.Net.Smtp.SmtpClient();
            await client.ConnectAsync(host, port, useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);
            if (!string.IsNullOrWhiteSpace(senderPassword))
            {
                await client.AuthenticateAsync(senderEmail, senderPassword);
            }
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            return;
        }
        catch (Exception mailKitEx)
        {
            // fallback to System.Net.Mail SmtpClient
            try
            {
                var mail = new System.Net.Mail.MailMessage();
                mail.From = new System.Net.Mail.MailAddress(senderEmail, senderName);
                mail.To.Add(new System.Net.Mail.MailAddress(recipientEmail));
                mail.Subject = message.Subject;
                mail.Body = (message.Body as TextPart)?.Text ?? string.Empty;
                mail.IsBodyHtml = true;

                using var smtp = new System.Net.Mail.SmtpClient(host, port)
                {
                    EnableSsl = useSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(senderEmail, senderPassword),
                    Timeout = 200000
                };

                // Send synchronously on threadpool to keep API async
                await Task.Run(() => smtp.Send(mail));
                return;
            }
            catch (Exception smtpEx)
            {
                throw new InvalidOperationException($"Failed to send OTP. MailKit error: {mailKitEx.Message}; SmtpClient error: {smtpEx.Message}", smtpEx);
            }
        }
    }
}
