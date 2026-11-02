using System.ComponentModel.DataAnnotations;
using UTHMLibrarySpace.Services;

namespace UTHMLibrarySpace.Models;

// ============================================================
// Account / Auth
// ============================================================

public class LoginVm
{
    [Required(ErrorMessage = "Enter your email, matric number or staff ID")]
    [Display(Name = "Email / Matric No. / Staff ID")]
    public string Identifier { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public class RegisterVm
{
    [Required, StringLength(120, MinimumLength = 2), Display(Name = "Full name")]
    public string Name { get; set; } = "";

    [Required, EmailAddress, Display(Name = "UTHM email")]
    public string Email { get; set; } = "";

    [Required]
    public string Role { get; set; } = Roles.Student;

    [Required, Display(Name = "Matric No. / Staff ID")]
    public string IdNumber { get; set; } = "";

    [Display(Name = "Faculty")]
    public string Faculty { get; set; } = "";

    [Display(Name = "Country code")]
    public string CountryCode { get; set; } = "+60";

    [RegularExpression(@"^[0-9\-\s]{7,20}$", ErrorMessage = "Enter a valid phone number.")]
    [Display(Name = "Phone number")]
    public string Phone { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required, Compare(nameof(Password), ErrorMessage = "Passwords do not match"), DataType(DataType.Password), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";
}

// ============================================================
// Profile management
// ============================================================

public class ProfilePageVm
{
    public User User { get; set; } = default!;
    public ProfileVm Profile { get; set; } = new();
    public ChangePasswordVm Password { get; set; } = new();
}

public class ProfileVm
{
    [Required, MaxLength(120)]
    public string Name { get; set; } = "";

    [MaxLength(30)]
    public string Phone { get; set; } = "";
}

public class ChangePasswordVm
{
    [Required, DataType(DataType.Password)]
    public string OldPassword { get; set; } = "";

    [Required, MinLength(6), DataType(DataType.Password)]
    public string NewPassword { get; set; } = "";

    [Required, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";
}

// ============================================================
// Facility listing & cards
// ============================================================

public class FacilityCardVm
{
    public Facility Facility { get; set; } = null!;
    public bool InUseNow { get; set; }
    public int BookingsToday { get; set; }
    public bool CanBook { get; set; }
}

public class FacilityIndexVm
{
    public List<FacilityCardVm> Items { get; set; } = new();
    public FacilityType? Type { get; set; }
    public string? Search { get; set; }
    public int? MinCapacity { get; set; }
    public Dictionary<FacilityType, int> Counts { get; set; } = new();
    public string? Role { get; set; }
}

// ============================================================
// Facility details (booking page)
// ============================================================

public class FacilityDetailsVm
{
    public Facility Facility { get; set; } = default!;
    public DateOnly Date { get; set; }
    public DateOnly Today { get; set; }
    public List<Booking> DayBookings { get; set; } = new();
    public bool IsAuthenticated { get; set; }
    public bool CanBook { get; set; }
    public string? BlockReason { get; set; }
    public BookingInputVm Input { get; set; } = new();

    // ---- Rental support ----
    /// <summary>True for study rooms (monthly rental).</summary>
    public bool IsRental { get; set; }

    /// <summary>First day of the month shown in the rental calendar.</summary>
    public DateOnly Month { get; set; }

    /// <summary>Active and future rentals for this facility (only when IsRental).</summary>
    public List<Booking> Rentals { get; set; } = new();

    /// <summary>Category limit config (optional; used for the info panel).</summary>
    public CategoryLimit? Limit { get; set; }
}

// ============================================================
// Booking input & My Bookings
// ============================================================

public class BookingInputVm
{
    public int FacilityId { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    [Required, Display(Name = "Start time")]
    public TimeOnly StartTime { get; set; } = new(10, 0);

    [Required, Display(Name = "End time")]
    public TimeOnly EndTime { get; set; } = new(12, 0);

    [Required, StringLength(500, MinimumLength = 3)]
    public string Purpose { get; set; } = "";

    [Range(1, 1000)]
    public int Attendees { get; set; } = 1;
}

public class MyBookingsVm
{
    public List<Booking> Bookings { get; set; } = new();
    public string Tab { get; set; } = "all";
    public DateOnly Today { get; set; }
}

// ============================================================
// Dashboards
// ============================================================

public class DashboardVm
{
    public User User { get; set; } = null!;
    public List<Booking> Upcoming { get; set; } = new();
    public List<Notification> Notifications { get; set; } = new();
    public List<FacilityCardVm> Bookable { get; set; } = new();
    public int TotalBookings { get; set; }
    public int PendingCount { get; set; }
    public decimal OutstandingFees { get; set; }
}

public class AdminDashboardVm
{
    public int TotalBookings { get; set; }
    public List<string> RoomLabels { get; set; } = new();
    public List<int> RoomCounts { get; set; } = new();
    public int Pending { get; set; }
    public int TodayBookings { get; set; }
    public decimal Revenue { get; set; }
    public decimal Outstanding { get; set; }
    public int TotalUsers { get; set; }
    public int TotalFacilities { get; set; }
    public int Maintenance { get; set; }
    public List<string> DayLabels { get; set; } = new();
    public List<int> DayCounts { get; set; } = new();
    public List<string> TypeThisWeekLabels { get; set; } = new();
    public List<int> TypeThisWeekCounts { get; set; } = new();
    public List<string> TypeLabels { get; set; } = new();

    public List<int> TypeCounts { get; set; } = new();
    public List<string> HourLabels { get; set; } = new();
    public List<int> HourCounts { get; set; } = new();
    public List<Booking> PendingQueue { get; set; } = new();
    public List<Booking> TodaySchedule { get; set; } = new();
}
public class AdminWalkInVm
{
    [Required(ErrorMessage = "Please choose a facility.")]
    public int? FacilityId { get; set; }        // ← nullable

    [Required, Display(Name = "User (student or lecturer)")]
    public int UserId { get; set; }

    [Required]
    public DateOnly Date { get; set; } = AppClock.Today;

    [Required]
    public TimeOnly StartTime { get; set; } = new(10, 0);

    [Required]
    public TimeOnly EndTime { get; set; } = new(12, 0);

    [Required, StringLength(500, MinimumLength = 3)]
    public string Purpose { get; set; } = "";

    [Range(1, 1000)]
    public int Attendees { get; set; } = 1;

    public bool MarkPaid { get; set; } = true;

    public List<User> Users { get; set; } = new();
    public List<Facility> Facilities { get; set; } = new();
}
public class AdminBookingsVm
{
    public List<Booking> Bookings { get; set; } = new();
    public BookingStatus? Status { get; set; }
    public FacilityType? Type { get; set; }
    public string? Search { get; set; }

    /// <summary>Bilik Iqra' bookings whose rental period has ended — student must return the room key.</summary>
    public List<Booking> KeyReturns { get; set; } = new();
}

public class AdminLimitsVm
{
    public List<CategoryLimit> Limits { get; set; } = new();
}

// ============================================================
// Admin: facility form & announcements
// ============================================================

public class FacilityFormVm
{
    public int? Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = "";

    [Required, MaxLength(20)]
    public string Code { get; set; } = "";

    public FacilityType Type { get; set; } = FacilityType.BilikIqra;

    [Range(1, 1000)]
    public int Capacity { get; set; } = 1;

    /// <summary>Optional lower bound of the advertised pax range (e.g. 30 for "30 - 40 Orang").</summary>
    [Range(1, 1000), Display(Name = "Capacity (min. pax)")]
    public int? CapacityMin { get; set; }

    /// <summary>Number of bookable units in this room type (e.g. 118 reading carrels).</summary>
    [Range(1, 1000), Display(Name = "Number of units")]
    public int Units { get; set; } = 1;

    [MaxLength(120)]
    public string Location { get; set; } = "";

    [MaxLength(1000)]
    public string Description { get; set; } = "";

    [MaxLength(500)]
    public string Amenities { get; set; } = "";

    /// <summary>Relative URL of an uploaded photo. Empty when the facility uses an icon tile.</summary>
    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public FacilityStatus Status { get; set; } = FacilityStatus.Available;

    /// <summary>Uploaded photo file. Used for all facility types now.</summary>
    public IFormFile? ImageFile { get; set; }
}

public class AnnouncementFormVm
{
    public int? Id { get; set; }

    [Required, StringLength(150, MinimumLength = 3)]
    public string Title { get; set; } = "";

    [Required, StringLength(1000, MinimumLength = 3)]
    public string Message { get; set; } = "";

    public AnnouncementKind Kind { get; set; } = AnnouncementKind.Info;

    public bool IsActive { get; set; } = true;
}

public class AdminAnnouncementsVm
{
    public List<Announcement> Items { get; set; } = new();
    public AnnouncementFormVm Form { get; set; } = new();
}
public class VerifyCodeVm
{
    [Required] public string Email { get; set; } = "";

    [Required, StringLength(6, MinimumLength = 6)]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Enter the 6-digit code from your email.")]
    public string Code { get; set; } = "";
}

public class ForgotPasswordVm
{
    [Required] public string Email { get; set; } = "";
}

public class ResetPasswordVm
{
    [Required] public string Email { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    public string NewPassword { get; set; } = "";

    [Required, Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";
}

// ================================================================
// Feedback
// ================================================================

public class FeedbackFormVm
{
    public int? FacilityId { get; set; }

    public FeedbackKind Kind { get; set; } = FeedbackKind.Report;

    [Required, StringLength(150, MinimumLength = 3)]
    public string Subject { get; set; } = "";

    [Required, StringLength(2000, MinimumLength = 10)]
    public string Description { get; set; } = "";
}

public class FeedbackPageVm
{
    public FeedbackFormVm Form { get; set; } = new();
    public List<Feedback> Items { get; set; } = new();
    public List<Facility> Facilities { get; set; } = new();
}

public class AdminFeedbackVm
{
    public FeedbackStatus? Status { get; set; }
    public List<Feedback> Items { get; set; } = new();
    public Dictionary<FeedbackStatus, int> Counts { get; set; } = new();
}

// ============================================================
// Chat / Gemini
// ============================================================

public class ChatTurn
{
    public string Role { get; set; } = "user";  // "user" | "model"
    public string Text { get; set; } = "";
}

public class ChatRequest
{
    public string Message { get; set; } = "";
    public List<ChatTurn> History { get; set; } = new();
}
