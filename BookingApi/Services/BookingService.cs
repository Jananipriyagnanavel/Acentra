using BookingApi.Data;
using BookingApi.DTOs;
using BookingApi.Exceptions;
using BookingApi.Hubs;
using BookingApi.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BookingApi.Services;

public interface IBookingService
{
    Task<List<BookingResponse>> GetForUserAsync(Guid userId, bool isAdmin);
    Task<BookingResponse> GetByIdAsync(Guid bookingId, Guid userId, bool isAdmin);
    Task<List<BookingResponse>> GetAvailabilityAsync(AvailabilityQuery query);
    Task<BookingResponse> CreateAsync(Guid userId, CreateBookingRequest request);
    Task<BookingResponse> UpdateAsync(Guid bookingId, Guid userId, bool isAdmin, UpdateBookingRequest request);
    Task CancelAsync(Guid bookingId, Guid userId, bool isAdmin);
    Task<RecurringBookingResult> CreateRecurringAsync(Guid userId, RecurringBookingRequest request);
}

public class BookingService : IBookingService
{
    // Postgres SQLSTATE for an exclusion constraint violation.
    private const string ExclusionViolationSqlState = "23P01";

    private readonly AppDbContext _db;
    private readonly IQrCodeService _qrCodeService;
    private readonly IBookingHubNotifier _hubNotifier;
    private readonly IEmailService _emailService;
    private readonly IWaitingListService _waitingListService;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        AppDbContext db,
        IQrCodeService qrCodeService,
        IBookingHubNotifier hubNotifier,
        IEmailService emailService,
        IWaitingListService waitingListService,
        ILogger<BookingService> logger)
    {
        _db = db;
        _qrCodeService = qrCodeService;
        _hubNotifier = hubNotifier;
        _emailService = emailService;
        _waitingListService = waitingListService;
        _logger = logger;
    }

    public async Task<List<BookingResponse>> GetForUserAsync(Guid userId, bool isAdmin)
    {
        var query = _db.Bookings
            .Include(b => b.Resource)
            .Include(b => b.User)
            .AsNoTracking();

        if (!isAdmin)
        {
            query = query.Where(b => b.UserId == userId);
        }

        var bookings = await query.OrderByDescending(b => b.StartTime).ToListAsync();
        return bookings.Select(ToResponse).ToList();
    }

    public async Task<BookingResponse> GetByIdAsync(Guid bookingId, Guid userId, bool isAdmin)
    {
        var booking = await _db.Bookings
            .Include(b => b.Resource)
            .Include(b => b.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new NotFoundException("Booking not found.");

        if (!isAdmin && booking.UserId != userId)
        {
            throw new ForbiddenException("You can only view your own bookings.");
        }

        return ToResponse(booking);
    }

    public async Task<List<BookingResponse>> GetAvailabilityAsync(AvailabilityQuery query)
    {
        // Existing confirmed bookings in the window — the frontend uses this
        // to paint the calendar, but it is NOT what protects against
        // double-booking (Part 11). The database exclusion constraint is.
        var bookings = await _db.Bookings
            .Include(b => b.Resource)
            .Include(b => b.User)
            .AsNoTracking()
            .Where(b => b.ResourceId == query.ResourceId
                        && b.Status == BookingStatus.Confirmed
                        && b.StartTime < query.To
                        && b.EndTime > query.From)
            .OrderBy(b => b.StartTime)
            .ToListAsync();

        return bookings.Select(ToResponse).ToList();
    }

    public async Task<BookingResponse> CreateAsync(Guid userId, CreateBookingRequest request)
    {
        ValidateTimeRange(request.StartTime, request.EndTime);

        var resource = await _db.Resources.FirstOrDefaultAsync(r => r.Id == request.ResourceId)
            ?? throw new NotFoundException("Resource not found.");

        if (!resource.IsActive)
        {
            throw new ValidationException("This resource is no longer active and cannot be booked.");
        }

        // Friendly, fast application-level pre-check. This is NOT the real
        // protection against a race — two concurrent requests can both pass
        // this check before either commits. The database exclusion
        // constraint (Data/Sql/001_booking_exclusion_constraint.sql) is the
        // actual guarantee; see the catch block below.
        var hasOverlap = await _db.Bookings.AnyAsync(b =>
            b.ResourceId == request.ResourceId &&
            b.Status == BookingStatus.Confirmed &&
            b.StartTime < request.EndTime &&
            b.EndTime > request.StartTime);

        if (hasOverlap)
        {
            throw new BookingConflictException();
        }

        var booking = new Booking
        {
            ResourceId = request.ResourceId,
            UserId = userId,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Purpose = request.Purpose,
            Status = BookingStatus.Confirmed,
            QrToken = _qrCodeService.GenerateToken()
        };

        _db.Bookings.Add(booking);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsExclusionViolation(ex))
        {
            // Another request won the race between our pre-check and this
            // commit. This is the real double-booking guarantee firing.
            throw new BookingConflictException();
        }

        await _db.Entry(booking).Reference(b => b.Resource).LoadAsync();
        await _db.Entry(booking).Reference(b => b.User).LoadAsync();

        await _hubNotifier.NotifyBookingCreated(booking.ResourceId, booking.Id, booking.StartTime, booking.EndTime);

        // Fire-and-forget from the booking transaction's perspective: email
        // failure must never roll back or block a successful booking.
        _ = SendConfirmationEmailSafelyAsync(booking);

        return ToResponse(booking);
    }

    public async Task<BookingResponse> UpdateAsync(Guid bookingId, Guid userId, bool isAdmin, UpdateBookingRequest request)
    {
        ValidateTimeRange(request.StartTime, request.EndTime);

        var booking = await _db.Bookings.Include(b => b.Resource).Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new NotFoundException("Booking not found.");

        if (!isAdmin && booking.UserId != userId)
        {
            throw new ForbiddenException("You can only modify your own bookings.");
        }

        // Optimistic concurrency: tell EF Core the value it should assume is
        // still current. If someone else updated the row since the client
        // last read it, `xmin` will have changed and SaveChanges will throw
        // DbUpdateConcurrencyException below.
        _db.Entry(booking).Property(b => b.Version).OriginalValue = request.Version;

        booking.StartTime = request.StartTime;
        booking.EndTime = request.EndTime;
        booking.Purpose = request.Purpose;
        booking.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException();
        }
        catch (DbUpdateException ex) when (IsExclusionViolation(ex))
        {
            throw new BookingConflictException();
        }

        return ToResponse(booking);
    }

    public async Task CancelAsync(Guid bookingId, Guid userId, bool isAdmin)
    {
        var booking = await _db.Bookings.Include(b => b.Resource).Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new NotFoundException("Booking not found.");

        if (!isAdmin && booking.UserId != userId)
        {
            throw new ForbiddenException("You can only cancel your own bookings.");
        }

        if (booking.Status != BookingStatus.Confirmed)
        {
            throw new ValidationException("Only confirmed bookings can be cancelled.");
        }

        booking.Status = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _hubNotifier.NotifyBookingCancelled(booking.ResourceId, booking.Id);

        _ = SendCancellationEmailSafelyAsync(booking);

        // Give the next waiting-list entry (if any) a chance at this slot.
        await _waitingListService.NotifyNextInLineAsync(booking.ResourceId, booking.StartTime, booking.EndTime);
    }

    public async Task<RecurringBookingResult> CreateRecurringAsync(Guid userId, RecurringBookingRequest request)
    {
        ValidateTimeRange(request.FirstStartTime, request.FirstEndTime);

        var resource = await _db.Resources.FirstOrDefaultAsync(r => r.Id == request.ResourceId)
            ?? throw new NotFoundException("Resource not found.");

        if (!resource.IsActive)
        {
            throw new ValidationException("This resource is no longer active and cannot be booked.");
        }

        var duration = request.FirstEndTime - request.FirstStartTime;
        var occurrences = GenerateOccurrences(request.FirstStartTime, duration, request.Frequency, request.Occurrences);

        var result = new RecurringBookingResult();
        var recurrenceGroupId = Guid.NewGuid();

        // Check every occurrence against existing confirmed bookings up
        // front so the user sees all conflicts at once (Part 13), rather
        // than discovering them one at a time.
        var conflictingSlots = new List<(DateTime Start, DateTime End)>();
        foreach (var (start, end) in occurrences)
        {
            var overlap = await _db.Bookings.AnyAsync(b =>
                b.ResourceId == request.ResourceId &&
                b.Status == BookingStatus.Confirmed &&
                b.StartTime < end &&
                b.EndTime > start);

            if (overlap)
            {
                conflictingSlots.Add((start, end));
            }
        }

        if (conflictingSlots.Count > 0 && !request.SkipConflicts)
        {
            result.Conflicts = conflictingSlots.Select(c => new ConflictingOccurrence
            {
                StartTime = c.Start,
                EndTime = c.End
            }).ToList();
            return result; // Nothing created — caller must confirm SkipConflicts=true to proceed.
        }

        foreach (var (start, end) in occurrences)
        {
            if (conflictingSlots.Any(c => c.Start == start && c.End == end))
            {
                result.Conflicts.Add(new ConflictingOccurrence { StartTime = start, EndTime = end });
                continue; // SkipConflicts=true: skip this one, keep going.
            }

            var booking = new Booking
            {
                ResourceId = request.ResourceId,
                UserId = userId,
                StartTime = start,
                EndTime = end,
                Purpose = request.Purpose,
                Status = BookingStatus.Confirmed,
                QrToken = _qrCodeService.GenerateToken(),
                RecurrenceGroupId = recurrenceGroupId
            };

            _db.Bookings.Add(booking);

            try
            {
                // Saved one at a time so the exclusion constraint (the real
                // race-condition guard) is checked per-occurrence, and a
                // conflict on one occurrence doesn't lose the others already
                // committed.
                await _db.SaveChangesAsync();
                await _hubNotifier.NotifyBookingCreated(booking.ResourceId, booking.Id, booking.StartTime, booking.EndTime);
                result.Created.Add(ToResponse(booking, resource));
            }
            catch (DbUpdateException ex) when (IsExclusionViolation(ex))
            {
                _db.Entry(booking).State = EntityState.Detached;
                result.Conflicts.Add(new ConflictingOccurrence { StartTime = start, EndTime = end });
            }
        }

        return result;
    }

    private static List<(DateTime Start, DateTime End)> GenerateOccurrences(
        DateTime firstStart, TimeSpan duration, RecurrenceFrequency frequency, int count)
    {
        var occurrences = new List<(DateTime, DateTime)>();
        for (var i = 0; i < count; i++)
        {
            var start = frequency switch
            {
                RecurrenceFrequency.Daily => firstStart.AddDays(i),
                RecurrenceFrequency.Weekly => firstStart.AddDays(7 * i),
                RecurrenceFrequency.Monthly => firstStart.AddMonths(i),
                _ => throw new ArgumentOutOfRangeException(nameof(frequency))
            };
            occurrences.Add((start, start + duration));
        }
        return occurrences;
    }

    private static void ValidateTimeRange(DateTime start, DateTime end)
    {
        if (start >= end)
        {
            throw new ValidationException("Start time must be before end time.");
        }
        if (start < DateTime.UtcNow.AddMinutes(-5))
        {
            throw new ValidationException("Cannot book a time slot in the past.");
        }
    }

    private static bool IsExclusionViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException pgEx && pgEx.SqlState == ExclusionViolationSqlState;
    }

    private async Task SendConfirmationEmailSafelyAsync(Booking booking)
    {
        try
        {
            var email = booking.User?.Email;
            if (string.IsNullOrWhiteSpace(email)) return;

            var subject = $"Booking confirmed: {booking.Resource?.Name}";
            var body = $"<p>Your booking for <strong>{booking.Resource?.Name}</strong> from " +
                       $"{booking.StartTime:g} to {booking.EndTime:g} is confirmed.</p>";

            await _emailService.SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send booking confirmation email for booking {BookingId}", booking.Id);
        }
    }

    private async Task SendCancellationEmailSafelyAsync(Booking booking)
    {
        try
        {
            var email = booking.User?.Email;
            if (string.IsNullOrWhiteSpace(email)) return;

            var subject = $"Booking cancelled: {booking.Resource?.Name}";
            var body = $"<p>Your booking for <strong>{booking.Resource?.Name}</strong> from " +
                       $"{booking.StartTime:g} to {booking.EndTime:g} has been cancelled.</p>";

            await _emailService.SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send cancellation email for booking {BookingId}", booking.Id);
        }
    }

    private static BookingResponse ToResponse(Booking b, Resource? resourceOverride = null)
    {
        var resource = resourceOverride ?? b.Resource;
        return new BookingResponse
        {
            Id = b.Id,
            ResourceId = b.ResourceId,
            ResourceName = resource?.Name ?? string.Empty,
            UserId = b.UserId,
            UserFullName = b.User?.FullName ?? string.Empty,
            StartTime = b.StartTime,
            EndTime = b.EndTime,
            Status = b.Status.ToString(),
            Purpose = b.Purpose,
            Version = b.Version,
            CheckInStatus = b.CheckInStatus.ToString(),
            RecurrenceGroupId = b.RecurrenceGroupId
        };
    }
}
