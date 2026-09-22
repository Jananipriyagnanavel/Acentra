using BookingApi.Authentication;
using BookingApi.DTOs;
using BookingApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingApi.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<BookingResponse>>>> GetAll()
    {
        var bookings = await _bookingService.GetForUserAsync(User.GetUserId(), User.IsAdmin());
        return Ok(ApiResponse<List<BookingResponse>>.Ok(bookings));
    }

    [HttpGet("availability")]
    public async Task<ActionResult<ApiResponse<List<BookingResponse>>>> GetAvailability([FromQuery] AvailabilityQuery query)
    {
        var bookings = await _bookingService.GetAvailabilityAsync(query);
        return Ok(ApiResponse<List<BookingResponse>>.Ok(bookings));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> GetById(Guid id)
    {
        var booking = await _bookingService.GetByIdAsync(id, User.GetUserId(), User.IsAdmin());
        return Ok(ApiResponse<BookingResponse>.Ok(booking));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> Create([FromBody] CreateBookingRequest request)
    {
        var booking = await _bookingService.CreateAsync(User.GetUserId(), request);
        return CreatedAtAction(nameof(GetById), new { id = booking.Id }, ApiResponse<BookingResponse>.Ok(booking));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> Update(Guid id, [FromBody] UpdateBookingRequest request)
    {
        var booking = await _bookingService.UpdateAsync(id, User.GetUserId(), User.IsAdmin(), request);
        return Ok(ApiResponse<BookingResponse>.Ok(booking));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await _bookingService.CancelAsync(id, User.GetUserId(), User.IsAdmin());
        return NoContent();
    }

    [HttpPost("recurring")]
    public async Task<ActionResult<ApiResponse<RecurringBookingResult>>> CreateRecurring([FromBody] RecurringBookingRequest request)
    {
        var result = await _bookingService.CreateRecurringAsync(User.GetUserId(), request);
        return Ok(ApiResponse<RecurringBookingResult>.Ok(result));
    }
}
