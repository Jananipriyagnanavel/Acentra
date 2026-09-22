using System.ComponentModel.DataAnnotations;

namespace BookingApi.DTOs;

public class JoinWaitingListRequest
{
    [Required]
    public Guid ResourceId { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }
}

public class WaitingListResponse
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Position { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? NotificationExpiresAt { get; set; }
}
