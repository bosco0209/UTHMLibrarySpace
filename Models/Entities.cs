using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UTHMLibrarySpace.Models;

public static class Roles
{
    public const string Student = "Student";
    public const string Lecturer = "Lecturer";
    public const string Admin = "Admin";
}

/// <summary>Named authorization policies.</summary>
public static class AuthPolicies
{
    /// <summary>Any account that holds a student/lecturer booking identity — including admins promoted from one of those roles.</summary>
    public const string CanBook = "CanBook";
}

public enum FacilityType { BilikIqra, LestariLounge, BilikEksplorasi, BilikHikmah, BilikLestari, Auditorium, MakmalAI, RemotePilot, BilikTutorial }
public enum FacilityStatus { Available, Maintenance }
public enum BookingStatus { Confirmed, Pending, Approved, Rejected, Cancelled }
public enum PaymentStatus { None, PayAtCounter, Paid, AwaitingProof, ProofSubmitted }
public enum AnnouncementKind { Info, Maintenance, Event, Urgent }

public class User
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    [Required, MaxLength(160)] public string Email { get; set; } = "";
    /// <summary>Matric number for students, staff ID for lecturers/admins.</summary>
    [Required, MaxLength(30)] public string IdNumber { get; set; } = "";
    [Required, MaxLength(20)] public string Role { get; set; } = Roles.Student;

    /// <summary>
    /// Booking identity, kept when this account is promoted to Admin. Empty means "just use <see cref="Role"/>.
    /// Set to Student/Lecturer when a member of that role becomes an admin, so they keep booking rooms
    /// with the permissions of the role they held (a promoted lecturer books like a lecturer).
    /// </summary>
    [MaxLength(20)] public string BookingRole { get; set; } = "";

    /// <summary>The role used for booking rules: the preserved booking identity, or <see cref="Role"/>.</summary>
    [NotMapped]
    public string EffectiveBookingRole =>
        string.IsNullOrWhiteSpace(BookingRole) ? Role : BookingRole;

    /// <summary>True when this account has a student/lecturer identity and may book facilities.</summary>
    [NotMapped]
    public bool CanBook => EffectiveBookingRole is Roles.Student or Roles.Lecturer;
    [MaxLength(120)] public string Faculty { get; set; } = "";
    [MaxLength(30)] public string Phone { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    /// <summary>Stored file name of the profile picture inside App_Data/avatars (served via /Account/Avatar/{id}).</summary>
    [MaxLength(200)] public string? AvatarFileName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Booking> Bookings { get; set; } = new();
    public List<Notification> Notifications { get; set; } = new();
}

public class Facility
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    [Required, MaxLength(20)] public string Code { get; set; } = "";
    public FacilityType Type { get; set; }
    /// <summary>Maximum pax. Also the ceiling used by the min-capacity filter and the attendee check.</summary>
    [Range(1, 1000)] public int Capacity { get; set; }
    /// <summary>Lower bound of the advertised pax range, e.g. 30 for "30 - 40 Orang". Display only; null = single figure.</summary>
    [Range(1, 1000)] public int? CapacityMin { get; set; }
    /// <summary>Number of bookable units in this room type, e.g. 118 reading carrels. Display only.</summary>
    [Range(1, 1000)] public int Units { get; set; } = 1;
    [MaxLength(120)] public string Location { get; set; } = "";
    /// <summary>Specific room-type label for this facility, e.g. "Karel Pembacaan Khusus".</summary>
    [MaxLength(120)] public string TypeName { get; set; } = "";
    [MaxLength(1000)] public string Description { get; set; } = "";
    /// <summary>Comma-separated amenity list, e.g. "Whiteboard,Smart TV".</summary>
    [MaxLength(500)] public string Amenities { get; set; } = "";
    [MaxLength(500)] public string ImageUrl { get; set; } = "";
    public FacilityStatus Status { get; set; } = FacilityStatus.Available;
    public List<Booking> Bookings { get; set; } = new();

    public IEnumerable<string> AmenityList =>
        Amenities.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Display-only pax range, e.g. "30 - 40". Falls back to <see cref="Capacity"/>.</summary>
    public string CapacityRangeText =>
        CapacityMin is { } min && min > 0 && min < Capacity ? $"{min} - {Capacity}" : Capacity.ToString();
}

public class Booking
{
    public int Id { get; set; }
    [Required, MaxLength(20)] public string Reference { get; set; } = "";
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTime? PaymentDueAt { get; set; }
    public bool PaymentExpired =>
    PaymentDueAt.HasValue
    && PaymentDueAt.Value < DateTime.UtcNow
    && Status == BookingStatus.Pending
    && PaymentStatus == PaymentStatus.AwaitingProof;
    public int FacilityId { get; set; }
    public Facility? Facility { get; set; }
    public DateOnly Date { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    [Required, MaxLength(500)] public string Purpose { get; set; } = "";
    public int Attendees { get; set; }
    public decimal Fee { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.None;
    public BookingStatus Status { get; set; }
    [MaxLength(500)] public string AdminNote { get; set; } = "";

    /// <summary>For Bilik Iqra' rentals: true once the room key has been physically returned and collected by staff.</summary>
    public bool KeyCollected { get; set; }

    /// <summary>Stored file name of the uploaded proof of payment (inside App_Data/proofs, never public).</summary>
    [MaxLength(200)] public string? ProofFileName { get; set; }
    [MaxLength(200)] public string? ProofOriginalName { get; set; }
    [MaxLength(100)] public string? ProofContentType { get; set; }
    public DateTime? ProofUploadedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive => Status is BookingStatus.Confirmed or BookingStatus.Pending or BookingStatus.Approved;
    public bool IsRental => EndDate > Date;
    public bool AwaitingProof => PaymentStatus == PaymentStatus.AwaitingProof && Status == BookingStatus.Pending;
    public bool HasProof => !string.IsNullOrEmpty(ProofFileName);

    public string DateText => IsRental
        ? $"{Date:dd MMM} – {EndDate:dd MMM yyyy}"
        : Date.ToString("dd MMM yyyy");

    public string TimeText => IsRental
        ? "1-month rental"
        : $"{StartTime:HH\\:mm} – {EndTime:HH\\:mm}";

    public string PaymentText => PaymentStatus switch
    {
        PaymentStatus.Paid => "Paid",
        PaymentStatus.PayAtCounter => "Pay at counter",
        PaymentStatus.AwaitingProof => "Proof needed",
        PaymentStatus.ProofSubmitted => "Proof submitted",
        _ => "—",
    };
}

/// <summary>Admin-configurable booking limits for one facility category. Null = no limit.</summary>
public class CategoryLimit
{
    public int Id { get; set; }
    public FacilityType Type { get; set; }
    /// <summary>Max hours in a single booking (hourly categories only).</summary>
    public double? MaxHoursPerBooking { get; set; }
    /// <summary>Max total hours one user may book in this category per week (Mon–Sun).</summary>
    public double? MaxHoursPerWeek { get; set; }
    /// <summary>Max active (pending/confirmed/approved, not yet ended) bookings per user. For study rooms = active monthly rentals.</summary>
    public int? MaxActiveBookings { get; set; }
}

public class Announcement
{
    public int Id { get; set; }
    [Required, MaxLength(150)] public string Title { get; set; } = "";
    [Required, MaxLength(1000)] public string Message { get; set; } = "";
    public AnnouncementKind Kind { get; set; } = AnnouncementKind.Info;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    [Required, MaxLength(150)] public string Title { get; set; } = "";
    [Required, MaxLength(600)] public string Message { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ================================================================
// Feedback / facility reports
// ================================================================

public enum FeedbackKind { Report, Suggestion, Question }
public enum FeedbackStatus { Open, InProgress, Resolved, Closed }

public class Feedback
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Optional — null means "general feedback", not about a specific room.</summary>
    public int? FacilityId { get; set; }
    public Facility? Facility { get; set; }

    public FeedbackKind Kind { get; set; } = FeedbackKind.Report;
    public FeedbackStatus Status { get; set; } = FeedbackStatus.Open;

    [Required, MaxLength(150)]
    public string Subject { get; set; } = "";

    [Required, MaxLength(2000)]
    public string Description { get; set; } = "";

    [MaxLength(200)]
    public string? ImageFileName { get; set; }

    [MaxLength(1000)]
    public string? AdminReply { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

// ---------- Presentation helpers (used by views) ----------
public static class FeedbackExtensions
{
    public static string StatusText(this FeedbackStatus s) => s switch
    {
        FeedbackStatus.Open => "Open",
        FeedbackStatus.InProgress => "In progress",
        FeedbackStatus.Resolved => "Resolved",
        FeedbackStatus.Closed => "Closed",
        _ => s.ToString()
    };

    public static string StatusPill(this FeedbackStatus s) => s switch
    {
        FeedbackStatus.Open => "pill-pending",
        FeedbackStatus.InProgress => "pill-approved",
        FeedbackStatus.Resolved => "pill-confirmed",
        FeedbackStatus.Closed => "pill-muted",
        _ => "pill-muted"
    };

    public static string KindText(this FeedbackKind k) => k switch
    {
        FeedbackKind.Report => "Report a problem",
        FeedbackKind.Suggestion => "Suggestion",
        FeedbackKind.Question => "Question",
        _ => k.ToString()
    };

    public static string KindIcon(this FeedbackKind k) => k switch
    {
        FeedbackKind.Report => "bi-exclamation-triangle",
        FeedbackKind.Suggestion => "bi-lightbulb",
        FeedbackKind.Question => "bi-question-circle",
        _ => "bi-chat"
    };

    // Convenience accessors used by both views as `f.StatusText` etc.
    public static string StatusText(this Feedback f) => f.Status.StatusText();
    public static string StatusPill(this Feedback f) => f.Status.StatusPill();
    public static string KindText(this Feedback f) => f.Kind.KindText();
    public static string KindIcon(this Feedback f) => f.Kind.KindIcon();
}