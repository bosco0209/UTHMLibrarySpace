using Microsoft.EntityFrameworkCore;
using UTHMLibrarySpace.Data;
using UTHMLibrarySpace.Models;

namespace UTHMLibrarySpace.Services;

/// <summary>
/// Booking rules: role permissions, library hours, monthly study-room rentals, category limits,
/// clash detection, approval workflow, 24-hour payment window and notifications.
/// </summary>
public class BookingService
{
    private static readonly BookingStatus[] ActiveStatuses =
        { BookingStatus.Confirmed, BookingStatus.Pending, BookingStatus.Approved };

    private readonly AppDbContext _db;
    private readonly string _proofDir;

    public BookingService(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _proofDir = Path.Combine(env.ContentRootPath, "App_Data", "proofs");
    }

    public string? ProofPath(Booking b) =>
        b.HasProof ? Path.Combine(_proofDir, b.ProofFileName!) : null;

    // ================================================================
    // Proof of payment upload
    // ================================================================
    public async Task<string?> SubmitProofAsync(int bookingId, User user, IFormFile? file)
    {
        var b = await _db.Bookings.Include(x => x.Facility)
            .FirstOrDefaultAsync(x => x.Id == bookingId && x.UserId == user.Id);
        if (b is null) return "Booking not found.";
        if (b.Status != BookingStatus.Pending ||
            b.PaymentStatus is not (PaymentStatus.AwaitingProof or PaymentStatus.ProofSubmitted))
            return "Proof can only be uploaded while the booking is waiting for approval.";

        if (b.PaymentDueAt is DateTime due && due < DateTime.UtcNow)
            return "The 24-hour payment window has closed. This booking has been released.";

        if (file is null || file.Length == 0) return "Please choose a file to upload.";
        if (file.Length > FacilityRules.MaxProofBytes)
            return "File is too large. The maximum size is 5 MB.";

        var head = new byte[8];
        await using (var s = file.OpenReadStream()) _ = await s.ReadAsync(head);

        (string ext, string type)? kind =
            head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF ? (".jpg", "image/jpeg") :
            head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47 ? (".png", "image/png") :
            head[0] == 0x25 && head[1] == 0x50 && head[2] == 0x44 && head[3] == 0x46 ? (".pdf", "application/pdf") : null;

        if (kind is null) return "Only JPG, PNG or PDF files are accepted.";

        Directory.CreateDirectory(_proofDir);
        if (ProofPath(b) is { } old && File.Exists(old)) File.Delete(old);

        var name = $"{b.Reference}-{Guid.NewGuid():N}{kind.Value.ext}";
        await using (var outStream = File.Create(Path.Combine(_proofDir, name)))
            await file.CopyToAsync(outStream);

        b.ProofFileName = name;
        b.ProofOriginalName = Path.GetFileName(file.FileName);
        b.ProofContentType = kind.Value.type;
        b.ProofUploadedAt = DateTime.UtcNow;
        b.PaymentStatus = PaymentStatus.ProofSubmitted;

        Notify(user.Id, "Proof of payment received",
            $"We received your proof of payment for {b.Facility!.Name} ({b.DateText}). An administrator will review it shortly.");

        await _db.SaveChangesAsync();
        return null;
    }

    // ================================================================
    // Sweep expired pending rentals (24-hour window)
    // ================================================================
    public async Task<int> SweepExpiredRentalsAsync()
    {
        var now = DateTime.UtcNow;

        var expired = await _db.Bookings
            .Include(b => b.Facility)
            .Where(b => b.Status == BookingStatus.Pending
                     && b.PaymentStatus == PaymentStatus.AwaitingProof
                     && b.PaymentDueAt != null
                     && b.PaymentDueAt < now)
            .ToListAsync();

        if (expired.Count == 0) return 0;

        foreach (var b in expired)
        {
            b.Status = BookingStatus.Cancelled;
            b.PaymentStatus = PaymentStatus.None;
            b.AdminNote = "Auto-cancelled — 24-hour payment window expired.";

            Notify(b.UserId, "Booking released — payment window expired",
                $"{b.Facility!.Name} ({b.DateText}) has been released because we didn't receive your proof of payment within 24 hours. You can book again if it's still available.");
        }

        await _db.SaveChangesAsync();
        return expired.Count;
    }

    // ================================================================
    // Role rules
    // ================================================================
    public static string? RoleBlockReason(FacilityType type, string? role)
    {
        if (role is null) return "Log in to book this facility.";
        var rule = FacilityRules.For(type);
        return rule.Roles.Contains(role)
            ? null
            : $"{rule.Plural} can only be booked by {FacilityRules.RolesText(type)}.";
    }

    public Task<CategoryLimit?> LimitAsync(FacilityType type) =>
        _db.CategoryLimits.FirstOrDefaultAsync(l => l.Type == type);

    // ================================================================
    // Facility cards for listing
    // ================================================================
    public async Task<List<FacilityCardVm>> LiveCardsAsync(IQueryable<Facility> query, string? role)
    {
        var facilities = await query.OrderBy(f => f.Type).ThenBy(f => f.Code).ToListAsync();
        var today = AppClock.Today;
        var now = AppClock.TimeNow;

        var todays = await _db.Bookings
            .Where(b => b.Date <= today && b.EndDate >= today && ActiveStatuses.Contains(b.Status))
            .Select(b => new { b.FacilityId, b.StartTime, b.EndTime, b.Status })
            .ToListAsync();

        return facilities.Select(f =>
        {
            var mine = todays.Where(b => b.FacilityId == f.Id).ToList();
            return new FacilityCardVm
            {
                Facility = f,
                BookingsToday = mine.Count,
                InUseNow = mine.Any(b => b.Status != BookingStatus.Pending
                                      && b.StartTime <= now && now < b.EndTime),
                CanBook = f.Status == FacilityStatus.Available
                       && RoleBlockReason(f.Type, role) is null,
            };
        }).ToList();
    }

    public Task<List<Booking>> DayBookingsAsync(int facilityId, DateOnly date) =>
        _db.Bookings.Include(b => b.User)
            .Where(b => b.FacilityId == facilityId
                     && b.Date <= date && b.EndDate >= date
                     && ActiveStatuses.Contains(b.Status))
            .OrderBy(b => b.StartTime)
            .ToListAsync();

    // ================================================================
    // Self-booking (student / lecturer)
    // ================================================================
    public async Task<(Booking? booking, string? error)> CreateAsync(User user, BookingInputVm input)
    {
        if (user is null) return (null, "User not found.");
        if (input is null) return (null, "Booking input is empty.");

        var facility = await _db.Facilities.FindAsync(input.FacilityId);
        if (facility is null) return (null, "Facility not found.");

        // Admins promoted from a student/lecturer account book under their preserved booking role.
        var bookingRole = user.EffectiveBookingRole;

        var block = RoleBlockReason(facility.Type, bookingRole);
        if (block is not null) return (null, block);
        if (facility.Status == FacilityStatus.Maintenance)
            return (null, "This facility is currently under maintenance.");

        var today = AppClock.Today;
        if (input.Date < today) return (null, "You cannot book a date in the past.");
        if (input.Date > today.AddDays(60)) return (null, "Bookings open up to 60 days in advance.");

        // Block ALL new bookings for any role until an overdue Bilik Iqra' key is returned.
        var keyOverdue = await _db.Bookings.Include(b => b.Facility)
            .AnyAsync(b => b.UserId == user.Id
                        && b.Facility!.Type == FacilityType.BilikIqra
                        && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Confirmed)
                        && !b.KeyCollected
                        && b.EndDate < today);
        if (keyOverdue)
            return (null, "You cannot book any facility until your overdue Bilik Iqra' room key is returned. Please collect your key at the PTTA counter.");

        var rule = FacilityRules.For(facility.Type);
        var limit = await LimitAsync(facility.Type);
        var rental = FacilityRules.IsMonthlyRental(facility.Type);
        var endDate = input.Date;

        if (rental)
        {
            input.StartTime = FacilityRules.Open;
            input.EndTime = FacilityRules.Close;
            endDate = FacilityRules.RentalEnd(input.Date);
        }
        else
        {
            if (input.EndTime <= input.StartTime)
                return (null, "End time must be after start time.");
            if (input.StartTime < FacilityRules.Open || input.EndTime > FacilityRules.Close)
                return (null, "Bookings must be within library hours (08:00 – 22:00).");
            if (input.Date == today && input.StartTime < AppClock.TimeNow.AddMinutes(-15))
                return (null, "That start time has already passed today.");
        }

        if (input.Attendees > facility.Capacity)
            return (null, $"This room fits a maximum of {facility.Capacity} people.");

        // Category limits
        var mineActive = await _db.Bookings.Include(b => b.Facility)
            .Where(b => b.UserId == user.Id
                     && b.Facility!.Type == facility.Type
                     && ActiveStatuses.Contains(b.Status)
                     && b.EndDate >= today)
            .ToListAsync();

        if (limit?.MaxActiveBookings is int maxActive && mineActive.Count >= maxActive)
            return (null, rental
                ? $"You already have {mineActive.Count} active study room rental(s). The limit is {maxActive} per user."
                : $"You already have {mineActive.Count} active {rule.Plural.ToLower()} booking(s). The limit is {maxActive} per user.");

        if (!rental)
        {
            var hours = (input.EndTime - input.StartTime).TotalHours;
            if (limit?.MaxHoursPerBooking is double maxPer && hours > maxPer + 1e-9)
                return (null, $"{rule.Plural} can be booked for at most {maxPer:0.#} hour(s) per booking.");

            if (limit?.MaxHoursPerWeek is double maxWeek)
            {
                var weekStart = input.Date.AddDays(-(((int)input.Date.DayOfWeek + 6) % 7));
                var weekEnd = weekStart.AddDays(6);
                var used = mineActive.Where(b => b.Date >= weekStart && b.Date <= weekEnd)
                                     .Sum(b => (b.EndTime - b.StartTime).TotalHours);
                if (used + hours > maxWeek + 1e-9)
                    return (null, $"Weekly limit reached: you have {used:0.#}h of {rule.Plural.ToLower()} booked for the week of {weekStart:dd MMM}. The limit is {maxWeek:0.#}h per week.");
            }
        }

        // Clash detection
        var clash = await _db.Bookings.FirstOrDefaultAsync(b =>
            b.FacilityId == facility.Id
            && ActiveStatuses.Contains(b.Status)
            && b.Date <= endDate && b.EndDate >= input.Date
            && b.StartTime < input.EndTime && b.EndTime > input.StartTime);

        if (clash is not null)
            return (null, rental
                ? $"This study room is already rented or booked between {clash.Date:dd MMM} and {clash.EndDate:dd MMM}. Choose another start date or room."
                : $"That time clashes with an existing booking ({clash.StartTime:HH\\:mm}–{clash.EndTime:HH\\:mm}).");

        var needsProof = FacilityRules.NeedsProof(facility.Type, bookingRole);

        var booking = new Booking
        {
            Reference = await NewReferenceAsync(),
            UserId = user.Id,
            FacilityId = facility.Id,
            Date = input.Date,
            EndDate = endDate,
            StartTime = input.StartTime,
            EndTime = input.EndTime,
            Purpose = input.Purpose.Trim(),
            Attendees = input.Attendees,
            Fee = rule.Fee,
            PaymentStatus = needsProof
                ? PaymentStatus.AwaitingProof
                : rule.Fee > 0 ? PaymentStatus.PayAtCounter : PaymentStatus.None,
            Status = needsProof || rule.RequiresApproval
                ? BookingStatus.Pending
                : BookingStatus.Confirmed,
            PaymentDueAt = needsProof ? DateTime.UtcNow.AddHours(24) : null,
        };
        _db.Bookings.Add(booking);

        var when = rental
            ? $"{input.Date:dd MMM} – {endDate:dd MMM yyyy} (1 month)"
            : $"{input.Date:dd MMM yyyy} {input.StartTime:HH\\:mm}–{input.EndTime:HH\\:mm}";

        Notify(user.Id,
            needsProof ? "Upload your proof of payment"
                : booking.Status == BookingStatus.Confirmed ? "Booking confirmed"
                : "Booking submitted for approval",
            needsProof
                ? $"{facility.Name}, {when}, is on hold for you. Upload your proof of payment within 24 hours so an administrator can approve it."
                : booking.Status == BookingStatus.Confirmed
                ? $"{facility.Name}, {when}, is confirmed."
                : $"Your request for {facility.Name} on {when} is waiting for administrator approval.");

        await _db.SaveChangesAsync();
        booking.Facility = facility;
        return (booking, null);
    }

    // ================================================================
    // Cancel
    // ================================================================
    public async Task<string?> CancelAsync(int bookingId, User user)
    {
        var b = await _db.Bookings.Include(x => x.Facility).FirstOrDefaultAsync(x => x.Id == bookingId);
        if (b is null || (b.UserId != user.Id && user.Role != Roles.Admin)) return "Booking not found.";
        if (!b.IsActive) return "Only active bookings can be cancelled.";

        if (user.Role != Roles.Admin)
        {
            // Students & lecturers: no cancellation once proof of payment has been submitted.
            if (b.PaymentStatus is PaymentStatus.ProofSubmitted or PaymentStatus.Paid)
                return "You cannot cancel this booking after submitting proof of payment. Please contact the PTTA counter.";

            // Bilik Iqra': no cancellation once the booking is successfully booked (confirmed/approved).
            if (b.Facility?.Type == FacilityType.BilikIqra
                && b.Status is BookingStatus.Confirmed or BookingStatus.Approved)
                return "Bilik Iqra' bookings cannot be cancelled once confirmed. Please contact the PTTA counter.";
        }

        b.Status = BookingStatus.Cancelled;
        if (b.PaymentStatus is PaymentStatus.PayAtCounter or PaymentStatus.AwaitingProof)
            b.PaymentStatus = PaymentStatus.None;

        if (b.UserId != user.Id)
            Notify(b.UserId, "Booking cancelled",
                $"{b.Facility!.Name} ({b.DateText}) was cancelled by the library.");

        await _db.SaveChangesAsync();
        return null;
    }

    // ================================================================
    // Approve / reject
    // ================================================================
    public async Task<string?> DecideAsync(int bookingId, bool approve, string? note)
    {
        var b = await _db.Bookings.Include(x => x.Facility).FirstOrDefaultAsync(x => x.Id == bookingId);
        if (b is null) return "Booking not found.";
        if (b.Status != BookingStatus.Pending)
            return "Only pending bookings can be approved or rejected.";

        if (approve)
        {
            if (b.PaymentStatus == PaymentStatus.AwaitingProof)
                return "Cannot approve yet. The user has not uploaded proof of payment.";

            var clash = await _db.Bookings.AnyAsync(x =>
                x.Id != b.Id && x.FacilityId == b.FacilityId
                && x.Date <= b.EndDate && x.EndDate >= b.Date
                && (x.Status == BookingStatus.Confirmed || x.Status == BookingStatus.Approved)
                && x.StartTime < b.EndTime && x.EndTime > b.StartTime);

            if (clash) return "Cannot approve. Another booking already holds this slot.";
        }

        b.Status = approve ? BookingStatus.Approved : BookingStatus.Rejected;
        if (approve && b.PaymentStatus == PaymentStatus.ProofSubmitted) b.PaymentStatus = PaymentStatus.Paid;
        if (!approve && b.PaymentStatus == PaymentStatus.AwaitingProof) b.PaymentStatus = PaymentStatus.None;

        b.AdminNote = note?.Trim() ?? "";
        var verb = approve ? "approved" : "rejected";

        Notify(b.UserId, $"Booking {verb}",
            $"{b.Facility!.Name}, {b.DateText} {b.TimeText}, was {verb}." +
            (string.IsNullOrWhiteSpace(b.AdminNote) ? "" : $" Note: {b.AdminNote}"));

        await _db.SaveChangesAsync();
        return null;
    }

    // ================================================================
    // Notification helper
    // ================================================================
    public void Notify(int userId, string title, string message) =>
        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title,
            Message = message
        });

    // ================================================================
    // ✅ Admin walk-in booking — FULLY CORRECTED
    // ================================================================
    /// <summary>
    /// Admin creates a walk-in booking on behalf of another user.
    /// Bypasses role check, approval workflow and the 24-hour payment window.
    /// Booking is auto-approved and marked paid, as the admin collects payment at the counter.
    /// </summary>
    public async Task<(Booking? booking, string? error)> CreateOnBehalfAsync(
        User booker, User onBehalfOf, BookingInputVm input, bool markPaid = true)
    {
        // ---------- 1. Null guards (BEFORE any property access) ----------
        if (booker is null) return (null, "Booker account not found.");
        if (onBehalfOf is null) return (null, "The selected user (on-behalf) was not found.");
        if (input is null) return (null, "Booking input is empty.");
        if (input.FacilityId <= 0) return (null, "Please select a valid facility.");

        // ---------- 2. Facility validation ----------
        var facility = await _db.Facilities.FindAsync(input.FacilityId);
        if (facility is null) return (null, "Facility not found.");
        if (facility.Status == FacilityStatus.Maintenance)
            return (null, "This facility is currently under maintenance.");

        // ---------- 3. Date / time validation ----------
        var today = AppClock.Today;
        if (input.Date < today) return (null, "You cannot book a date in the past.");
        if (input.Date > today.AddDays(60)) return (null, "Bookings open up to 60 days in advance.");

        var rule = FacilityRules.For(facility.Type);
        var rental = FacilityRules.IsMonthlyRental(facility.Type);
        var endDate = input.Date;

        if (rental)
        {
            input.StartTime = FacilityRules.Open;
            input.EndTime = FacilityRules.Close;
            endDate = FacilityRules.RentalEnd(input.Date);
        }
        else
        {
            if (input.EndTime <= input.StartTime)
                return (null, "End time must be after start time.");
            if (input.StartTime < FacilityRules.Open || input.EndTime > FacilityRules.Close)
                return (null, "Bookings must be within library hours (08:00 – 22:00).");
        }

        if (input.Attendees > facility.Capacity)
            return (null, $"This room fits a maximum of {facility.Capacity} people.");

        // ---------- 4. Clash detection ----------
        var clash = await _db.Bookings.FirstOrDefaultAsync(b =>
            b.FacilityId == facility.Id
            && ActiveStatuses.Contains(b.Status)
            && b.Date <= endDate && b.EndDate >= input.Date
            && b.StartTime < input.EndTime && b.EndTime > input.StartTime);

        if (clash is not null)
            return (null, rental
                ? $"This study room is already rented or booked between {clash.Date:dd MMM} and {clash.EndDate:dd MMM}."
                : $"That time clashes with an existing booking ({clash.StartTime:HH\\:mm}–{clash.EndTime:HH\\:mm}).");

        // ---------- 5. Create the booking ----------
        var booking = new Booking
        {
            Reference = await NewReferenceAsync(),
            UserId = onBehalfOf.Id,
            FacilityId = facility.Id,
            Date = input.Date,
            EndDate = endDate,
            StartTime = input.StartTime,
            EndTime = input.EndTime,
            Purpose = input.Purpose?.Trim() ?? "Walk-in booking",
            Attendees = input.Attendees,
            Fee = rule.Fee,

            // Admin collects cash at the counter — no payment window, no proof required.
            PaymentStatus = rule.Fee > 0
                ? (markPaid ? PaymentStatus.Paid : PaymentStatus.PayAtCounter)
                : PaymentStatus.None,

            // Booked and approved by the counter — no approval queue needed.
            Status = BookingStatus.Approved,

            AdminNote = $"Walk-in booking created by {booker.Name} at the PTTA counter.",
            PaymentDueAt = null,
        };
        _db.Bookings.Add(booking);

        Notify(onBehalfOf.Id, "Booking created at PTTA counter",
            $"{facility.Name} on {booking.DateText} {booking.TimeText} was booked for you by the PTTA counter. Reference: {booking.Reference}.");

        await _db.SaveChangesAsync();
        booking.Facility = facility;
        return (booking, null);
    }

    // ================================================================
    // Booking reference generator
    // ================================================================
    private async Task<string> NewReferenceAsync()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        while (true)
        {
            var r = "PTTA-" + new string(Enumerable.Range(0, 6)
                .Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
            if (!await _db.Bookings.AnyAsync(b => b.Reference == r)) return r;
        }
    }
}