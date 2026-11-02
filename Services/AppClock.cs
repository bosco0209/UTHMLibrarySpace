namespace UTHMLibrarySpace.Services;

/// <summary>All "today/now" logic is anchored to Malaysia time (UTC+8), not the server clock.</summary>
public static class AppClock
{
    private static readonly TimeZoneInfo Zone = ResolveZone();

    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "Asia/Kuala_Lumpur", "Singapore Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { /* try next */ }
        }
        return TimeZoneInfo.CreateCustomTimeZone("MYT", TimeSpan.FromHours(8), "Malaysia Time", "Malaysia Time");
    }

    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);
    public static DateOnly Today => DateOnly.FromDateTime(Now);
    public static TimeOnly TimeNow => TimeOnly.FromDateTime(Now);
    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
}
