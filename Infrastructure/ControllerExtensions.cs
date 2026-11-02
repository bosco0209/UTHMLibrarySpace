using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace UTHMLibrarySpace.Infrastructure;

/// <summary>Custom claim types used across the app.</summary>
public static class UserClaims
{
    /// <summary>
    /// Booking identity of the signed-in user. Present only for admins who were promoted from a
    /// student/lecturer account, so they can keep booking rooms with that role's permissions.
    /// </summary>
    public const string BookingRole = "BookingRole";

    public const string IdNumber = "IdNumber";
    public const string Avatar = "Avatar";
}

public static class ControllerExtensions
{
    public static int? CurrentUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>
    /// The role to use when checking booking permissions: the preserved booking identity for
    /// promoted admins, otherwise the account role. Null for anonymous visitors.
    /// </summary>
    public static string? CurrentBookingRole(this ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
            ? user.FindFirstValue(UserClaims.BookingRole) ?? user.FindFirstValue(ClaimTypes.Role)
            : null;

    public static void Toast(this Controller c, string message, string kind = "success")
    {
        c.TempData["ToastMessage"] = message;
        c.TempData["ToastKind"] = kind;
    }
}
