using System.ComponentModel.DataAnnotations;
using BookingApi.Models;

namespace BookingApi.DTOs;

public class ResourceResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string[] Features { get; set; } = Array.Empty<string>();
    public bool IsActive { get; set; }
}

public class CreateResourceRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public ResourceType ResourceType { get; set; }

    [MaxLength(200)]
    public string Location { get; set; } = string.Empty;

    [Range(1, 10000)]
    public int Capacity { get; set; } = 1;

    public string[] Features { get; set; } = Array.Empty<string>();
}

public class UpdateResourceRequest : CreateResourceRequest
{
    public bool IsActive { get; set; } = true;
}
