using Microsoft.AspNetCore.SignalR;

namespace BookingApi.Hubs;

public class BookingHub : Hub
{
    // Clients join a group per resource so updates only go to people
    // actually viewing that resource's calendar.
    public async Task JoinResourceGroup(string resourceId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(resourceId));
    }

    public async Task LeaveResourceGroup(string resourceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(resourceId));
    }

    public static string GroupName(string resourceId) => $"resource:{resourceId}";
}

public interface IBookingHubNotifier
{
    Task NotifyBookingCreated(Guid resourceId, Guid bookingId, DateTime startTime, DateTime endTime);
    Task NotifyBookingCancelled(Guid resourceId, Guid bookingId);
}

/// <summary>
/// Thin wrapper around IHubContext so BookingService doesn't take a direct
/// dependency on SignalR types. Broadcasts happen AFTER the database commit
/// succeeds — SignalR is a UI convenience, not part of the concurrency
/// guarantee (Part 12).
/// </summary>
public class BookingHubNotifier : IBookingHubNotifier
{
    private readonly IHubContext<BookingHub> _hub;

    public BookingHubNotifier(IHubContext<BookingHub> hub)
    {
        _hub = hub;
    }

    public Task NotifyBookingCreated(Guid resourceId, Guid bookingId, DateTime startTime, DateTime endTime)
    {
        return _hub.Clients.Group(BookingHub.GroupName(resourceId.ToString()))
            .SendAsync("BookingCreated", new { resourceId, bookingId, startTime, endTime });
    }

    public Task NotifyBookingCancelled(Guid resourceId, Guid bookingId)
    {
        return _hub.Clients.Group(BookingHub.GroupName(resourceId.ToString()))
            .SendAsync("BookingCancelled", new { resourceId, bookingId });
    }
}
