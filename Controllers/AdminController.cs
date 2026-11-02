using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UTHMLibrarySpace.Data;
using UTHMLibrarySpace.Infrastructure;
using UTHMLibrarySpace.Models;
using UTHMLibrarySpace.Services;

namespace UTHMLibrarySpace.Controllers;

[Authorize(Roles = Roles.Admin)]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly BookingService _bookings;
    private readonly PdfReportService _pdf;
    private readonly EmailService _email;
    private readonly IWebHostEnvironment _env;

    public AdminController(
        AppDbContext db,
        BookingService bookings,
        PdfReportService pdf,
        EmailService email,
        IWebHostEnvironment env)
    {
        _db = db;
        _bookings = bookings;
        _pdf = pdf;
        _email = email;
        _env = env;
    }

    // ---------------------------------------------------------------
    // Dashboard
    // ---------------------------------------------------------------
    public async Task<IActionResult> Index()
    {
        await _bookings.SweepExpiredRentalsAsync();

        var all = await _db.Bookings.Include(b => b.Facility).Include(b => b.User).ToListAsync();
        var allFacilities = await _db.Facilities.ToListAsync();
        var today = AppClock.Today;
        var live = all.Where(b => b.Status is BookingStatus.Confirmed or BookingStatus.Approved).ToList();
        var days = Enumerable.Range(0, 7).Select(i => today.AddDays(i - 3)).ToList();

        // Rooms per facility type (used by the doughnut chart)
        var roomCounts = FacilityRules.All.Keys
            .Select(t => allFacilities.Count(f => f.Type == t))
            .ToList();

        var vm = new AdminDashboardVm
        {
            TotalBookings = all.Count,
            Pending = all.Count(b => b.Status == BookingStatus.Pending),
            TodayBookings = all.Count(b => b.Date == today && b.IsActive),
            Revenue = all.Where(b => b.PaymentStatus == PaymentStatus.Paid).Sum(b => b.Fee),
            Outstanding = all
                .Where(b => b.IsActive && b.PaymentStatus is
                       PaymentStatus.PayAtCounter or PaymentStatus.AwaitingProof or PaymentStatus.ProofSubmitted)
                .Sum(b => b.Fee),
            TotalUsers = await _db.Users.CountAsync(),
            TotalFacilities = allFacilities.Count,
            Maintenance = allFacilities.Count(f => f.Status == FacilityStatus.Maintenance),

            DayLabels = days.Select(d => d == today ? "Today" : d.ToString("ddd d")).ToList(),
            DayCounts = days.Select(d => all.Count(b => b.Date == d && b.Status != BookingStatus.Cancelled)).ToList(),

            // Bookings per type — kept for the left bar chart
            TypeLabels = FacilityRules.All.Values.Select(r => r.Plural).ToList(),
            TypeCounts = FacilityRules.All.Keys.Select(t =>
                all.Count(b => b.Facility!.Type == t && b.Status != BookingStatus.Cancelled)).ToList(),

            // Rooms per type — used by the doughnut chart
            RoomLabels = FacilityRules.All.Values.Select(r => r.Plural).ToList(),
            RoomCounts = roomCounts,

            TypeThisWeekLabels = FacilityRules.All.Values.Select(r => r.Plural).ToList(),
            TypeThisWeekCounts = FacilityRules.All.Keys.Select(t =>
                days.Sum(d => all.Count(b => b.Facility!.Type == t && b.Date == d && b.Status != BookingStatus.Cancelled))).ToList(),

            HourLabels = Enumerable.Range(8, 14).Select(h => $"{h:00}:00").ToList(),
            HourCounts = Enumerable.Range(8, 14).Select(h =>
                live.Count(b => b.StartTime.Hour <= h && b.EndTime.Hour > h)).ToList(),

            PendingQueue = all.Where(b => b.Status == BookingStatus.Pending)
                              .OrderBy(b => b.Date).ThenBy(b => b.StartTime).Take(6).ToList(),
            TodaySchedule = all.Where(b => b.Date == today && b.IsActive)
                               .OrderBy(b => b.StartTime).ToList(),
        };
        return View(vm);
    }

    // ---------------------------------------------------------------
    // Bookings & approvals
    // ---------------------------------------------------------------
    private IQueryable<Booking> FilteredBookings(BookingStatus? status, FacilityType? type, string? search)
    {
        var q = _db.Bookings.Include(b => b.Facility).Include(b => b.User).AsQueryable();
        if (status.HasValue) q = q.Where(b => b.Status == status);
        if (type.HasValue) q = q.Where(b => b.Facility!.Type == type);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(b => b.Reference.ToLower().Contains(s) ||
                             b.User!.Name.ToLower().Contains(s) ||
                             b.User!.IdNumber.ToLower().Contains(s) ||
                             b.Facility!.Name.ToLower().Contains(s));
        }
        return q;
    }

    public async Task<IActionResult> Bookings(BookingStatus? status, FacilityType? type, string? search)
    {
        var list = await FilteredBookings(status, type, search).ToListAsync();
        list = list
            .OrderBy(b => b.Status == BookingStatus.Pending ? 0 : 1)
            .ThenByDescending(b => b.Date)
            .ThenBy(b => b.StartTime)
            .ToList();
        var today = AppClock.Today;
        var keyReturns = await _db.Bookings
            .Include(b => b.Facility).Include(b => b.User)
            .Where(b => b.Facility!.Type == FacilityType.BilikIqra
                     && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Confirmed)
                     && b.EndDate < today
                     && !b.KeyCollected)
            .OrderByDescending(b => b.EndDate)
            .ToListAsync();

        return View(new AdminBookingsVm
        {
            Bookings = list,
            Status = status,
            Type = type,
            Search = search,
            KeyReturns = keyReturns
        });
    }

    // Bilik Iqra' key returns — ended rentals that still need the room key back
    public async Task<IActionResult> KeyReturns()
    {
        var today = AppClock.Today;
        var ended = await _db.Bookings
            .Include(b => b.Facility).Include(b => b.User)
            .Where(b => b.Facility!.Type == FacilityType.BilikIqra
                     && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Confirmed)
                     && b.EndDate < today
                     && !b.KeyCollected)
            .OrderByDescending(b => b.EndDate)
            .ToListAsync();
        return View(ended);
    }

    public async Task<IActionResult> ExportKeyReturnsPdf()
    {
        var today = AppClock.Today;
        var list = await _db.Bookings
            .Include(b => b.Facility).Include(b => b.User)
            .Where(b => b.Facility!.Type == FacilityType.BilikIqra
                     && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Confirmed)
                     && !b.KeyCollected
                     && b.EndDate < today)
            .OrderBy(b => b.EndDate)
            .ToListAsync();
        var bytes = _pdf.KeyReturnReport(list);
        return File(bytes, "application/pdf", $"PTTA-BilikIqra-KeyReturns-{AppClock.Today:yyyyMMdd}.pdf");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CollectKey(int id, string? returnUrl)
    {
        var b = await _db.Bookings.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id);
        if (b is null) return NotFound();
        b.KeyCollected = true;
        await _db.SaveChangesAsync();
        this.Toast($"Room key collected from {b.User!.Name}.");
        return Redirect(returnUrl ?? Url.Action(nameof(KeyReturns))!);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? note, string? returnUrl)
    {
        var err = await _bookings.DecideAsync(id, true, note);
        this.Toast(err ?? "Booking approved and user notified.", err is null ? "success" : "danger");
        return Back(returnUrl);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? note, string? returnUrl)
    {
        var err = await _bookings.DecideAsync(id, false, note);
        this.Toast(err ?? "Booking rejected and user notified.", err is null ? "warning" : "danger");
        return Back(returnUrl);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPaid(int id, string? returnUrl)
    {
        var b = await _db.Bookings.FindAsync(id);
        if (b is null || b.PaymentStatus is not (PaymentStatus.PayAtCounter or PaymentStatus.ProofSubmitted))
        {
            this.Toast("No outstanding payment for this booking.", "danger");
        }
        else
        {
            b.PaymentStatus = PaymentStatus.Paid;
            _bookings.Notify(b.UserId, "Payment received",
                $"RM{b.Fee:0.00} received at the PTTA counter for booking {b.Reference}. Thank you!");
            await _db.SaveChangesAsync();
            this.Toast($"{b.Reference} marked as paid.");
        }
        return Back(returnUrl);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelBooking(int id, string? returnUrl)
    {
        var currentId = User.CurrentUserId();
        if (currentId is null) return Challenge();
        var admin = await _db.Users.FindAsync(currentId.Value);
        var err = await _bookings.CancelAsync(id, admin!);
        this.Toast(err ?? "Booking cancelled.", err is null ? "info" : "danger");
        return Back(returnUrl);
    }

    public async Task<IActionResult> ExportPdf(BookingStatus? status, FacilityType? type, string? search)
    {
        var list = await FilteredBookings(status, type, search).ToListAsync();
        list = list.OrderByDescending(b => b.Date).ThenBy(b => b.StartTime).ToList();

        var filter = string.Join(", ", new[]
        {
            status?.ToString(),
            type.HasValue ? FacilityRules.For(type.Value).Plural : null,
            string.IsNullOrWhiteSpace(search) ? null : $"\"{search}\"",
        }.Where(x => x != null));

        var bytes = _pdf.BookingReport(list, string.IsNullOrEmpty(filter) ? "All bookings" : filter);
        return File(bytes, "application/pdf", $"PTTA-Booking-Report-{AppClock.Today:yyyyMMdd}.pdf");
    }

    // View uploaded proof of payment
    [HttpGet]
    public async Task<IActionResult> ProofFile(int id)
    {
        var b = await _db.Bookings.FindAsync(id);
        if (b is null || !b.HasProof)
            return NotFound();

        var path = _bookings.ProofPath(b);
        if (path is null || !System.IO.File.Exists(path))
            return NotFound();

        var contentType = b.ProofContentType ?? "application/octet-stream";
        return PhysicalFile(path, contentType, enableRangeProcessing: true);
    }

    // ---------------------------------------------------------------
    // Walk-in booking (created by admin on behalf of a user)
    // ---------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> WalkIn(int? facilityId)
    {
        var vm = new AdminWalkInVm
        {
            FacilityId = facilityId,
            Date = AppClock.Today,
            Users = await _db.Users
                .Where(u => u.IsActive && u.Role != Roles.Admin)
                .OrderBy(u => u.Role).ThenBy(u => u.Name)
                .ToListAsync(),
            Facilities = await _db.Facilities
                .Where(f => f.Status == FacilityStatus.Available)
                .OrderBy(f => f.Type).ThenBy(f => f.Code)
                .ToListAsync(),
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> WalkIn(AdminWalkInVm vm)
    {
        // Reload reference data for re-render on error
        vm.Users = await _db.Users
            .Where(u => u.IsActive && u.Role != Roles.Admin)
            .OrderBy(u => u.Role).ThenBy(u => u.Name)
            .ToListAsync();
        vm.Facilities = await _db.Facilities
            .Where(f => f.Status == FacilityStatus.Available)
            .OrderBy(f => f.Type).ThenBy(f => f.Code)
            .ToListAsync();

        if (vm.FacilityId is null)
        {
            ModelState.AddModelError(nameof(vm.FacilityId), "Please choose a facility.");
            return View(vm);
        }

        var onBehalfOf = await _db.Users.FindAsync(vm.UserId);
        if (onBehalfOf is null || onBehalfOf.Role == Roles.Admin)
        {
            ModelState.AddModelError(nameof(vm.UserId), "Please choose a valid student or lecturer.");
            return View(vm);
        }

        var currentUserId = User.CurrentUserId();
        if (currentUserId is null) return Challenge();
        var admin = await _db.Users.FindAsync(currentUserId.Value);
        if (admin is null) return Challenge();

        var input = new BookingInputVm
        {
            FacilityId = vm.FacilityId.Value,
            Date = vm.Date,
            StartTime = vm.StartTime,
            EndTime = vm.EndTime,
            Purpose = vm.Purpose,
            Attendees = vm.Attendees,
        };

        var (booking, error) = await _bookings.CreateOnBehalfAsync(admin, onBehalfOf, input, markPaid: vm.MarkPaid);
        if (error is not null)
        {
            ModelState.AddModelError(string.Empty, error);
            return View(vm);
        }

        this.Toast($"Walk-in booking {booking!.Reference} created for {onBehalfOf.Name}.");
        return RedirectToAction(nameof(Bookings), new { search = booking.Reference });
    }

    // ---------------------------------------------------------------
    // Facilities
    // ---------------------------------------------------------------
    public async Task<IActionResult> Facilities(FacilityType? type)
    {
        var all = await _db.Facilities.OrderBy(f => f.Type).ThenBy(f => f.Code).ToListAsync();

        ViewBag.Type = type;
        ViewBag.Counts = all.GroupBy(f => f.Type).ToDictionary(g => g.Key, g => g.Count());
        ViewBag.Usage = await _db.Bookings
            .Where(b => b.Status != BookingStatus.Cancelled)
            .GroupBy(b => b.FacilityId)
            .Select(g => new { g.Key, C = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.C);

        return View(type is null ? all : all.Where(f => f.Type == type).ToList());
    }

    public async Task<IActionResult> FacilityForm(int? id, FacilityType? type)
    {
        if (id is null) return View(new FacilityFormVm { Type = type ?? FacilityType.BilikIqra });

        var f = await _db.Facilities.FindAsync(id);
        if (f is null) return NotFound();

        return View(new FacilityFormVm
        {
            Id = f.Id,
            Name = f.Name,
            Code = f.Code,
            Type = f.Type,
            Capacity = f.Capacity,
            CapacityMin = f.CapacityMin,
            Units = f.Units,
            Location = f.Location,
            Description = f.Description,
            Amenities = f.Amenities,
            ImageUrl = f.ImageUrl,
            Status = f.Status,
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> FacilityForm(FacilityFormVm vm)
    {
        vm.Code = (vm.Code ?? "").Trim().ToUpper();
        vm.Name = (vm.Name ?? "").Trim();
        vm.Location = (vm.Location ?? "").Trim();
        vm.Description = (vm.Description ?? "").Trim();
        vm.Amenities = (vm.Amenities ?? "").Trim();

        if (!FacilityRules.ShowsImage(vm.Type))
        {
            vm.ImageUrl = null;
            vm.ImageFile = null;
        }
        else if (vm.ImageFile is { Length: > 0 })
        {
            var uploadErr = await SaveFacilityImageAsync(vm, vm.ImageFile);
            if (uploadErr is not null)
                ModelState.AddModelError(nameof(vm.ImageFile), uploadErr);
        }

        if (await _db.Facilities.AnyAsync(f => f.Code == vm.Code && f.Id != vm.Id))
            ModelState.AddModelError(nameof(vm.Code), "This facility code is already used.");

        if (!ModelState.IsValid) return View(vm);

        var f = vm.Id is null ? new Facility() : await _db.Facilities.FindAsync(vm.Id);
        if (f is null) return NotFound();

        f.Name = vm.Name;
        f.Code = vm.Code;
        f.Type = vm.Type;
        f.Capacity = vm.Capacity;
        f.CapacityMin = vm.CapacityMin is > 0 && vm.CapacityMin < vm.Capacity ? vm.CapacityMin : null;
        f.Units = vm.Units < 1 ? 1 : vm.Units;
        f.Location = vm.Location;
        f.Description = vm.Description;
        f.Amenities = vm.Amenities;
        f.Status = vm.Status;

        if (FacilityRules.ShowsImage(vm.Type))
            f.ImageUrl = vm.ImageUrl ?? "";

        if (vm.Id is null) _db.Facilities.Add(f);
        await _db.SaveChangesAsync();

        this.Toast(vm.Id is null ? $"{f.Name} added." : $"{f.Name} updated.");
        return RedirectToAction(nameof(Facilities), new { type = f.Type });
    }

    // ---------------------------------------------------------------
    // Facility image upload helper
    // ---------------------------------------------------------------
    private static readonly string[] AllowedImageExts = { ".jpg", ".jpeg", ".png", ".webp" };
    private const int MaxImageBytes = 3 * 1024 * 1024;

    private async Task<string?> SaveFacilityImageAsync(FacilityFormVm vm, IFormFile file)
    {
        if (file.Length > MaxImageBytes)
            return "Image is too large (max 3 MB).";

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExts.Contains(ext))
            return "Only JPG, PNG or WEBP images are allowed.";

        var head = new byte[8];
        await using (var s = file.OpenReadStream()) _ = await s.ReadAsync(head);
        bool isImage =
            (head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF) ||
            (head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E) ||
            (head[0] == 0x52 && head[1] == 0x49 && head[2] == 0x46);
        if (!isImage)
            return "That file doesn't look like a real image.";

        var dir = Path.Combine(_env.WebRootPath, "uploads", "facilities");
        Directory.CreateDirectory(dir);

        if (!string.IsNullOrEmpty(vm.ImageUrl) && vm.ImageUrl.StartsWith("/uploads/facilities/"))
        {
            var oldPath = Path.Combine(_env.WebRootPath,
                vm.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(oldPath))
                System.IO.File.Delete(oldPath);
        }

        var name = $"fac-{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(dir, name);
        await using (var outStream = System.IO.File.Create(path))
            await file.CopyToAsync(outStream);

        vm.ImageUrl = $"/uploads/facilities/{name}";
        return null;
    }

    // ---------------------------------------------------------------
    // Toggle / delete / limits
    // ---------------------------------------------------------------
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleMaintenance(int id)
    {
        var f = await _db.Facilities.FindAsync(id);
        if (f is null) return NotFound();

        f.Status = f.Status == FacilityStatus.Available
            ? FacilityStatus.Maintenance
            : FacilityStatus.Available;
        await _db.SaveChangesAsync();

        this.Toast(f.Status == FacilityStatus.Maintenance
            ? $"{f.Name} set to maintenance."
            : $"{f.Name} is available again.", "info");

        return RedirectToAction(nameof(Facilities), new { type = f.Type });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFacility(int id)
    {
        var f = await _db.Facilities.FindAsync(id);
        if (f is null) return NotFound();

        if (await _db.Bookings.AnyAsync(b => b.FacilityId == id))
        {
            this.Toast("This facility has booking history — set it to maintenance instead of deleting.", "danger");
            return RedirectToAction(nameof(Facilities), new { type = f.Type });
        }

        _db.Facilities.Remove(f);
        await _db.SaveChangesAsync();
        this.Toast($"{f.Name} deleted.", "info");
        return RedirectToAction(nameof(Facilities), new { type = f.Type });
    }

    // ---------------------------------------------------------------
    // Booking rules (category limits)
    // ---------------------------------------------------------------
    public async Task<IActionResult> Limits()
    {
        var limits = await _db.CategoryLimits.ToListAsync();
        foreach (var t in FacilityRules.All.Keys.Where(t => limits.All(l => l.Type != t)))
            limits.Add(new CategoryLimit { Type = t });
        return View(new AdminLimitsVm { Limits = limits.OrderBy(l => l.Type).ToList() });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLimit(FacilityType type, double? maxHoursPerBooking, double? maxHoursPerWeek, int? maxActiveBookings)
    {
        if (maxHoursPerBooking is <= 0 or > 14 || maxHoursPerWeek is <= 0 or > 98 || maxActiveBookings is <= 0 or > 50)
        {
            this.Toast("Limits must be positive (max 14h per booking, 98h per week, 50 active bookings). Leave a field empty for no limit.", "danger");
            return RedirectToAction(nameof(Limits));
        }
        if (maxHoursPerBooking is double p && maxHoursPerWeek is double w && w < p)
        {
            this.Toast("Weekly hours cannot be lower than hours per booking.", "danger");
            return RedirectToAction(nameof(Limits));
        }
        var limit = await _db.CategoryLimits.FirstOrDefaultAsync(l => l.Type == type);
        if (limit is null) { limit = new CategoryLimit { Type = type }; _db.CategoryLimits.Add(limit); }
        var rental = FacilityRules.IsMonthlyRental(type);
        limit.MaxHoursPerBooking = rental ? null : maxHoursPerBooking;
        limit.MaxHoursPerWeek = rental ? null : maxHoursPerWeek;
        limit.MaxActiveBookings = maxActiveBookings;
        await _db.SaveChangesAsync();
        this.Toast($"{FacilityRules.For(type).Plural} limits saved.");
        return RedirectToAction(nameof(Limits));
    }

    // ---------------------------------------------------------------
    // Users
    // ---------------------------------------------------------------
    public async Task<IActionResult> Users(string? role, string? search)
    {
        var q = _db.Users.AsQueryable();
        if (!string.IsNullOrEmpty(role)) q = q.Where(u => u.Role == role);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(u => u.Name.ToLower().Contains(s) ||
                             u.Email.Contains(s) ||
                             u.IdNumber.ToLower().Contains(s));
        }

        ViewBag.Role = role;
        ViewBag.Search = search;
        ViewBag.BookingCounts = await _db.Bookings
            .GroupBy(b => b.UserId)
            .Select(g => new { g.Key, C = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.C);

        return View(await q.OrderBy(u => u.Role).ThenBy(u => u.Name).ToListAsync());
    }

    /// <summary>
    /// Grants admin rights using only an email address.
    /// If the address already belongs to a registered user (student / lecturer) that account is promoted
    /// and keeps its current password. Otherwise a brand-new admin account is created and a temporary
    /// password is emailed to the address so the new admin can sign in and set their own password.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAdmin(string? email, string? returnUrl)
    {
        var addr = (email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(addr) || !addr.Contains('@') || addr.Contains(' '))
        {
            this.Toast("Enter a valid email address.", "danger");
            return Back(returnUrl);
        }

        var existing = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == addr);

        if (existing is not null)
        {
            if (existing.Role == Roles.Admin)
            {
                this.Toast($"{existing.Name} is already an admin.", "info");
                return Back(returnUrl);
            }

            var wasRole = existing.Role;
            existing.Role = Roles.Admin;
            // Keep the booking identity so a promoted student/lecturer can still book rooms.
            existing.BookingRole = wasRole;
            existing.IsActive = true;
            if (string.IsNullOrWhiteSpace(existing.Faculty))
                existing.Faculty = "Perpustakaan Tunku Tun Aminah";
            await _db.SaveChangesAsync();

            _bookings.Notify(existing.Id, "You are now a PTTA admin",
                $"{existing.Name}, an administrator granted you Admin Console access. Sign in and use the Admin Console to manage bookings, users and rooms.");

            try
            {
                await _email.SendAdminGrantedAsync(existing.Email, existing.Name, wasRole);
            }
            catch (Exception ex)
            {
                // Email is best-effort — never fail the promotion because of SMTP.
                Console.WriteLine($"[AddAdmin] email failed for {existing.Email}: {ex.Message}");
            }

            this.Toast($"{existing.Name} ({addr}) is now an admin.", "success");
            return Back(returnUrl);
        }

        // Unknown address → create the admin account with a temporary password.
        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
        var tempPassword = TempPassword();

        var admin = new User
        {
            Name = NameFromEmail(addr),
            Email = addr,
            IdNumber = await NextAdminIdAsync(),
            Role = Roles.Admin,
            Faculty = "Perpustakaan Tunku Tun Aminah",
            Phone = "",
            IsActive = true,
        };
        admin.PasswordHash = hasher.HashPassword(admin, tempPassword);

        _db.Users.Add(admin);
        await _db.SaveChangesAsync();

        _bookings.Notify(admin.Id, "Welcome to the PTTA admin team",
            $"An administrator created this admin account for {addr}. Sign in with the temporary password we emailed you, then use Forgot password to set your own.");

        try
        {
            await _email.SendAdminWelcomeAsync(admin.Email, admin.Name, admin.IdNumber, tempPassword);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AddAdmin] email failed for {admin.Email}: {ex.Message}");
        }

        this.Toast($"Admin account created for {addr}. Temporary password: {tempPassword}", "success");
        return Back(returnUrl);
    }

    /// <summary>Short, human-readable display name derived from the email's local part.</summary>
    private static string NameFromEmail(string email)
    {
        var local = email.Split('@')[0];
        var parts = local.Split(new[] { '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        var text = string.Join(" ", parts.Select(p =>
            char.ToUpperInvariant(p[0]) + p[1..]));

        if (string.IsNullOrWhiteSpace(text)) text = "PTTA Admin";
        if (text.Any(char.IsDigit)) text = $"PTTA {text}";
        return text;
    }

    /// <summary>Unique staff ID for a new admin, e.g. ADM007.</summary>
    private async Task<string> NextAdminIdAsync()
    {
        var used = await _db.Users
            .Where(u => u.Role == Roles.Admin)
            .Select(u => u.IdNumber)
            .ToListAsync();

        for (var n = used.Count + 1; n < 1000; n++)
        {
            var candidate = $"ADM{n:000}";
            if (!used.Contains(candidate, StringComparer.OrdinalIgnoreCase)) return candidate;
        }
        return $"ADM{DateTime.UtcNow:yyyyMMddHHmm}";
    }

    /// <summary>
    /// Readable one-time password (no 0/O/1/l/I ambiguity) that still satisfies the complexity rules.
    /// </summary>
    private static string TempPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digit = "23456789";
        const string symbol = "!@#$%&*";
        var chars = new List<char>
        {
            upper[Random.Shared.Next(upper.Length)],
            lower[Random.Shared.Next(lower.Length)],
            digit[Random.Shared.Next(digit.Length)],
            symbol[Random.Shared.Next(symbol.Length)],
        };
        var all = upper + lower + digit + symbol;
        chars.AddRange(Enumerable.Range(0, 8).Select(_ => all[Random.Shared.Next(all.Length)]));
        // Fisher–Yates so the guaranteed characters are not always in the same positions.
        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars.ToArray());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUser(int id, string? returnUrl)
    {
        if (id == User.CurrentUserId())
        {
            this.Toast("You cannot disable your own account.", "danger");
            return RedirectToAction(nameof(Users));
        }
        var u = await _db.Users.FindAsync(id);
        if (u is null) return NotFound();

        u.IsActive = !u.IsActive;
        await _db.SaveChangesAsync();
        this.Toast($"{u.Name} {(u.IsActive ? "re-activated" : "disabled")}.", "info");
        return Redirect(returnUrl ?? Url.Action(nameof(Users))!);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int id, bool force = false)
    {
        if (id == User.CurrentUserId())
        {
            this.Toast("You cannot delete your own account.", "danger");
            return RedirectToAction(nameof(Users));
        }

        var u = await _db.Users
            .Include(x => x.Bookings)
            .Include(x => x.Notifications)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NotFound();

        var bookingCount = u.Bookings.Count;

        if (bookingCount > 0 && !force)
        {
            TempData["ToastMessage"] =
                $"{u.Name} has {bookingCount} booking(s). Click Delete again and confirm to remove them permanently.";
            TempData["ToastKind"] = "warning";
            return RedirectToAction(nameof(Users));
        }

        foreach (var b in u.Bookings)
        {
            if (b.HasProof && !string.IsNullOrEmpty(b.ProofFileName))
            {
                var proofPath = Path.Combine(_env.ContentRootPath, "App_Data", "proofs", b.ProofFileName);
                if (System.IO.File.Exists(proofPath))
                    System.IO.File.Delete(proofPath);
            }
        }
        _db.Bookings.RemoveRange(u.Bookings);
        _db.Notifications.RemoveRange(u.Notifications);

        if (!string.IsNullOrEmpty(u.AvatarFileName))
        {
            var avatarPath = Path.Combine(_env.ContentRootPath, "App_Data", "avatars", u.AvatarFileName);
            if (System.IO.File.Exists(avatarPath))
                System.IO.File.Delete(avatarPath);
        }

        var pending = await _db.PendingRegistrations
            .Where(p => p.Email == u.Email)
            .ToListAsync();
        if (pending.Count > 0)
            _db.PendingRegistrations.RemoveRange(pending);

        _db.Users.Remove(u);
        await _db.SaveChangesAsync();

        this.Toast(
            bookingCount > 0
                ? $"{u.Name} and their {bookingCount} booking(s) were deleted."
                : $"{u.Name} has been deleted.",
            "info");
        return RedirectToAction(nameof(Users));
    }

    // ---------------------------------------------------------------
    // Announcements
    // ---------------------------------------------------------------
    public async Task<IActionResult> Announcements(int? edit)
    {
        var vm = new AdminAnnouncementsVm
        {
            Items = await _db.Announcements.OrderByDescending(a => a.CreatedAt).ToListAsync()
        };
        if (edit is not null && vm.Items.FirstOrDefault(a => a.Id == edit) is { } a)
            vm.Form = new AnnouncementFormVm
            {
                Id = a.Id,
                Title = a.Title,
                Message = a.Message,
                Kind = a.Kind,
                IsActive = a.IsActive
            };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAnnouncement(
        [Bind(Prefix = "Form")] AnnouncementFormVm form,
        bool notifyUsers = false)
    {
        if (!ModelState.IsValid)
        {
            this.Toast("Title and message are required (min. 3 characters).", "danger");
            return RedirectToAction(nameof(Announcements));
        }

        var a = form.Id is null ? new Announcement() : await _db.Announcements.FindAsync(form.Id);
        if (a is null) return NotFound();

        a.Title = form.Title.Trim();
        a.Message = form.Message.Trim();
        a.Kind = form.Kind;
        a.IsActive = form.IsActive;

        if (form.Id is null) _db.Announcements.Add(a);

        if (notifyUsers)
        {
            var ids = await _db.Users
                .Where(u => u.Role != Roles.Admin && u.IsActive)
                .Select(u => u.Id)
                .ToListAsync();
            foreach (var uid in ids) _bookings.Notify(uid, $"📢 {a.Title}", a.Message);
        }

        await _db.SaveChangesAsync();
        this.Toast(form.Id is null ? "Announcement published." : "Announcement updated.");
        return RedirectToAction(nameof(Announcements));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAnnouncement(int id)
    {
        var a = await _db.Announcements.FindAsync(id);
        if (a is not null)
        {
            _db.Announcements.Remove(a);
            await _db.SaveChangesAsync();
        }
        this.Toast("Announcement deleted.", "info");
        return RedirectToAction(nameof(Announcements));
    }

    // ---------------------------------------------------------------
    // Feedback
    // ---------------------------------------------------------------
    public async Task<IActionResult> Feedback(FeedbackStatus? status)
    {
        var all = await _db.Feedbacks
            .Include(f => f.User)
            .Include(f => f.Facility)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        return View(new AdminFeedbackVm
        {
            Status = status,
            Items = status is null ? all : all.Where(f => f.Status == status).ToList(),
            Counts = Enum.GetValues<FeedbackStatus>().ToDictionary(s => s, s => all.Count(f => f.Status == s)),
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateFeedback(int id, FeedbackStatus status, string? reply, FeedbackStatus? filter)
    {
        var fb = await _db.Feedbacks.FindAsync(id);
        if (fb is null) return NotFound();

        var newReply = (reply ?? "").Trim();
        if (newReply.Length > 1000) newReply = newReply[..1000];

        var changed = fb.Status != status || fb.AdminReply != newReply;
        fb.Status = status;
        fb.AdminReply = newReply;
        fb.UpdatedAt = DateTime.UtcNow;

        if (changed)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = fb.UserId,
                Title = $"Feedback update: {fb.StatusText()}",
                Message = $"\"{fb.Subject}\" is now {fb.StatusText().ToLower()}." +
                          (newReply.Length > 0 ? $" PTTA: {newReply}" : ""),
            });
        }

        await _db.SaveChangesAsync();
        this.Toast("Feedback updated and the user was notified.");
        return RedirectToAction(nameof(Feedback), new { status = filter });
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------
    private IActionResult Back(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction(nameof(Bookings));
}
