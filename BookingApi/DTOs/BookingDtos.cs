using System.ComponentModel.DataAnnotations;

namespace BookingApi.DTOs;

public class CreateBookingRequest
{
    [Required]
    public Guid ResourceId { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    [MaxLength(500)]
    public string Purpose { get; set; } = string.Empty;
}

public class UpdateBookingRequest
{
    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    [MaxLength(500)]
    public string Purpose { get; set; } = string.Empty;

    // Client must send back the version it last read so EF Core can detect
    // a stale update (Part 8 — optimistic concurrency).
    [Required]
    public uint Version { get; set; }
}

public class BookingResponse
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public uint Version { get; set; }
    public string CheckInStatus { get; set; } = string.Empty;
    public Guid? RecurrenceGroupId { get; set; }
}

public class AvailabilityQuery
{
    [Required]
    public Guid ResourceId { get; set; }

    [Required]
    public DateTime From { get; set; }

    [Required]
    public DateTime To { get; set; }
}

public class RecurringBookingRequest
{
    [Required]
    public Guid ResourceId { get; set; }

    [Required]
    public DateTime FirstStartTime { get; set; }

    [Required]
    public DateTime FirstEndTime { get; set; }

    [Required]
    public RecurrenceFrequency Frequency { get; set; }

    [Range(1, 52)]
    public int Occurrences { get; set; } = 1;

    [MaxLength(500)]
    public string Purpose { get; set; } = string.Empty;

    // When true, occurrences that conflict are skipped and the rest are
    // still created. When false (default), a conflict aborts the whole
    // batch and the conflicting slots are returned for the user to review.
    public bool SkipConflicts { get; set; } = false;
}

public enum RecurrenceFrequency
{
    Daily,
    Weekly,
    Monthly
}

public class RecurringBookingResult
{
    public List<BookingResponse> Created { get; set; } = new();
    public List<ConflictingOccurrence> Conflicts { get; set; } = new();
}

public class ConflictingOccurrence
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}
