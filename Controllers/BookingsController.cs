using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UTHMLibrarySpace.Data;
using UTHMLibrarySpace.Infrastructure;
using UTHMLibrarySpace.Models;
using UTHMLibrarySpace.Services;

namespace UTHMLibrarySpace.Controllers;

// Admins promoted from a student/lecturer account are included — they book as their original role.
[Authorize(Policy = AuthPolicies.CanBook)]
public class BookingsController : Controller
{
    private readonly AppDbContext _db;
    private readonly BookingService _bookings;

    public BookingsController(AppDbContext db, BookingService bookings)
    {
        _db = db;
        _bookings = bookings;
    }

    public async Task<IActionResult> Index(string tab = "all", string? highlight = null)
    {
        var uid = User.CurrentUserId()!.Value;
        await _bookings.SweepExpiredRentalsAsync();

        var all = await _db.Bookings
            .Include(b => b.Facility)
            .Where(b => b.UserId == uid)
            .OrderByDescending(b => b.Date)
            .ThenByDescending(b => b.StartTime)
            .ToListAsync();

        var today = AppClock.Today;
        var list = tab switch
        {
            "upcoming" => all.Where(b => b.IsActive && b.Date >= today && b.Status != BookingStatus.Pending).ToList(),
            "pending" => all.Where(b => b.Status == BookingStatus.Pending).ToList(),
            "past" => all.Where(b => b.Date < today && b.Status is BookingStatus.Confirmed or BookingStatus.Approved).ToList(),
            "closed" => all.Where(b => b.Status is BookingStatus.Cancelled or BookingStatus.Rejected).ToList(),
            _ => all,
        };

        ViewBag.Highlight = highlight;
        ViewBag.Counts = new Dictionary<string, int>
        {
            ["all"] = all.Count,
            ["upcoming"] = all.Count(b => b.IsActive && b.Date >= today && b.Status != BookingStatus.Pending),
            ["pending"] = all.Count(b => b.Status == BookingStatus.Pending),
            ["past"] = all.Count(b => b.Date < today && b.Status is BookingStatus.Confirmed or BookingStatus.Approved),
            ["closed"] = all.Count(b => b.Status is BookingStatus.Cancelled or BookingStatus.Rejected),
        };

        return View(new MyBookingsVm { Bookings = list, Tab = tab, Today = today });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var user = await _db.Users.FindAsync(User.CurrentUserId()!.Value);
        var error = await _bookings.CancelAsync(id, user!);
        this.Toast(error ?? "Booking cancelled.", error is null ? "success" : "danger");
        return RedirectToAction(nameof(Index));
    }

    // ---------- Proof upload page (GET) ----------
    public async Task<IActionResult> Proof(int id)
    {
        var uid = User.CurrentUserId()!.Value;
        var b = await _db.Bookings
            .Include(x => x.Facility)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid);
        if (b is null) return NotFound();

        if (TempData["ProofError"] is string err)
            ModelState.AddModelError(string.Empty, err);

        return View(b);
    }

    // ---------- Proof upload (POST) ----------
    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Proof(int id, IFormFile? proof)
    {
        var user = await _db.Users.FindAsync(User.CurrentUserId()!.Value);
        var error = await _bookings.SubmitProofAsync(id, user!, proof);
        if (error is not null)
        {
            TempData["ProofError"] = error;
            return RedirectToAction(nameof(Proof), new { id });
        }

        var b = await _db.Bookings.FindAsync(id);
        this.Toast($"Proof uploaded. {b!.Reference} is now waiting for admin approval.");
        return RedirectToAction(nameof(Index), new { tab = "pending", highlight = b.Reference });
    }

    // ---------- View proof file ----------
    // Single action. The class-level [Authorize] allows only students/lecturers,
    // so this action serves the file only for its owner.
    public async Task<IActionResult> ProofFile(int id)
    {
        var uid = User.CurrentUserId()!.Value;
        var b = await _db.Bookings.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid);
        var path = b is null ? null : _bookings.ProofPath(b);
        if (path is null || !System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, b!.ProofContentType ?? "application/octet-stream");
    }
}