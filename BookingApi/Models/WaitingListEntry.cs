namespace BookingApi.Models;

public enum WaitingListStatus
{
    Waiting,
    Notified,
    Fulfilled,
    Expired,
    Cancelled
}

public class WaitingListEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public Guid ResourceId { get; set; }
    public Resource? Resource { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    // Position within the queue for this exact resource/time slot (1 = next in line).
    public int Position { get; set; }

    public WaitingListStatus Status { get; set; } = WaitingListStatus.Waiting;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // When a slot opens up, the notified user gets a window to confirm before
    // the offer passes to the next person in line.
    public DateTime? NotifiedAt { get; set; }
    public DateTime? NotificationExpiresAt { get; set; }
}
