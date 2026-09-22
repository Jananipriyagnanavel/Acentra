using Microsoft.AspNetCore.Identity;

namespace BookingApi.Models;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<WaitingListEntry> WaitingListEntries { get; set; } = new List<WaitingListEntry>();
}

public class ApplicationRole : IdentityRole<Guid>
{
}

public static class Roles
{
    public const string User = "USER";
    public const string Admin = "ADMIN";
}
