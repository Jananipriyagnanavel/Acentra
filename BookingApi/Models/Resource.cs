namespace BookingApi.Models;

public enum ResourceType
{
    MeetingRoom,
    ConferenceRoom,
    ComputerLab,
    Workspace,
    Equipment,
    Other
}

public class Resource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ResourceType ResourceType { get; set; }
    public string Location { get; set; } = string.Empty;
    public int Capacity { get; set; }

    // Stored as a simple comma-separated string to keep the schema simple;
    // exposed to the frontend as a string[] via the DTO layer.
    public string Features { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<WaitingListEntry> WaitingListEntries { get; set; } = new List<WaitingListEntry>();
}
