using BookingApi.DTOs;
using BookingApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingApi.Controllers;

[ApiController]
public class CheckInController : ControllerBase
{
    private readonly ICheckInService _checkInService;

    public CheckInController(ICheckInService checkInService)
    {
        _checkInService = checkInService;
    }

    // Public: this is exactly what the QR code points to. No auth required,
    // since the unguessable token itself is the credential (Part 14).
    [HttpGet("api/checkin/{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> CheckInByToken(string token)
    {
        var result = await _checkInService.CheckInAsync(token, HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(ApiResponse<BookingResponse>.Ok(result));
    }

    // Authenticated: lets staff/admin manually check someone in by booking id
    // if they don't have their QR handy.
    [HttpPost("api/bookings/{id:guid}/checkin")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> CheckInByBookingId(Guid id)
    {
        var result = await _checkInService.CheckInByBookingIdAsync(id, HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(ApiResponse<BookingResponse>.Ok(result));
    }
}
