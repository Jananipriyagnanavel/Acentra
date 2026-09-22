namespace BookingApi.DTOs;

public class AdminDashboardResponse
{
    public int TodaysBookingsCount { get; set; }
    public int ActiveBookingsCount { get; set; }
    public int AvailableResourcesCount { get; set; }
    public int CancelledBookingsCount { get; set; }
    public int NoShowCount { get; set; }
    public List<ResourceUtilization> ResourceUtilization { get; set; } = new();
    public List<BookingResponse> RecentBookings { get; set; } = new();
}

public class ResourceUtilization
{
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public int BookingsLast30Days { get; set; }
}

public class UserSummaryResponse
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = new List<string>();
}
