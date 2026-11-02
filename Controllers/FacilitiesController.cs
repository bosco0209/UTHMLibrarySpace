using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UTHMLibrarySpace.Data;
using UTHMLibrarySpace.Infrastructure;
using UTHMLibrarySpace.Models;
using UTHMLibrarySpace.Services;

namespace UTHMLibrarySpace.Controllers;

public class FacilitiesController : Controller
{
    private readonly AppDbContext _db;
    private readonly BookingService _bookings;

    public FacilitiesController(AppDbContext db, BookingService bookings)
    {
        _db = db;
        _bookings = bookings;
    }

    // Booking permissions follow the booking identity, so an admin promoted from a lecturer
    // still sees and can book lecturer-only rooms.
    private string? CurrentRole => User.CurrentBookingRole();

    public async Task<IActionResult> Index(FacilityType? type, string? search, int? minCapacity)
    {
        var q = _db.Facilities.AsQueryable();
        if (type.HasValue) q = q.Where(f => f.Type == type);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(f => f.Name.ToLower().Contains(s)
                          || f.Code.ToLower().Contains(s)
                          || f.Amenities.ToLower().Contains(s)
                          || f.Location.ToLower().Contains(s));
        }
        if (minCapacity is > 0) q = q.Where(f => f.Capacity >= minCapacity);

        var vm = new FacilityIndexVm
        {
            Items = await _bookings.LiveCardsAsync(q, CurrentRole),
            Type = type,
            Search = search,
            MinCapacity = minCapacity,
            Role = CurrentRole,
            Counts = await _db.Facilities
                .GroupBy(f => f.Type)
                .Select(g => new { g.Key, C = g.Sum(f => f.Units) })
                .ToDictionaryAsync(x => x.Key, x => x.C),
        };
        return View(vm);
    }

    public async Task<IActionResult> Details(int id, DateOnly? date, string? month)
    {
        var facility = await _db.Facilities.FindAsync(id);
        if (facility is null) return NotFound();

        var today = AppClock.Today;
        var day = date is { } d && d >= today ? d : today;
        var thisMonth = new DateOnly(today.Year, today.Month, 1);

        var shownMonth = DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", out var m)
                         && m >= thisMonth
                         && m <= thisMonth.AddMonths(3)
            ? m
            : thisMonth;

        var role = CurrentRole;

        var block = facility.Status == FacilityStatus.Maintenance
            ? "This facility is currently under maintenance."
            : BookingService.RoleBlockReason(facility.Type, role);

        if (block is null && role is not null)
        {
            var uid = User.CurrentUserId();
            if (uid is int uidInt)
            {
                var keyOverdue = await _db.Bookings.Include(b => b.Facility)
                    .AnyAsync(b => b.UserId == uidInt
                                && b.Facility!.Type == FacilityType.BilikIqra
                                && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Confirmed)
                                && !b.KeyCollected
                                && b.EndDate < today);
                if (keyOverdue)
                    block = "You have an overdue Bilik Iqra' room key. Return it at the PTTA counter before booking again.";
            }
        }

        var vm = new FacilityDetailsVm
        {
            Facility = facility,
            Date = day,
            Today = today,
            DayBookings = await _bookings.DayBookingsAsync(id, day),
            IsAuthenticated = role is not null,
            CanBook = block is null,
            BlockReason = block,
            Input = new BookingInputVm { FacilityId = id, Date = day },
            IsRental = FacilityRules.IsMonthlyRental(facility.Type),
            Limit = await _bookings.LimitAsync(facility.Type),
            Month = shownMonth,
        };
        if (vm.IsRental)
            vm.Rentals = await _db.Bookings
                .Where(b => b.FacilityId == id
                         && b.EndDate >= today
                         && (b.Status == BookingStatus.Confirmed
                          || b.Status == BookingStatus.Pending
                          || b.Status == BookingStatus.Approved))
                .OrderBy(b => b.Date)
                .ToListAsync();

        if (TempData["BookingError"] is string err)
            ModelState.AddModelError(string.Empty, err);
        return View(vm);
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(BookingInputVm input)
    {
        var userId = User.CurrentUserId();
        var user = userId is null ? null : await _db.Users.FindAsync(userId.Value);
        if (user is null) return Challenge();

        if (!ModelState.IsValid)
        {
            TempData["BookingError"] = "Please fill in the date, times, purpose and attendees.";
            return RedirectToAction(nameof(Details), new
            {
                id = input.FacilityId,
                date = input.Date.ToString("yyyy-MM-dd")
            });
        }

        var (booking, error) = await _bookings.CreateAsync(user, input);
        if (error is not null)
        {
            TempData["BookingError"] = error;
            TempData["BookingDraft"] = System.Text.Json.JsonSerializer.Serialize(new
            {
                start = input.StartTime.ToString("HH:mm"),
                end = input.EndTime.ToString("HH:mm"),
                input.Purpose,
                input.Attendees,
            });
            return RedirectToAction(nameof(Details), new
            {
                id = input.FacilityId,
                date = input.Date.ToString("yyyy-MM-dd")
            });
        }

        if (booking!.AwaitingProof)
        {
            this.Toast($"{booking.Reference}: {booking.Facility!.Name} is on hold for you. Upload your proof of payment to send it for approval.", "info");
            return RedirectToAction("Proof", "Bookings", new { id = booking.Id });
        }

        this.Toast(booking.Status == BookingStatus.Confirmed
            ? $"Booked! {booking.Reference} is confirmed."
            : $"Request {booking.Reference} submitted — awaiting admin approval.");

        return RedirectToAction("Index", "Bookings", new { highlight = booking.Reference });
    }
}