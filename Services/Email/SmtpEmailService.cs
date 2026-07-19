using System.Net;
using System.Net.Mail;
using System.Text;

namespace Eliteracingleague.API.Services.Email;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public SmtpEmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new ArgumentException("Recipient email is required.", nameof(toEmail));

        var host = _configuration["Smtp:Host"];
        var portText = _configuration["Smtp:Port"];
        var enableSslText = _configuration["Smtp:EnableSsl"];
        var userName = _configuration["Smtp:UserName"];
        var password = _configuration["Smtp:Password"];
        var fromEmail = _configuration["Smtp:FromEmail"];
        var fromName = _configuration["Smtp:FromName"] ?? "Elite Racing League";

        if (string.IsNullOrWhiteSpace(host)
            || !int.TryParse(portText, out var port)
            || port <= 0
            || string.IsNullOrWhiteSpace(userName)
            || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(fromEmail))
        {
            throw new InvalidOperationException("Thiếu hoặc sai cấu hình SMTP trong appsettings/User Secrets.");
        }

        var enableSsl = !bool.TryParse(enableSslText, out var parsedSsl) || parsedSsl;
        var timeoutMs = _configuration.GetValue("Smtp:TimeoutMilliseconds", 20_000);

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName, Encoding.UTF8),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            Body = htmlBody,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = true
        };

        message.To.Add(new MailAddress(toEmail));

        using var smtpClient = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(userName, password),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = Math.Max(5_000, timeoutMs)
        };

        await smtpClient.SendMailAsync(message);
    }
}
