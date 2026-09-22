using BookingApi.DTOs;
using BookingApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingApi.Controllers;

[ApiController]
[Route("api/resources")]
[Authorize] // any authenticated user (USER or ADMIN) can browse resources
public class ResourcesController : ControllerBase
{
    private readonly IResourceService _resourceService;

    public ResourcesController(IResourceService resourceService)
    {
        _resourceService = resourceService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ResourceResponse>>>> GetAll()
    {
        var resources = await _resourceService.GetAllAsync(includeInactive: User.IsInRole(Models.Roles.Admin));
        return Ok(ApiResponse<List<ResourceResponse>>.Ok(resources));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ResourceResponse>>> GetById(Guid id)
    {
        var resource = await _resourceService.GetByIdAsync(id);
        return Ok(ApiResponse<ResourceResponse>.Ok(resource));
    }
}

[ApiController]
[Route("api/admin/resources")]
[Authorize(Roles = Models.Roles.Admin)]
public class AdminResourcesController : ControllerBase
{
    private readonly IResourceService _resourceService;

    public AdminResourcesController(IResourceService resourceService)
    {
        _resourceService = resourceService;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ResourceResponse>>> Create([FromBody] CreateResourceRequest request)
    {
        var resource = await _resourceService.CreateAsync(request);
        return CreatedAtAction(nameof(Create), new { id = resource.Id }, ApiResponse<ResourceResponse>.Ok(resource));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ResourceResponse>>> Update(Guid id, [FromBody] UpdateResourceRequest request)
    {
        var resource = await _resourceService.UpdateAsync(id, request);
        return Ok(ApiResponse<ResourceResponse>.Ok(resource));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await _resourceService.DeactivateAsync(id);
        return NoContent();
    }
}
