using BookingApi.Data;
using BookingApi.DTOs;
using BookingApi.Exceptions;
using BookingApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BookingApi.Services;

public interface IResourceService
{
    Task<List<ResourceResponse>> GetAllAsync(bool includeInactive);
    Task<ResourceResponse> GetByIdAsync(Guid id);
    Task<ResourceResponse> CreateAsync(CreateResourceRequest request);
    Task<ResourceResponse> UpdateAsync(Guid id, UpdateResourceRequest request);
    Task DeactivateAsync(Guid id);
}

public class ResourceService : IResourceService
{
    private readonly AppDbContext _db;

    public ResourceService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<ResourceResponse>> GetAllAsync(bool includeInactive)
    {
        var query = _db.Resources.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }

        var resources = await query.OrderBy(r => r.Name).ToListAsync();
        return resources.Select(ToResponse).ToList();
    }

    public async Task<ResourceResponse> GetByIdAsync(Guid id)
    {
        var resource = await _db.Resources.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new NotFoundException("Resource not found.");
        return ToResponse(resource);
    }

    public async Task<ResourceResponse> CreateAsync(CreateResourceRequest request)
    {
        var resource = new Resource
        {
            Name = request.Name,
            Description = request.Description,
            ResourceType = request.ResourceType,
            Location = request.Location,
            Capacity = request.Capacity,
            Features = string.Join(",", request.Features),
            IsActive = true
        };

        _db.Resources.Add(resource);
        await _db.SaveChangesAsync();
        return ToResponse(resource);
    }

    public async Task<ResourceResponse> UpdateAsync(Guid id, UpdateResourceRequest request)
    {
        var resource = await _db.Resources.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new NotFoundException("Resource not found.");

        resource.Name = request.Name;
        resource.Description = request.Description;
        resource.ResourceType = request.ResourceType;
        resource.Location = request.Location;
        resource.Capacity = request.Capacity;
        resource.Features = string.Join(",", request.Features);
        resource.IsActive = request.IsActive;
        resource.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ToResponse(resource);
    }

    public async Task DeactivateAsync(Guid id)
    {
        var resource = await _db.Resources.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new NotFoundException("Resource not found.");

        // Deactivate rather than delete: existing bookings must remain
        // available for reporting/history (Part 5).
        resource.IsActive = false;
        resource.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private static ResourceResponse ToResponse(Resource r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Description = r.Description,
        ResourceType = r.ResourceType.ToString(),
        Location = r.Location,
        Capacity = r.Capacity,
        Features = string.IsNullOrWhiteSpace(r.Features) ? Array.Empty<string>() : r.Features.Split(','),
        IsActive = r.IsActive
    };
}
