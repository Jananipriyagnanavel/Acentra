using BookingApi.Data;
using BookingApi.DTOs;
using BookingApi.Exceptions;
using BookingApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BookingApi.Services;

public interface IWaitingListService
{
    Task<WaitingListResponse> JoinAsync(Guid userId, JoinWaitingListRequest request);
    Task<List<WaitingListResponse>> GetForUserAsync(Guid userId);
    Task LeaveAsync(Guid entryId, Guid userId);

    /// <summary>Called after a booking is cancelled. Finds the longest-waiting
    /// eligible entry for that exact resource/time window and marks it
    /// Notified — it does NOT auto-create a booking (Part 15 explicitly
    /// forbids that without a clear confirmation step).</summary>
    Task NotifyNextInLineAsync(Guid resourceId, DateTime startTime, DateTime endTime);
}

public class WaitingListService : IWaitingListService
{
    private static readonly TimeSpan NotificationWindow = TimeSpan.FromHours(2);

    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly ILogger<WaitingListService> _logger;

    public WaitingListService(AppDbContext db, IEmailService emailService, ILogger<WaitingListService> logger)
    {
        _db = db;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<WaitingListResponse> JoinAsync(Guid userId, JoinWaitingListRequest request)
    {
        var resource = await _db.Resources.FirstOrDefaultAsync(r => r.Id == request.ResourceId)
            ?? throw new NotFoundException("Resource not found.");

        var currentCount = await _db.WaitingListEntries.CountAsync(w =>
            w.ResourceId == request.ResourceId &&
            w.StartTime == request.StartTime &&
            w.EndTime == request.EndTime &&
            w.Status == WaitingListStatus.Waiting);

        var entry = new WaitingListEntry
        {
            UserId = userId,
            ResourceId = request.ResourceId,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Position = currentCount + 1,
            Status = WaitingListStatus.Waiting
        };

        _db.WaitingListEntries.Add(entry);
        await _db.SaveChangesAsync();

        return new WaitingListResponse
        {
            Id = entry.Id,
            ResourceId = entry.ResourceId,
            ResourceName = resource.Name,
            StartTime = entry.StartTime,
            EndTime = entry.EndTime,
            Position = entry.Position,
            Status = entry.Status.ToString()
        };
    }

    public async Task<List<WaitingListResponse>> GetForUserAsync(Guid userId)
    {
        var entries = await _db.WaitingListEntries
            .Include(w => w.Resource)
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync();

        return entries.Select(e => new WaitingListResponse
        {
            Id = e.Id,
            ResourceId = e.ResourceId,
            ResourceName = e.Resource?.Name ?? string.Empty,
            StartTime = e.StartTime,
            EndTime = e.EndTime,
            Position = e.Position,
            Status = e.Status.ToString(),
            NotificationExpiresAt = e.NotificationExpiresAt
        }).ToList();
    }

    public async Task LeaveAsync(Guid entryId, Guid userId)
    {
        var entry = await _db.WaitingListEntries.FirstOrDefaultAsync(w => w.Id == entryId)
            ?? throw new NotFoundException("Waiting list entry not found.");

        if (entry.UserId != userId)
        {
            throw new ForbiddenException("You can only remove your own waiting list entries.");
        }

        entry.Status = WaitingListStatus.Cancelled;
        await _db.SaveChangesAsync();
    }

    public async Task NotifyNextInLineAsync(Guid resourceId, DateTime startTime, DateTime endTime)
    {
        var next = await _db.WaitingListEntries
            .Include(w => w.User)
            .Include(w => w.Resource)
            .Where(w => w.ResourceId == resourceId &&
                        w.StartTime == startTime &&
                        w.EndTime == endTime &&
                        w.Status == WaitingListStatus.Waiting)
            .OrderBy(w => w.Position)
            .FirstOrDefaultAsync();

        if (next == null)
        {
            return; // Nobody waiting for this exact slot.
        }

        next.Status = WaitingListStatus.Notified;
        next.NotifiedAt = DateTime.UtcNow;
        next.NotificationExpiresAt = DateTime.UtcNow.Add(NotificationWindow);
        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(next.User?.Email))
        {
            var subject = $"A slot opened up: {next.Resource?.Name}";
            var body = $"<p>{next.Resource?.Name} is now available from {next.StartTime:g} to " +
                       $"{next.EndTime:g}. You have until {next.NotificationExpiresAt:g} to book it " +
                       "before it's offered to the next person on the list.</p>";

            var sent = await _emailService.SendEmailAsync(next.User.Email, subject, body);
            if (!sent)
            {
                _logger.LogWarning("Waiting-list notification email failed for entry {EntryId}", next.Id);
            }
        }
    }
}
