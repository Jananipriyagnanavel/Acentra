using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BookingApi.Services;

public interface IEmailService
{
    /// <summary>
    /// Sends via Resend's REST API. Returns false (never throws) on failure —
    /// callers must not let email delivery block or fail the booking
    /// transaction itself (Part 16).
    /// </summary>
    Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody);
}

public class ResendEmailOptions
{
    public const string SectionName = "Resend";
    public string ApiKey { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "bookings@example.com";
}

public class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly ResendEmailOptions _options;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(HttpClient httpClient, Microsoft.Extensions.Options.IOptions<ResendEmailOptions> options, ILogger<ResendEmailService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress ??= new Uri("https://api.resend.com/");
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("Resend API key not configured; skipping email to {Email}", toEmail);
            return false;
        }

        var payload = new
        {
            from = _options.FromAddress,
            to = new[] { toEmail },
            subject,
            html = htmlBody
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("emails", content);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Resend email send failed ({Status}): {Body}", response.StatusCode, body);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            // Never let an email provider outage break the booking flow.
            _logger.LogError(ex, "Exception sending email via Resend to {Email}", toEmail);
            return false;
        }
    }
}
