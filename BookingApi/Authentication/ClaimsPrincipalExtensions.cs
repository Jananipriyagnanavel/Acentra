using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BookingApi.Models;

namespace BookingApi.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                    ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (value == null || !Guid.TryParse(value, out var id))
        {
            throw new InvalidOperationException("User id claim missing or invalid.");
        }

        return id;
    }

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin);
}
