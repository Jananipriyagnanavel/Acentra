namespace BookingApi.Exceptions;

/// <summary>Base type for exceptions that carry a stable error code and HTTP status.</summary>
public abstract class ApiException : Exception
{
    public abstract int StatusCode { get; }
    public abstract string ErrorCode { get; }

    protected ApiException(string message) : base(message)
    {
    }
}

public class BookingConflictException : ApiException
{
    public override int StatusCode => StatusCodes.Status409Conflict;
    public override string ErrorCode => "BOOKING_CONFLICT";

    public BookingConflictException(string message = "This resource is no longer available for the selected time.")
        : base(message)
    {
    }
}

public class ConcurrencyConflictException : ApiException
{
    public override int StatusCode => StatusCodes.Status409Conflict;
    public override string ErrorCode => "CONCURRENCY_CONFLICT";

    public ConcurrencyConflictException(string message = "This booking was modified by another user.")
        : base(message)
    {
    }
}

public class NotFoundException : ApiException
{
    public override int StatusCode => StatusCodes.Status404NotFound;
    public override string ErrorCode => "NOT_FOUND";

    public NotFoundException(string message) : base(message)
    {
    }
}

public class ForbiddenException : ApiException
{
    public override int StatusCode => StatusCodes.Status403Forbidden;
    public override string ErrorCode => "FORBIDDEN";

    public ForbiddenException(string message = "You do not have permission to perform this action.")
        : base(message)
    {
    }
}

public class ValidationException : ApiException
{
    public override int StatusCode => StatusCodes.Status400BadRequest;
    public override string ErrorCode => "VALIDATION_ERROR";

    public ValidationException(string message) : base(message)
    {
    }
}
