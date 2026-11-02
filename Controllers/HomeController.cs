using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UTHMLibrarySpace.Data;
using UTHMLibrarySpace.Models;
using UTHMLibrarySpace.Services;

namespace UTHMLibrarySpace.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    private readonly BookingService _bookings;

    public HomeController(AppDbContext db, BookingService bookings)
    {
        _db = db;
        _bookings = bookings;
    }

    public async Task<IActionResult> Index()
    {
        var cards = await _bookings.LiveCardsAsync(_db.Facilities, null);
        ViewBag.TotalFacilities = cards.Count;
        ViewBag.AvailableNow = cards.Count(c => c.Facility.Status == FacilityStatus.Available && !c.InUseNow);
        ViewBag.BookingsToday = cards.Sum(c => c.BookingsToday);
        ViewBag.TotalBookings = await _db.Bookings.CountAsync();
        ViewBag.Counts = cards.GroupBy(c => c.Facility.Type).ToDictionary(g => g.Key, g => g.Count());
        ViewBag.Images = cards.GroupBy(c => c.Facility.Type).ToDictionary(g => g.Key, g => g.First().Facility.ImageUrl);
        return View(cards.Where(c => c.Facility.Status == FacilityStatus.Available).Take(6).ToList());
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(model: Activity.Current?.Id ?? HttpContext.TraceIdentifier);
}
