namespace BookingApi.Models;

public enum BookingStatus
{
    Confirmed,
    Cancelled,
    Completed,
    NoShow
}

public enum CheckInStatus
{
    NotCheckedIn,
    CheckedIn
}

public class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ResourceId { get; set; }
    public Resource? Resource { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public string Purpose { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Optimistic concurrency token. PostgreSQL has no built-in rowversion type
    // like SQL Server, so we map EF Core's concurrency token to the Postgres
    // system column `xmin`, which changes automatically on every UPDATE.
    // See Data/AppDbContext.cs OnModelCreating for the mapping.
    public uint Version { get; set; }

    // Set for a recurring booking's individual occurrences, null for one-off bookings.
    public Guid? RecurrenceGroupId { get; set; }

    public string QrToken { get; set; } = string.Empty;
    public CheckInStatus CheckInStatus { get; set; } = CheckInStatus.NotCheckedIn;
    public DateTime? CheckedInAt { get; set; }
}
