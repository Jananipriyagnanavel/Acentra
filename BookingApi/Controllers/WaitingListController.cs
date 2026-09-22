using BookingApi.Authentication;
using BookingApi.DTOs;
using BookingApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingApi.Controllers;

[ApiController]
[Route("api/waiting-list")]
[Authorize]
public class WaitingListController : ControllerBase
{
    private readonly IWaitingListService _waitingListService;

    public WaitingListController(IWaitingListService waitingListService)
    {
        _waitingListService = waitingListService;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<WaitingListResponse>>> Join([FromBody] JoinWaitingListRequest request)
    {
        var result = await _waitingListService.JoinAsync(User.GetUserId(), request);
        return Ok(ApiResponse<WaitingListResponse>.Ok(result));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<WaitingListResponse>>>> GetMine()
    {
        var result = await _waitingListService.GetForUserAsync(User.GetUserId());
        return Ok(ApiResponse<List<WaitingListResponse>>.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Leave(Guid id)
    {
        await _waitingListService.LeaveAsync(id, User.GetUserId());
        return NoContent();
    }
}
