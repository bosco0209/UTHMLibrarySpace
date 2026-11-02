using UTHMLibrarySpace.Models;

namespace UTHMLibrarySpace.Infrastructure;

public static class Ui
{
    public static string StatusClass(BookingStatus s) => s switch
    {
        BookingStatus.Confirmed => "pill-confirmed",
        BookingStatus.Approved => "pill-approved",
        BookingStatus.Pending => "pill-pending",
        BookingStatus.Rejected => "pill-rejected",
        _ => "pill-muted",
    };

    public static string StatusIcon(BookingStatus s) => s switch
    {
        BookingStatus.Confirmed => "bi-check-circle-fill",
        BookingStatus.Approved => "bi-patch-check-fill",
        BookingStatus.Pending => "bi-hourglass-split",
        BookingStatus.Rejected => "bi-x-circle-fill",
        _ => "bi-slash-circle",
    };

    public static string AnnouncementIcon(AnnouncementKind k) => k switch
    {
        AnnouncementKind.Maintenance => "bi-tools",
        AnnouncementKind.Event => "bi-calendar-event",
        AnnouncementKind.Urgent => "bi-exclamation-octagon",
        _ => "bi-megaphone",
    };

    public static string TypeSlug(FacilityType t) => t.ToString().ToLowerInvariant();

    public static string Ago(DateTime utc)
    {
        var d = DateTime.UtcNow - DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        if (d.TotalMinutes < 1) return "just now";
        if (d.TotalHours < 1) return $"{(int)d.TotalMinutes}m ago";
        if (d.TotalDays < 1) return $"{(int)d.TotalHours}h ago";
        return $"{(int)d.TotalDays}d ago";
    }

    public static string Initials(string name) =>
        string.Concat(name.Replace("Dr.", "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(p => p.Length > 2 || p.All(char.IsUpper)).Take(2).Select(p => p[0])).ToUpperInvariant();
}
