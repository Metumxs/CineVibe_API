using CineVibe.API.Configuration;
using CineVibe.API.Services.Interfaces;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace CineVibe.API.Services
{
    public class SendGridEmailService : IEmailService
    {
        private readonly SendGridSettings _sendGridSettings;
        private readonly ILogger<SendGridEmailService> _logger;
        private readonly HttpClient _sendGridHttpClient;

        public SendGridEmailService(
            IOptions<SendGridSettings> settings,
            ILogger<SendGridEmailService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _sendGridSettings = settings.Value;
            _logger = logger;
            _sendGridHttpClient = httpClientFactory.CreateClient("SendGridClient");
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlContent)
        {
            // Pass the managed HttpClient to SendGridClient
            var client = new SendGridClient(_sendGridHttpClient, _sendGridSettings.ApiKey);

            var from = new EmailAddress(_sendGridSettings.SenderEmail, _sendGridSettings.SenderName);
            var to = new EmailAddress(toEmail);

            var message = MailHelper.CreateSingleEmail(
                from, to, subject,
                plainTextContent: null,
                htmlContent: htmlContent);

            var response = await client.SendEmailAsync(message);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Body.ReadAsStringAsync();

                _logger.LogError(
                    "SendGrid failed to send email to {Email}. StatusCode: {StatusCode}. Details: {Details}",
                    toEmail,
                    response.StatusCode,
                    errorBody);

                throw new InvalidOperationException($"Failed to send email to {toEmail}. Status: {response.StatusCode}");
            }
        }
    }
}