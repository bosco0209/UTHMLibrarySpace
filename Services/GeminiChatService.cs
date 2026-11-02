using System.Net.Http.Json;
using System.Text.Json;
using UTHMLibrarySpace.Models;

namespace UTHMLibrarySpace.Services;

/// <summary>Who is asking, so the assistant can give role-aware answers.</summary>
public record ChatUserContext(string? Name, string? Role, string? BookingRole, bool IsAuthenticated)
{
    public static readonly ChatUserContext Anonymous = new(null, null, null, false);

    /// <summary>Studying / booking identity (a promoted admin still books as their original role).</summary>
    public string? EffectiveBookingRole =>
        string.IsNullOrWhiteSpace(BookingRole) ? Role : BookingRole;

    public bool CanBook => EffectiveBookingRole is Roles.Student or Roles.Lecturer;
    public bool IsAdmin => Role == Roles.Admin;

    public string Describe() => !IsAuthenticated
        ? "The visitor is NOT signed in."
        : IsAdmin
            ? $"The visitor is signed in as an ADMIN ({Name}).{(CanBook ? $" They also book rooms as {EffectiveBookingRole}." : " They do not book rooms.")}"
            : $"The visitor is signed in as a {EffectiveBookingRole?.ToUpperInvariant()} named {Name}.";
}

/// <summary>
/// 24/7 library assistant powered by the Google Gemini REST API (generateContent).
/// Set "Gemini:ApiKey" in appsettings.json (or env var Gemini__ApiKey). Without a key it answers from a built-in FAQ.
/// It also knows the system itself — how to register, change a password, update a profile picture, use the admin console, etc.
/// </summary>
public class GeminiChatService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<GeminiChatService> _log;

    public GeminiChatService(HttpClient http, IConfiguration config, ILogger<GeminiChatService> log)
    {
        _http = http;
        _config = config;
        _log = log;
    }

    // ================================================================
    // KNOWLEDGE BASE — the library AND the web system
    // ================================================================
    public const string KnowledgeBase = """
        LIBRARY: Perpustakaan Tunku Tun Aminah (PTTA), Universiti Tun Hussein Onn Malaysia (UTHM), Parit Raja, Batu Pahat, Johor.
        OPENING HOURS: Facilities are bookable 8:00 AM – 10:00 PM daily. Counter: Mon–Thu 8:00 AM–10:00 PM; Fri 8:00 AM–12:15 PM and 2:45 PM–10:00 PM; Sat–Sun 9:00 AM–5:00 PM. Closed on public holidays. Extended exam-week hours are announced on the website.

        === THE FACILITY BOOKING SYSTEM (how the website works) ===
        ACCOUNTS:
        - Register: Sign in page → "Create account". Enter full name, UTHM email, role (Student or Lecturer), matric number (students: 2 letters + 6 digits, e.g. AI240119) or staff ID (lecturers), faculty, country code + phone number, and a password. A 6-digit verification code is emailed to the address — enter it to activate the account. Accounts are inactive until verified.
        - Sign in: use your UTHM email, your matric number, or your staff ID, together with your password.
        - Forgot password: on the Sign in page click "Forgot password?" → enter your email → a 6-digit reset code is emailed → confirm the code → set a new password. Codes expire after 10 minutes and allow 5 attempts; after that request a new code.
        - Change password while signed in: My Profile → "Change password" tab → current password, new password (minimum 6 characters) and confirm. The new password must differ from the current one. A "Password changed" notification is added to your bell.
        - Update profile picture: My Profile → "Profile details" tab → click the round avatar (or "Change photo") → a crop box opens → drag to position and zoom → Save → the picture is cropped square and saved as your avatar everywhere (navbar, admin user list). Accepted formats JPG / PNG / WebP, maximum 2 MB. You can also "Remove photo" to go back to your initials.
        - Edit your details: My Profile → Profile details tab → change your display name and phone number → Save.
        - Sign out: the user chip at the top right → "Sign out".
        NAVIGATION: Home, Facilities (browse and filter rooms), Dashboard (your summary), My Bookings (All / Upcoming / Pending / Past tabs), and for admins the Admin Console. The bell icon shows your notifications — "Mark all read" clears them.
        BOOKING A ROOM: Facilities → pick a room card (filter by type, search, or minimum capacity) → open the room → choose the date, drag the timeline or type a start and end time between 08:00 and 22:00, add purpose and number of attendees → "Submit for approval" or "Reserve". You can also change the date directly on the room page.
        BOOKING STATUSES: Confirmed (instant), Pending (waiting for an administrator), Approved, Rejected, Cancelled. Track and cancel everything under My Bookings.
        PROOF OF PAYMENT: paid or proof-required bookings must have a receipt uploaded within 24 hours or the request expires. Open the booking → "Upload proof" → attach a JPG, PNG or PDF (max 5 MB) → Submit. Administrators verify it and mark the booking paid.
        KEY RETURN (Bilik Iqra' only): after the one-month carrel rental ends you must return the room key to the PTTA counter. A countdown chip on the booking shows the deadline. Until the key is collected you cannot book any facility again — the counter marks it collected on the Admin Console → Key returns.
        FEEDBACK: the "Feedback" link (or your user menu) opens the form. Choose Report a problem / Suggestion / Question, pick an optional room, write the subject and description, attach a photo (JPG/PNG/WebP, max 5 MB) and submit. Track replies under the same page; administrators answer in the Admin Console → Feedback.
        ROLES AND ACCESS: Students can book Bilik Iqra' (study carrels, RM20/month) and the meeting rooms (Lestari Lounge, Bilik Eksplorasi, Bilik Hikmah). Lecturers can book every room type, including the tutorial rooms (Bilik Lestari 1 & 2), Auditorium AI-Jazari, Bilik Tutorial, Makmal A.I. and the Remote Pilot Smart Classroom — those are lecturers only.
        ADMIN CONSOLE (admin accounts only): Overview (dashboard + charts), Bookings & Approvals (approve/reject, mark payment received), Rooms (add/edit rooms, set maintenance), Booking Rules (per-category limits: max hours per booking, per week, max active bookings), Users (filter, disable/activate, delete, add another admin by email), Announcements (scrolling banner), Feedback (reply and close), Walk-in booking (book on behalf of a student or lecturer), and Key returns (collect returned Bilik Iqra' keys + export a PDF report).
        BECOMING AN ADMIN: only an existing admin can grant admin rights, and only by entering an email address. If that email already belongs to a student or lecturer, their account is promoted and they keep their password and their booking permissions (they still book rooms as their original role). Otherwise a new admin account is created and a temporary password is emailed to the address.

        === FACILITY RULES ===
        ROOMS: Study — Bilik Iqra' (118 reading carrels, ARAS 2 & 4). Meeting — Lestari Lounge (20 pax), Bilik Eksplorasi (6–10 pax), Bilik Hikmah (4–6 pax). Tutorial — Bilik Lestari 1 and Bilik Lestari 2 (80 pax each) and Bilik Tutorial. Hall — Auditorium AI-Jazari (150 pax). Technology — Makmal A.I. UTHM (30–40 pax) and Remote Pilot Smart Classroom (20–30 pax).
        - Study Rooms (Bilik Iqra'): students and lecturers, RM20 for a one-month carrel rental. Pay at the PTTA Level 1 counter and upload the receipt (see the Room Iqra' payment guide).
        - Meeting Rooms (Lestari Lounge, Bilik Eksplorasi, Bilik Hikmah): students and lecturers, free, needs administrator approval (usually within 1 working day).
        - Tutorial Rooms (Bilik Lestari 1 & 2, Bilik Tutorial), Hall (Auditorium AI-Jazari) and Technology Rooms (Makmal A.I. UTHM, Remote Pilot Smart Classroom): lecturers only, free, needs admin approval.
        HOW TO BOOK: Log in with email / matric number (students) / staff ID (lecturers) → Facilities → choose a room → pick a date and custom start/end time within 08:00–22:00 → purpose + attendees → Submit. Bookings cannot overlap, cannot be in the past and cannot exceed room capacity. Up to 60 days in advance.
        STATUS: Confirmed (study rooms), Pending → Approved/Rejected (other rooms). You receive a notification for every decision. Track everything in My Bookings.
        CANCELLATION: Cancel from My Bookings any time before the session. Once you have uploaded proof of payment you can no longer cancel it yourself — contact the PTTA counter. Repeated no-shows may suspend booking privileges.
        CONDUCT: Keep rooms clean, no food (closed water bottles allowed), keep noise low, vacate on time. Lost & found at the Level 1 counter.
        WIFI: Connect to "UTHM-WiFi" or eduroam using UTHM email credentials.
        CONTACT: PTTA counter 07-453 3333, ptta@uthm.edu.my.
        """;

    private static string SystemPrompt(ChatUserContext ctx) =>
        "You are 'PTTA Assist', the friendly 24/7 virtual assistant of UTHM Library Space, the facility booking system of " +
        "Perpustakaan Tunku Tun Aminah.\n" +
        "You answer two kinds of question: (a) about the library and its facilities, and (b) how to USE this website — " +
        "accounts, changing a password, updating a profile picture, booking, proof of payment, returning keys, feedback, " +
        "and what admins can do. For (b) always give the exact menu path, e.g. \"My Profile → Change password\".\n" +
        "Answer concisely (under 140 words) in the user's language (English or Bahasa Melayu), using **bold** for key facts. " +
        "Number the steps when explaining a procedure. Never invent features: only use the knowledge base below. " +
        "If the answer is not covered, say so and refer the user to the PTTA counter (07-453 3333).\n" +
        ctx.Describe() + "\n\nKNOWLEDGE BASE:\n" + KnowledgeBase;

    // ================================================================
    // FALLBACK (used when no Gemini key, or Gemini fails)
    // ================================================================
    private static readonly (string[] Keys, string Answer)[] Faq =
    {
        // ---------- Account ----------
        (new[] { "forgot password", "forgot my password", "forgot the password", "reset password", "reset my password",
                 "lupa password", "cannot log in", "can't log in", "cant log in", "sign in problem",
                 "lost password", "password reset", "recover my account", "cannot remember my password" },
            "**Forgot your password?**\n1. On the Sign in page click **\"Forgot password?\"**\n2. Enter your email address\n3. Enter the **6-digit code** we email you (valid **10 minutes**, max **5 attempts**)\n4. Set a **new password**\n\nIf the code expires, just request a new one."),
        (new[] { "change password", "change my password", "update password", "tukar password", "new password", "password change" },
            "**Change your password (while signed in):**\n1. Open **My Profile** (user chip, top right)\n2. Switch to the **\"Change password\"** tab\n3. Enter your **current** password, a **new password** (min. 6 characters) and **confirm** it\n4. Save\n\nThe new password must be different from the old one."),
        (new[] { "profile picture", "profile photo", "change photo", "update photo", "picture", "avatar", "gambar", "foto", "photo" },
            "**Update your profile picture:**\n1. Go to **My Profile** → **Profile details** tab\n2. Click your round **avatar** (or *Change photo*)\n3. A **crop box** opens — drag to position, scroll/pinch to zoom\n4. Click **Save**\n\nIt is cropped square and used everywhere (navbar, admin user list). **JPG / PNG / WebP, max 2 MB.** You can also *Remove photo*."),
        (new[] { "edit profile", "update profile", "edit my details", "change my name", "update phone", "phone number", "change phone", "edit name" },
            "**Edit your details:** **My Profile** → **Profile details** tab → change your **display name** or **phone number** → **Save**. Your name shows on every booking and in the admin user list."),
        (new[] { "register", "sign up", "create account", "new account", "daftar", "akaun" },
            "**Create an account:** click **Create account** → full name, UTHM email, role (Student/Lecturer), matric number (2 letters + 6 digits) or staff ID, faculty, country code + phone, and a password → we email a **6-digit verification code** → enter it to activate. The account stays inactive until verified."),
        (new[] { "login", "log in", "sign in", "masuk", "signing in" },
            "**Sign in** with your **UTHM email**, **matric number** or **staff ID** plus your password. Forgot it? Use **Forgot password?** on the sign-in page."),
        (new[] { "account disabled", "disabled account", "account blocked", "akaun disables", "not able to log in" },
            "If your account shows as **disabled**, an administrator has switched it off. Contact the PTTA counter (07-453 3333) to have it reactivated."),

        // ---------- Admin console ----------
        (new[] { "add admin", "another admin", "make admin", "promote", "grant admin", "admin rights", "new admin" },
            "**Only an admin can grant admin rights**, and only by entering an **email address**:\n1. **Admin Console → Users**\n2. Type the email in **\"Add another admin\"** → **Grant admin access**\n3. If that email is already a student/lecturer, their account is **promoted** and keeps its password *and* its booking permissions\n4. Otherwise a new account is created and a **temporary password is emailed** to the address\n\nYou can also click **Make admin** on any user row."),
        (new[] { "admin console", "admin panel", "what can admin do", "administrator do", "admin features", "how does admin" },
            "**Admin Console** sections:\n- **Overview** — dashboard and charts\n- **Bookings & Approvals** — approve/reject, mark payment received\n- **Rooms** — add/edit rooms, set maintenance\n- **Booking Rules** — per-category limits (hours per booking/week, max active bookings)\n- **Users** — filter, disable/activate, delete, add another admin by email\n- **Announcements** — the scrolling banner\n- **Feedback** — reply and close reports\n- **Walk-in booking** — book on behalf of someone\n- **Key returns** — collect Bilik Iqra' keys, export PDF"),
        (new[] { "walk in", "walk-in", "book on behalf", "on behalf" },
            "**Walk-in booking:** **Admin Console → Walk-in booking** → choose the room, date and time, then pick the student or lecturer it is for. Useful for bookings made at the counter."),
        (new[] { "booking rules", "booking limit", "limits", "max hours", "max active", "rule settings", "booking policy" },
            "**Booking Rules** (Admin Console → Booking Rules) sets per-category limits: **max hours per booking**, **max hours per week**, and **max active bookings per user**. Leave a field empty for no limit."),

        // ---------- Keys ----------
        (new[] { "key", "kunci", "return key", "room key" },
            "**Returning a Bilik Iqra' key:** your one-month carrel rental includes a room key. When the rental ends, a countdown chip on **My Bookings** shows the deadline — hand the key back at the **PTTA Level 1 counter**. An administrator ticks it off under **Admin Console → Key returns**.\n\n⚠️ Until the key is collected you **cannot book any facility again**."),

        // ---------- Proof of payment ----------
        (new[] { "proof", "receipt", "upload payment", "bukti bayaran", "resit", "upload receipt", "payment proof" },
            "**Uploading proof of payment:** open the booking under **My Bookings** → **Upload proof** → attach your receipt (**JPG, PNG or PDF, max 5 MB**) → **Submit**. You have **24 hours** before the request expires. Administrators verify it and mark the booking **Paid**."),

        // ---------- Bookings ----------
        (new[] { "book", "reserve", "tempah", "caranya tempah" },
            "**To book a room:**\n1. **Facilities** → pick a room (filter by type, search, or capacity)\n2. Open the room → choose the **date**\n3. Set **start/end time** between **08:00–22:00** (or drag the timeline)\n4. Add **purpose** and **attendees**\n5. **Submit**\n\nTrack it under **My Bookings**."),
        (new[] { "cancel", "batal" },
            "**Cancelling:** open **My Bookings** → **Cancel** on the booking. You can cancel any time before the session — but once you have uploaded proof of payment it must be cancelled by staff at the counter."),
        (new[] { "approve", "approval", "pending", "status", "lulus", "how long" },
            "**Statuses:** study rooms are **confirmed instantly**; other rooms show **Pending** until an administrator approves or rejects them (usually within **1 working day**). You get a notification either way — check the **bell icon**."),
        (new[] { "my bookings", "my booking", "booking history", "tempahan saya" },
            "**My Bookings** has four tabs — **All**, **Upcoming**, **Pending** and **Past**. From there you can upload proof of payment, see the approval status and cancel a booking."),

        // ---------- Feedback / notifications ----------
        (new[] { "feedback", "report", "suggestion", "question", "complain", "maklum balas", "aduan", "report a problem" },
            "**Sending feedback:** click **Feedback** in the menu → choose **Report a problem / Suggestion / Question**, optionally pick a room, write the **subject** and **description**, attach a photo (max 5 MB) → **Submit**. Track replies on the same page; administrators answer under **Admin Console → Feedback**."),
        (new[] { "notification", "bell", "alert", "notifikasi", "notis" },
            "The **bell icon** in the navbar shows your notifications — booking decisions, payment reminders, key reminders and password changes. Use **Mark all read** to clear them."),

        // ---------- Rooms / fees ----------
        (new[] { "study", "fee", "pay", "rm20", "price", "cost", "bayar", "harga", "iqra", "karel" },
            "**Bilik Iqra'** reading carrels are rented for **one month at RM20** — 118 carrels across **ARAS 2 & 4**. Pay at the **PTTA Level 1 counter** and upload your receipt. You also get a **room key** to return when the rental ends."),
        (new[] { "meeting", "mesyuarat" },
            "**Meeting Rooms** (Lestari Lounge, Bilik Eksplorasi, Bilik Hikmah) are **free** for students and lecturers but need **administrator approval** — usually within one working day."),
        (new[] { "tutorial", "hall", "dewan", "technology", "tech", "lab", "vr", "lecturer", "pensyarah", "auditorium",
                 "which rooms", "which room", "rooms can i book", "rooms can students", "students book",
                 "can students book", "room access", "who can book", "eligible" },
            "**Lecturers can book every room type.** Tutorial Rooms, the Auditorium and the Technology Rooms are **lecturers only** — free and need administrator approval. Students can book Bilik Iqra' and the Meeting Rooms."),
        (new[] { "hour", "open", "close", "masa", "buka", "tutup", "waktu" },
            "PTTA facilities can be booked daily from **8:00 AM to 10:00 PM**. The counter opens Mon–Thu 8 AM–10 PM, Fri 8 AM–12:15 PM & 2:45–10 PM, and weekends 9 AM–5 PM (closed on public holidays)."),
        (new[] { "wifi", "internet", "eduroam" },
            "Connect to **UTHM-WiFi** or **eduroam** using your UTHM email credentials."),
        (new[] { "food", "eat", "drink", "rule", "noise", "makan", "peraturan", "conduct" },
            "Room rules: **no food** (closed water bottles are fine), keep noise low, keep the room tidy and **vacate on time**."),
        (new[] { "contact", "phone", "email", "help", "lost", "hubungi", "counter" },
            "Reach the PTTA counter at **07-453 3333** or **ptta@uthm.edu.my**. Lost & found is at the Level 1 counter."),
    };

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_config["Gemini:ApiKey"]);

    public async Task<(string Reply, string Source)> AskAsync(
        string message,
        IReadOnlyList<ChatTurn> history,
        ChatUserContext? context = null)
    {
        var ctx = context ?? ChatUserContext.Anonymous;
        var key = _config["Gemini:ApiKey"];
        var model = _config["Gemini:Model"] ?? "gemini-2.0-flash";
        if (!string.IsNullOrWhiteSpace(key))
        {
            try
            {
                var contents = history.TakeLast(10)
                    .Select(h => (object)new
                    {
                        role = h.Role == "user" ? "user" : "model",
                        parts = new[] { new { text = h.Text } }
                    })
                    .Append(new { role = "user", parts = new[] { new { text = message } } })
                    .ToList();

                var body = new
                {
                    system_instruction = new { parts = new[] { new { text = SystemPrompt(ctx) } } },
                    contents,
                    generationConfig = new { temperature = 0.3, maxOutputTokens = 900 },
                };

                using var req = new HttpRequestMessage(HttpMethod.Post,
                    $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent")
                {
                    Content = JsonContent.Create(body)
                };
                req.Headers.Add("x-goog-api-key", key);
                using var res = await _http.SendAsync(req);
                var json = await res.Content.ReadAsStringAsync();
                if (!res.IsSuccessStatusCode)
                    throw new HttpRequestException($"Gemini {(int)res.StatusCode}: {json}");

                using var doc = JsonDocument.Parse(json);
                var parts = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts");

                var text = string.Concat(parts.EnumerateArray()
                    .Select(p => p.TryGetProperty("text", out var t) ? t.GetString() : ""));

                if (!string.IsNullOrWhiteSpace(text))
                    return (text.Trim(), "gemini");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Gemini request failed — falling back to FAQ");
            }
        }
        return (FaqAnswer(message), "faq");
    }

    /// <summary>Best-match FAQ lookup. Longer, more specific keywords score higher so
    /// \"how do I change my password\" never gets answered with the generic booking article.</summary>
    public static string FaqAnswer(string message)
    {
        var t = message.ToLowerInvariant();
        string? best = null;
        var bestScore = 0;

        foreach (var (keys, answer) in Faq)
        {
            var score = 0;
            foreach (var key in keys)
            {
                if (!t.Contains(key)) continue;
                score += key switch
                {
                    var k when k.Contains(' ') => k.Length,      // phrases are very specific
                    var k when k.Length >= 8 => 5,
                    var k when k.Length >= 5 => 3,
                    _ => 1,
                };
            }
            if (score > bestScore)
            {
                bestScore = score;
                best = answer;
            }
        }

        return best ?? Fallback;
    }

    private const string Fallback =
        "Hi, I'm **PTTA Assist**! I can help with:\n" +
        "- **Using this system** — change password, update profile picture, edit details\n" +
        "- **Booking** — how to book, cancel, approval status, proof of payment, returning the Bilik Iqra' key\n" +
        "- **Facilities** — opening hours, fees, which rooms students or lecturers can book\n" +
        "- **Account** — registering, signing in, feedback and notifications\n\n" +
        "What would you like to know?";
}