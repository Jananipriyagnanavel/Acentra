using BookingApi.Data;
using BookingApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BookingApi.Services;

/// <summary>
/// Runs periodically to: (1) send a reminder email ~30 minutes before a
/// booking starts, and (2) expire waiting-list offers whose confirmation
/// window has passed and pass the offer to the next person in line.
/// Kept as a simple polling loop rather than a full job scheduler, in
/// keeping with Part 1 rule 17 (simple enough for a hackathon team to
/// maintain).
/// </summary>
public class BookingReminderBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ReminderLeadTime = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingReminderBackgroundService> _logger;

    public BookingReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendDueRemindersAsync(stoppingToken);
                await ExpireStaleWaitingListOffersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in booking background service loop");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task SendDueRemindersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var now = DateTime.UtcNow;
        var reminderWindowEnd = now.Add(ReminderLeadTime);

        // Reuse the Notifications table as a simple "have we already sent
        // this reminder" guard, keyed by booking id + type.
        var candidates = await db.Bookings
            .Include(b => b.Resource)
            .Include(b => b.User)
            .Where(b => b.Status == BookingStatus.Confirmed &&
                        b.StartTime > now &&
                        b.StartTime <= reminderWindowEnd)
            .ToListAsync(ct);

        foreach (var booking in candidates)
        {
            var alreadySent = await db.Notifications.AnyAsync(n =>
                n.BookingId == booking.Id && n.Type == NotificationType.BookingReminder, ct);

            if (alreadySent || string.IsNullOrWhiteSpace(booking.User?.Email))
            {
                continue;
            }

            var subject = $"Reminder: {booking.Resource?.Name} starts soon";
            var body = $"<p>Reminder: your booking for <strong>{booking.Resource?.Name}</strong> starts at " +
                       $"{booking.StartTime:g}.</p>";

            var sent = await emailService.SendEmailAsync(booking.User.Email, subject, body);

            db.Notifications.Add(new Notification
            {
                UserId = booking.UserId,
                BookingId = booking.Id,
                Type = NotificationType.BookingReminder,
                Status = sent ? NotificationStatus.Sent : NotificationStatus.Failed,
                Subject = subject,
                Body = body,
                SentAt = sent ? DateTime.UtcNow : null
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ExpireStaleWaitingListOffersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var waitingListService = scope.ServiceProvider.GetRequiredService<IWaitingListService>();

        var now = DateTime.UtcNow;
        var expired = await db.WaitingListEntries
            .Where(w => w.Status == WaitingListStatus.Notified && w.NotificationExpiresAt < now)
            .ToListAsync(ct);

        foreach (var entry in expired)
        {
            entry.Status = WaitingListStatus.Expired;
            // Pass the offer along to whoever is next for the same slot.
            await waitingListService.NotifyNextInLineAsync(entry.ResourceId, entry.StartTime, entry.EndTime);
        }

        if (expired.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
