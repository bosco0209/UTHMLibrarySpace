namespace UTHMLibrarySpace.Models;

/// <summary>Booking policy per facility type (from the PSM proposal).</summary>
public record FacilityRule(
    string Label, string Plural, string[] Roles, bool RequiresApproval, decimal Fee, string Icon, string Blurb);

public static class FacilityRules
{
    public static readonly TimeOnly Open = new(8, 0);
    public static readonly TimeOnly Close = new(22, 0);
    public const long MaxProofBytes = 5 * 1024 * 1024;

    /// <summary>Shown on the proof-of-payment page. Edit to match the real PTTA payment channel.</summary>
    public const string PaymentInstructions =
        "Pay at the PTTA Level 1 counter or transfer to the UTHM Library account, then upload the receipt (JPG, PNG or PDF, max 5 MB).";

    /// <summary>Study rooms are rented for one whole month from the chosen start date instead of by the hour.</summary>
    public static bool IsMonthlyRental(FacilityType t) => t == FacilityType.BilikIqra;

    /// <summary>Last day of a one-month rental, e.g. 15 Mar → 14 Apr.</summary>
    public static DateOnly RentalEnd(DateOnly start) => start.AddMonths(1).AddDays(-1);

    /// <summary>Study-room rentals (any role) and every lecturer booking need an uploaded proof of payment before admin review.</summary>
    public static bool NeedsProof(FacilityType t, string role) => IsMonthlyRental(t) || role == Models.Roles.Lecturer;

    public static readonly IReadOnlyDictionary<FacilityType, FacilityRule> All = new Dictionary<FacilityType, FacilityRule>
    {
        [FacilityType.BilikIqra] = new("Bilik Iqra'", "Bilik Iqra'", new[] { Models.Roles.Student, Models.Roles.Lecturer }, true, 20m,
            "bi-book", "Rent your own reading carrel for a whole month. RM20 per month."),
        [FacilityType.LestariLounge] = new("Lestari Lounge", "Lestari Lounge", new[] { Models.Roles.Student, Models.Roles.Lecturer }, true, 0m,
            "bi-cup-straw", "Ruang santai eksklusif pembelajaran kolaboratif."),
        [FacilityType.BilikEksplorasi] = new("Bilik Eksplorasi", "Bilik Eksplorasi", new[] { Models.Roles.Student, Models.Roles.Lecturer }, true, 0m,
            "bi-people", "Bilik perbincangan mini seminar untuk perbincangan kumpulan."),
        [FacilityType.BilikHikmah] = new("Bilik Hikmah", "Bilik Hikmah", new[] { Models.Roles.Student, Models.Roles.Lecturer }, true, 0m,
            "bi-chat-square-text", "Bilik perbincangan kumpulan dilengkapi skrin & papan putih."),
        [FacilityType.BilikLestari] = new("Bilik Lestari", "Bilik Lestari", new[] { Models.Roles.Lecturer }, true, 0m,
            "bi-easel", "Bilik seminar utama untuk bengkel & taklimat."),
        [FacilityType.Auditorium] = new("Auditorium", "Auditorium", new[] { Models.Roles.Lecturer }, true, 0m,
            "bi-building", "Dewan persembahan & persidangan berprestij."),
        [FacilityType.MakmalAI] = new("Makmal A.I.", "Makmal A.I.", new[] { Models.Roles.Lecturer }, true, 0m,
            "bi-cpu", "Stesen kerja berkuasa tinggi khas A.I."),
        [FacilityType.RemotePilot] = new("Remote Pilot Smart Classroom", "Remote Pilot Smart Classroom", new[] { Models.Roles.Lecturer }, true, 0m,
            "bi-camera-video", "Bilik pengajaran pintar interaktif & multimedia."),
        [FacilityType.BilikTutorial] = new("Bilik Tutorial", "Bilik Tutorial", new[] { Models.Roles.Lecturer }, true, 0m,
            "bi-mortarboard", "Bilik tutorial — hanya boleh ditempah oleh pensyarah."),
    };

    public static FacilityRule For(FacilityType t) => All[t];

    /// <summary>Study and meeting rooms are shown as clean icon tiles instead of photos.</summary>
    public static bool ShowsImage(FacilityType t) => true;

    public static string FeeText(FacilityType t) =>
        All[t].Fee > 0 ? (IsMonthlyRental(t) ? $"RM{All[t].Fee:0} per month" : $"RM{All[t].Fee:0} / session") : "Free";

    public static string RolesText(FacilityType t) =>
        string.Join(" & ", All[t].Roles.Select(r => r + "s"));
}
