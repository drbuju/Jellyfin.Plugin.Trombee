using System;
using System.Security.Claims;

namespace Jellyfin.Plugin.Trombee.Api;

/// <summary>
/// Extension methods for resolving the authenticated user from claims.
/// </summary>
internal static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns the authenticated user's ID from the "Jellyfin-UserId" claim, or
    /// <see cref="Guid.Empty"/> if not present/parseable.
    /// </summary>
    /// <param name="user">The claims principal for the current request.</param>
    /// <returns>The user's ID, or <see cref="Guid.Empty"/>.</returns>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst("Jellyfin-UserId");
        if (userIdClaim is not null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }

        return Guid.Empty;
    }
}
