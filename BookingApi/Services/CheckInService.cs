using BookingApi.Data;
using BookingApi.DTOs;
using BookingApi.Exceptions;
using BookingApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BookingApi.Services;

public interface ICheckInService
{
    Task<BookingResponse> CheckInAsync(string token, string? ipAddress);
    Task<BookingResponse> CheckInByBookingIdAsync(Guid bookingId, string? ipAddress);
}

public class CheckInService : ICheckInService
{
    // How early a user is allowed to check in before the booking's start time.
    private static readonly TimeSpan CheckInWindowBefore = TimeSpan.FromMinutes(15);

    // How long after the start time check-in is still allowed.
    private static readonly TimeSpan CheckInWindowAfter = TimeSpan.FromMinutes(30);

    private readonly AppDbContext _db;

    public CheckInService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<BookingResponse> CheckInAsync(string token, string? ipAddress)
    {
        var booking = await _db.Bookings
            .Include(b => b.Resource)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.QrToken == token)
            ?? throw new NotFoundException("Invalid or expired check-in code.");

        return await CompleteCheckInAsync(booking, ipAddress);
    }

    public async Task<BookingResponse> CheckInByBookingIdAsync(Guid bookingId, string? ipAddress)
    {
        var booking = await _db.Bookings
            .Include(b => b.Resource)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new NotFoundException("Booking not found.");

        return await CompleteCheckInAsync(booking, ipAddress);
    }

    private async Task<BookingResponse> CompleteCheckInAsync(Booking booking, string? ipAddress)
    {
        if (booking.Status != BookingStatus.Confirmed)
        {
            throw new ValidationException("This booking is not currently confirmed and cannot be checked in.");
        }

        if (booking.CheckInStatus == CheckInStatus.CheckedIn)
        {
            throw new ValidationException("This booking has already been checked in.");
        }

        var now = DateTime.UtcNow;
        var windowStart = booking.StartTime - CheckInWindowBefore;
        var windowEnd = booking.StartTime + CheckInWindowAfter;

        if (now < windowStart || now > windowEnd)
        {
            throw new ValidationException(
                $"Check-in is only available between {windowStart:t} and {windowEnd:t} on the booking day.");
        }

        booking.CheckInStatus = CheckInStatus.CheckedIn;
        booking.CheckedInAt = now;
        booking.UpdatedAt = now;

        _db.CheckIns.Add(new CheckIn
        {
            BookingId = booking.Id,
            CheckedInAt = now,
            IpAddress = ipAddress
        });

        await _db.SaveChangesAsync();

        return new BookingResponse
        {
            Id = booking.Id,
            ResourceId = booking.ResourceId,
            ResourceName = booking.Resource?.Name ?? string.Empty,
            UserId = booking.UserId,
            UserFullName = booking.User?.FullName ?? string.Empty,
            StartTime = booking.StartTime,
            EndTime = booking.EndTime,
            Status = booking.Status.ToString(),
            Purpose = booking.Purpose,
            Version = booking.Version,
            CheckInStatus = booking.CheckInStatus.ToString(),
            RecurrenceGroupId = booking.RecurrenceGroupId
        };
    }
}
