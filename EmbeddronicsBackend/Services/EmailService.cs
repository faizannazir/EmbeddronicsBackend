using System.Net;
using System.Net.Mail;
using EmbeddronicsBackend.Controllers;
using Microsoft.Extensions.Options;

namespace EmbeddronicsBackend.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task<bool> SendContactFormEmailAsync(ContactRequest request)
        {
            try
            {
                var subject = string.IsNullOrEmpty(request.Subject) 
                    ? $"New Contact Form Submission from {request.Name}" 
                    : $"Contact Form: {request.Subject}";

                var body = $@"
                    <h2>New Contact Form Submission</h2>
                    <p><strong>Name:</strong> {request.Name}</p>
                    <p><strong>Email:</strong> {request.Email}</p>
                    <p><strong>Phone:</strong> {request.Phone}</p>
                    <p><strong>Company:</strong> {request.Company}</p>
                    <p><strong>Subject:</strong> {request.Subject}</p>
                    <p><strong>Message:</strong></p>
                    <p>{request.Message.Replace("\n", "<br>")}</p>
                    <hr>
                    <p><small>Submitted at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</small></p>
                ";

                return await SendEmailAsync(_emailSettings.AdminEmail, subject, body, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send contact form email for {Name} ({Email})", request.Name, request.Email);
                return false;
            }
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = false)
        {
            try
            {
                if (!_emailSettings.IsEnabled)
                {
                    _logger.LogInformation("Email service is disabled. Would send email to {To} with subject: {Subject}", to, subject);
                    return true; // Return true for development/testing
                }

                using var client = new SmtpClient(_emailSettings.SmtpHost, _emailSettings.SmtpPort)
                {
                    Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password),
                    EnableSsl = _emailSettings.EnableSsl
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_emailSettings.FromEmail, _emailSettings.FromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = isHtml
                };

                mailMessage.To.Add(to);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Email sent successfully to {To} with subject: {Subject}", to, subject);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To} with subject: {Subject}", to, subject);
                return false;
            }
        }

        public async Task<bool> SendWelcomeEmailAsync(string email, string name)
        {
            try
            {
                var subject = "Welcome to Embeddronics!";
                var body = $@"
                    <h2>Welcome to Embeddronics, {name}!</h2>
                    <p>Thank you for registering with us. Your account has been created and is pending approval.</p>
                    <p>You will receive another email once your account has been approved by our team.</p>
                    <p>If you have any questions, please don't hesitate to contact us.</p>
                    <br>
                    <p>Best regards,<br>The Embeddronics Team</p>
                ";

                return await SendEmailAsync(email, subject, body, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send welcome email to {Email}", email);
                return false;
            }
        }

        public async Task<bool> SendOrderStatusUpdateEmailAsync(string email, string orderTitle, string status)
        {
            try
            {
                var subject = $"Order Update: {orderTitle}";
                var body = $@"
                    <h2>Order Status Update</h2>
                    <p>Your order <strong>{orderTitle}</strong> has been updated.</p>
                    <p><strong>New Status:</strong> {status}</p>
                    <p>You can view the full details of your order by logging into your client portal.</p>
                    <br>
                    <p>Best regards,<br>The Embeddronics Team</p>
                ";

                return await SendEmailAsync(email, subject, body, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send order status update email to {Email} for order {OrderTitle}", email, orderTitle);
                return false;
            }
        }

        public async Task<bool> SendQuoteNotificationEmailAsync(string email, string orderTitle, decimal amount)
        {
            try
            {
                var subject = $"New Quote Available: {orderTitle}";
                var body = $@"
                    <h2>New Quote Available</h2>
                    <p>A new quote has been prepared for your order <strong>{orderTitle}</strong>.</p>
                    <p><strong>Quote Amount:</strong> ${amount:N2}</p>
                    <p>Please log into your client portal to review and accept the quote.</p>
                    <br>
                    <p>Best regards,<br>The Embeddronics Team</p>
                ";

                return await SendEmailAsync(email, subject, body, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send quote notification email to {Email} for order {OrderTitle}", email, orderTitle);
                return false;
            }
        }
    }

    public class EmailSettings
    {
        public bool IsEnabled { get; set; } = false;
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = "Embeddronics";
        public string AdminEmail { get; set; } = string.Empty;
    }
}