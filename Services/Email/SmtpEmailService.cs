using System.Net;
using System.Net.Mail;

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
        var host = _configuration["Smtp:Host"];
        var port = int.Parse(_configuration["Smtp:Port"]!);
        var enableSsl = bool.Parse(_configuration["Smtp:EnableSsl"]!);
        var userName = _configuration["Smtp:UserName"];
        var password = _configuration["Smtp:Password"];
        var fromEmail = _configuration["Smtp:FromEmail"];
        var fromName = _configuration["Smtp:FromName"];

        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrWhiteSpace(userName)
            || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(fromEmail))
        {
            throw new InvalidOperationException("Thiếu cấu hình SMTP trong appsettings hoặc User Secrets.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };

        message.To.Add(toEmail);

        using var smtpClient = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            Credentials = new NetworkCredential(userName, password)
        };

        await smtpClient.SendMailAsync(message);
    }
}