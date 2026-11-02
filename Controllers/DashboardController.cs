using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UTHMLibrarySpace.Data;
using UTHMLibrarySpace.Infrastructure;
using UTHMLibrarySpace.Models;
using UTHMLibrarySpace.Services;

namespace UTHMLibrarySpace.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly AppDbContext _db;
    private readonly BookingService _bookings;

    public DashboardController(AppDbContext db, BookingService bookings)
    {
        _db = db;
        _bookings = bookings;
    }

    public async Task<IActionResult> Index()
    {
        // Admins with no booking identity have no dashboard of their own — send them to the console.
        if (User.IsInRole(Roles.Admin) && User.FindFirstValue(UserClaims.BookingRole) is null)
            return RedirectToAction("Index", "Admin");

        var uid = User.CurrentUserId()!.Value;
        var user = await _db.Users.FindAsync(uid);
        if (user is null) return RedirectToAction("Login", "Account");
        var today = AppClock.Today;
        var mine = await _db.Bookings.Include(b => b.Facility).Where(b => b.UserId == uid).ToListAsync();
        var bookingRole = user.EffectiveBookingRole;
        var allowedTypes = FacilityRules.All.Where(kv => kv.Value.Roles.Contains(bookingRole)).Select(kv => kv.Key).ToList();

        var vm = new DashboardVm
        {
            User = user,
            Upcoming = mine.Where(b => b.IsActive && b.Date >= today).OrderBy(b => b.Date).ThenBy(b => b.StartTime).Take(5).ToList(),
            Notifications = await _db.Notifications.Where(n => n.UserId == uid).OrderByDescending(n => n.CreatedAt).Take(6).ToListAsync(),
            Bookable = await _bookings.LiveCardsAsync(_db.Facilities.Where(f => allowedTypes.Contains(f.Type)), bookingRole),
            TotalBookings = mine.Count,
            PendingCount = mine.Count(b => b.Status == BookingStatus.Pending),
            OutstandingFees = mine.Where(b => b.PaymentStatus is PaymentStatus.PayAtCounter or PaymentStatus.AwaitingProof && b.IsActive).Sum(b => b.Fee),
        };
        return View(vm);
    }
}

[Authorize]
public class NotificationsController : Controller
{
    private readonly AppDbContext _db;
    public NotificationsController(AppDbContext db) => _db = db;

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(string? returnUrl)
    {
        var uid = User.CurrentUserId()!.Value;
        await _db.Notifications.Where(n => n.UserId == uid && !n.IsRead).ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "Dashboard");
    }
}
 