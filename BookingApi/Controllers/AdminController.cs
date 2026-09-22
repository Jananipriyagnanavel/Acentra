using BookingApi.DTOs;
using BookingApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingApi.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = Models.Roles.Admin)]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<AdminDashboardResponse>>> Dashboard()
    {
        var result = await _adminService.GetDashboardAsync();
        return Ok(ApiResponse<AdminDashboardResponse>.Ok(result));
    }

    [HttpGet("bookings")]
    public async Task<ActionResult<ApiResponse<List<BookingResponse>>>> AllBookings()
    {
        var result = await _adminService.GetAllBookingsAsync();
        return Ok(ApiResponse<List<BookingResponse>>.Ok(result));
    }

    [HttpGet("users")]
    public async Task<ActionResult<ApiResponse<List<UserSummaryResponse>>>> AllUsers()
    {
        var result = await _adminService.GetAllUsersAsync();
        return Ok(ApiResponse<List<UserSummaryResponse>>.Ok(result));
    }
}
