namespace BookingApi.Models;

public class CheckIn
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }

    public DateTime CheckedInAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
}

public enum NotificationType
{
    BookingConfirmation,
    BookingCancellation,
    BookingReminder,
    WaitingListOffer
}

public enum NotificationStatus
{
    Pending,
    Sent,
    Failed
}

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public Guid? BookingId { get; set; }

    public NotificationType Type { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
}
