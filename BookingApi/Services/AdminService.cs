using BookingApi.Data;
using BookingApi.DTOs;
using BookingApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BookingApi.Services;

public interface IAdminService
{
    Task<AdminDashboardResponse> GetDashboardAsync();
    Task<List<BookingResponse>> GetAllBookingsAsync();
    Task<List<UserSummaryResponse>> GetAllUsersAsync();
}

public class AdminService : IAdminService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminService(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<AdminDashboardResponse> GetDashboardAsync()
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var todayEnd = todayStart.AddDays(1);
        var last30Days = now.AddDays(-30);

        var todaysBookingsCount = await _db.Bookings.CountAsync(b =>
            b.StartTime >= todayStart && b.StartTime < todayEnd && b.Status == BookingStatus.Confirmed);

        var activeBookingsCount = await _db.Bookings.CountAsync(b =>
            b.Status == BookingStatus.Confirmed && b.EndTime >= now);

        var availableResourcesCount = await _db.Resources.CountAsync(r => r.IsActive);

        var cancelledBookingsCount = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.Cancelled);

        var noShowCount = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.NoShow);

        var utilization = await _db.Bookings
            .Where(b => b.CreatedAt >= last30Days && b.Status != BookingStatus.Cancelled)
            .GroupBy(b => new { b.ResourceId, b.Resource!.Name })
            .Select(g => new ResourceUtilization
            {
                ResourceId = g.Key.ResourceId,
                ResourceName = g.Key.Name,
                BookingsLast30Days = g.Count()
            })
            .OrderByDescending(r => r.BookingsLast30Days)
            .ToListAsync();

        var recent = await _db.Bookings
            .Include(b => b.Resource)
            .Include(b => b.User)
            .OrderByDescending(b => b.CreatedAt)
            .Take(10)
            .Select(b => new BookingResponse
            {
                Id = b.Id,
                ResourceId = b.ResourceId,
                ResourceName = b.Resource!.Name,
                UserId = b.UserId,
                UserFullName = b.User!.FullName,
                StartTime = b.StartTime,
                EndTime = b.EndTime,
                Status = b.Status.ToString(),
                Purpose = b.Purpose,
                Version = b.Version,
                CheckInStatus = b.CheckInStatus.ToString(),
                RecurrenceGroupId = b.RecurrenceGroupId
            })
            .ToListAsync();

        return new AdminDashboardResponse
        {
            TodaysBookingsCount = todaysBookingsCount,
            ActiveBookingsCount = activeBookingsCount,
            AvailableResourcesCount = availableResourcesCount,
            CancelledBookingsCount = cancelledBookingsCount,
            NoShowCount = noShowCount,
            ResourceUtilization = utilization,
            RecentBookings = recent
        };
    }

    public async Task<List<BookingResponse>> GetAllBookingsAsync()
    {
        return await _db.Bookings
            .Include(b => b.Resource)
            .Include(b => b.User)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BookingResponse
            {
                Id = b.Id,
                ResourceId = b.ResourceId,
                ResourceName = b.Resource!.Name,
                UserId = b.UserId,
                UserFullName = b.User!.FullName,
                StartTime = b.StartTime,
                EndTime = b.EndTime,
                Status = b.Status.ToString(),
                Purpose = b.Purpose,
                Version = b.Version,
                CheckInStatus = b.CheckInStatus.ToString(),
                RecurrenceGroupId = b.RecurrenceGroupId
            })
            .ToListAsync();
    }

    public async Task<List<UserSummaryResponse>> GetAllUsersAsync()
    {
        var users = await _db.Users.AsNoTracking().ToListAsync();
        var results = new List<UserSummaryResponse>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            results.Add(new UserSummaryResponse
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Roles = roles
            });
        }

        return results;
    }
}
