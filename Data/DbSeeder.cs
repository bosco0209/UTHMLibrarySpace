using Microsoft.AspNetCore.Identity;
using UTHMLibrarySpace.Models;
using UTHMLibrarySpace.Services;

namespace UTHMLibrarySpace.Data;

/// <summary>Seeds demo accounts, facilities, bookings and announcements on an empty database.</summary>
public static class DbSeeder
{
    private const string ImgStudy1 = "https://images.unsplash.com/photo-1759732419233-5b84c4cb5a9c?auto=format&fit=crop&w=1200&q=80";
    private const string ImgStudy2 = "https://images.pexels.com/photos/9572540/pexels-photo-9572540.jpeg?auto=compress&cs=tinysrgb&w=1200";
    private const string ImgStudy3 = "https://images.unsplash.com/photo-1739918075668-fc7844c6d921?auto=format&fit=crop&w=1200&q=80";
    private const string ImgStudy4 = "https://images.pexels.com/photos/6549372/pexels-photo-6549372.jpeg?auto=compress&cs=tinysrgb&w=1200";
    private const string ImgMeet1 = "https://images.unsplash.com/photo-1628062699790-7c45262b82b4?auto=format&fit=crop&w=1200&q=80";
    private const string ImgMeet2 = "https://images.unsplash.com/photo-1497366811353-6870744d04b2?auto=format&fit=crop&w=1200&q=80";
    private const string ImgTut1 = "https://images.unsplash.com/photo-1541829070764-84a7d30dd3f3?auto=format&fit=crop&w=1200&q=80";
    private const string ImgTut2 = "https://images.unsplash.com/photo-1718327453695-4d32b94c90a4?auto=format&fit=crop&w=1200&q=80";
    private const string ImgHall1 = "https://images.unsplash.com/photo-1763706320063-b210731b37a1?auto=format&fit=crop&w=1200&q=80";
    private const string ImgHall2 = "https://images.unsplash.com/photo-1778876091264-1446d649156c?auto=format&fit=crop&w=1200&q=80";
    private const string ImgTech1 = "https://images.unsplash.com/photo-1606761568499-6d2451b23c66?auto=format&fit=crop&w=1200&q=80";
    private const string ImgTech2 = "https://images.unsplash.com/photo-1776397409772-6202fe558465?auto=format&fit=crop&w=1200&q=80";

    public static void Seed(AppDbContext db)
    {
        if (db.Users.Any()) return;

        var hasher = new PasswordHasher<User>();
        User U(string name, string email, string id, string role, string faculty, string pw)
        {
            var u = new User { Name = name, Email = email, IdNumber = id, Role = role, Faculty = faculty };
            u.PasswordHash = hasher.HashPassword(u, pw);
            return u;
        }

        var admin = U("Puan Zubaidah binti Salleh", "admin.ptta@uthm.edu.my", "ADM001", Roles.Admin, "Perpustakaan Tunku Tun Aminah", "Admin@123");
        var student = U("Nurul Izzah binti Ahmad", "ai210045@siswa.uthm.edu.my", "AI210045", Roles.Student, "FSKTM", "Student@123");
        var chie = U("Chie Wai Lun", "ai240119@siswa.uthm.edu.my", "AI240119", Roles.Student, "FSKTM", "Student@123");
        var lecturer = U("Dr. Farhan bin Mansor", "dr.farhan@uthm.edu.my", "STF8842", Roles.Lecturer, "FSKTM", "Lecturer@123");
        var tay = U("Dr. Tay Xin Hui", "tayxh@uthm.edu.my", "STF5120", Roles.Lecturer, "FSKTM", "Lecturer@123");
        db.Users.AddRange(admin, student, chie, lecturer, tay);

        Facility F(string code, string name, FacilityType t, int cap, string loc, string desc, string amen, string img,
            FacilityStatus st = FacilityStatus.Available, int? capMin = null, int units = 1, string typeName = "") =>
            new() { Code = code, Name = name, Type = t, Capacity = cap, CapacityMin = capMin, Units = units, Location = loc, TypeName = typeName, Description = desc, Amenities = amen, ImageUrl = img, Status = st };

        // The nine real PTTA rooms. Capacity = maximum pax; CapacityMin = the lower bound shown on the card
        // ("Kapasiti: 30 - 40 Orang"); Units = how many bookable units the room type holds (display only).
        var facilities = new List<Facility>
        {
            F("SR-01", "Bilik Iqra'", FacilityType.BilikIqra, 1, "ARAS 2 & 4, PTTA",
                "Karel Pembacaan Khusus — ruang bacaan karel persendirian (RM20/bulan). 118 karel disediakan.",
                "Karel persendirian,Lampu meja,Plug kuasa,WiFi,Berhawa dingin", ImgStudy4, units: 118, typeName: "Karel Pembacaan Khusus"),
            F("MR-01", "Lestari Lounge", FacilityType.LestariLounge, 20, "ARAS 3, PTTA",
                "Ruang Santai & Perbincangan — ruang santai eksklusif pembelajaran kolaboratif.",
                "Sofa santai,Skrin paparan,WiFi,Berhawa dingin", ImgMeet2, typeName: "Ruang Santai & Perbincangan"),
            F("MR-02", "Bilik Eksplorasi", FacilityType.BilikEksplorasi, 10, "ARAS 3, PTTA",
                "Bilik Perbincangan Mini Seminar — sesuai untuk perbincangan kumpulan berskala sederhana.",
                "Skrin paparan,Papan putih,Mikrofon,WiFi,Berhawa dingin", ImgMeet1, capMin: 6, units: 20, typeName: "Bilik Perbincangan Mini Seminar"),
            F("MR-03", "Bilik Hikmah", FacilityType.BilikHikmah, 6, "ARAS 2, PTTA",
                "Bilik Perbincangan Kumpulan — dilengkapi skrin paparan & papan putih.",
                "Skrin paparan,Papan putih,Plug kuasa,WiFi,Berhawa dingin", ImgStudy2, capMin: 4, units: 20, typeName: "Bilik Perbincangan Kumpulan"),
            F("TR-01", "Bilik Lestari 1", FacilityType.BilikLestari, 80, "ARAS 3, PTTA",
                "Bilik Seminar Utama — bilik seminar rasmi untuk bengkel & taklimat.",
                "Projektor,Mikrofon,Sistem bunyi,Papan putih,WiFi,Berhawa dingin", ImgTut1, typeName: "Bilik Seminar Utama"),
            F("TR-02", "Bilik Lestari 2", FacilityType.BilikLestari, 80, "ARAS 3, PTTA",
                "Bilik Seminar Utama — bilik seminar serbaguna berhawa dingin.",
                "Projektor,Mikrofon,Papan putih,WiFi,Berhawa dingin", ImgTut2, typeName: "Bilik Seminar Utama"),
            F("HL-01", "Auditorium AI-Jazari", FacilityType.Auditorium, 150, "ARAS 3, PTTA",
                "Auditorium Utama PTTA — dewan persembahan & persidangan berprestij.",
                "Pentas,Projektor dwi,Sistem PA,Mikrofon tanpa wayar,Rakaman", ImgHall1, typeName: "Auditorium Utama PTTA"),
            F("TC-01", "Makmal A.I. UTHM", FacilityType.MakmalAI, 40, "ARAS 2, PTTA",
                "Makmal Komputer & A.I. — stesen kerja berkuasa tinggi khas A.I.",
                "Stesen kerja GPU,Perisian A.I.,Projektor,WiFi,Berhawa dingin", ImgTech1, capMin: 30, typeName: "Makmal Komputer & A.I."),
            F("TC-02", "Remote Pilot Smart Classroom", FacilityType.RemotePilot, 30, "ARAS 2, PTTA",
                "Bilik Pengajaran Pintar — fasiliti pengajaran interaktif & multimedia.",
                "Skrin interaktif,Kamera PTZ,Mikrofon,WiFi,Berhawa dingin", ImgTech2, capMin: 20, typeName: "Bilik Pengajaran Pintar"),
            F("BT-01", "Bilik Tutorial", FacilityType.BilikTutorial, 20, "ARAS 3, PTTA",
                "Bilik Tutorial — bilik tutorial milik pensyarah; hanya boleh ditempah oleh pensyarah.",
                "Papan putih,Projektor,Mikrofon,WiFi,Berhawa dingin", ImgTut1, capMin: 10, typeName: "Bilik Tutorial"),
        };
        db.Facilities.AddRange(facilities);
        db.SaveChanges();

        var today = AppClock.Today;
        var rnd = new Random(42);
        string Ref() => "PTTA-" + new string(Enumerable.Range(0, 6).Select(_ => "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"[rnd.Next(32)]).ToArray());

        Booking B(User u, Facility f, int dayOffset, int sh, int eh, string purpose, int pax, BookingStatus s, string note = "")
        {
            var fee = FacilityRules.For(f.Type).Fee;
            return new Booking
            {
                Reference = Ref(),
                UserId = u.Id,
                FacilityId = f.Id,
                Date = today.AddDays(dayOffset),
                EndDate = today.AddDays(dayOffset),
                StartTime = new TimeOnly(sh, 0),
                EndTime = new TimeOnly(eh, 0),
                Purpose = purpose,
                Attendees = pax,
                Fee = fee,
                PaymentStatus = fee > 0 ? PaymentStatus.Paid : PaymentStatus.None,
                Status = s,
                AdminNote = note,
                CreatedAt = DateTime.UtcNow.AddDays(Math.Min(dayOffset, 0) - 1),
            };
        }

        Facility Fc(string code) => facilities.First(f => f.Code == code);

        // Bilik Iqra' carrels: one-month rental from the start date, flat RM20.
        Booking R(User u, Facility f, int startOffset, string purpose, int pax, BookingStatus s)
        {
            var b = B(u, f, startOffset, 8, 22, purpose, pax, s);
            b.EndDate = FacilityRules.RentalEnd(b.Date);
            if (s == BookingStatus.Cancelled) b.PaymentStatus = PaymentStatus.None;
            return b;
        }

        db.Bookings.AddRange(
            R(student, Fc("SR-01"), -5, "Quiet carrel for final revision", 1, BookingStatus.Confirmed),
            R(chie, Fc("SR-01"), -40, "PSM 1 writing carrel", 1, BookingStatus.Cancelled),
            B(student, Fc("MR-01"), 2, 15, 17, "Computer Science Society committee meeting", 8, BookingStatus.Pending),
            B(chie, Fc("MR-02"), 3, 14, 16, "PSM supervisor consultation", 3, BookingStatus.Pending),
            B(chie, Fc("MR-03"), 1, 13, 15, "Course project discussion", 4, BookingStatus.Pending),
            B(lecturer, Fc("HL-01"), 5, 9, 13, "Industry talk: AI in Manufacturing", 150, BookingStatus.Pending),
            B(lecturer, Fc("TR-01"), 1, 8, 10, "BIT33603 replacement tutorial", 28, BookingStatus.Approved),
            B(tay, Fc("TC-01"), 4, 10, 12, "Multimedia workshop for final-year students", 18, BookingStatus.Pending),
            B(tay, Fc("TC-02"), 6, 9, 11, "Smart classroom teaching session", 25, BookingStatus.Approved),
            B(tay, Fc("TR-02"), -1, 14, 16, "PSM briefing session", 22, BookingStatus.Approved),
            B(lecturer, Fc("HL-01"), -2, 9, 11, "Faculty town hall", 100, BookingStatus.Rejected, "Auditorium reserved for convocation rehearsal."),
            B(tay, Fc("MR-01"), -5, 11, 12, "Research group meeting", 6, BookingStatus.Approved),
            B(student, Fc("MR-02"), -3, 10, 12, "FYP group discussion", 5, BookingStatus.Approved),
            B(lecturer, Fc("TR-02"), 0, 15, 17, "Consultation hour", 12, BookingStatus.Approved)
        );

        db.CategoryLimits.AddRange(
            new CategoryLimit { Type = FacilityType.BilikIqra, MaxActiveBookings = 1 },
            new CategoryLimit { Type = FacilityType.LestariLounge, MaxHoursPerBooking = 3, MaxHoursPerWeek = 6, MaxActiveBookings = 3 },
            new CategoryLimit { Type = FacilityType.BilikEksplorasi, MaxHoursPerBooking = 3, MaxHoursPerWeek = 6, MaxActiveBookings = 3 },
            new CategoryLimit { Type = FacilityType.BilikHikmah, MaxHoursPerBooking = 3, MaxHoursPerWeek = 6, MaxActiveBookings = 3 },
            new CategoryLimit { Type = FacilityType.BilikLestari, MaxHoursPerBooking = 4, MaxHoursPerWeek = 12, MaxActiveBookings = 5 },
            new CategoryLimit { Type = FacilityType.Auditorium, MaxHoursPerBooking = 8, MaxHoursPerWeek = 16, MaxActiveBookings = 3 },
            new CategoryLimit { Type = FacilityType.MakmalAI, MaxHoursPerBooking = 4, MaxHoursPerWeek = 12, MaxActiveBookings = 4 },
            new CategoryLimit { Type = FacilityType.RemotePilot, MaxHoursPerBooking = 4, MaxHoursPerWeek = 12, MaxActiveBookings = 4 },
            new CategoryLimit { Type = FacilityType.BilikTutorial, MaxHoursPerBooking = 4, MaxHoursPerWeek = 12, MaxActiveBookings = 4 }
        );

        db.Announcements.AddRange(
            new Announcement { Title = "Extended hours for Final Examination week", Message = "PTTA facilities are bookable until 11:59 PM from 15–28 June. Good luck with your exams!", Kind = AnnouncementKind.Event },
            new Announcement { Title = "Nine PTTA rooms are now bookable online", Message = "Makmal A.I. UTHM, Bilik Lestari 1 & 2, Lestari Lounge, Bilik Eksplorasi, Auditorium AI-Jazari, Bilik Hikmah, Bilik Iqra' and Remote Pilot Smart Classroom.", Kind = AnnouncementKind.Info },
            new Announcement { Title = "Bilik Iqra' carrel rental", Message = "118 reading carrels at ARAS 2 & 4 are rented for one month at RM20.00. Payment is verified at the PTTA counter.", Kind = AnnouncementKind.Info }
        );

        db.Notifications.AddRange(
            new Notification { UserId = student.Id, Title = "Carrel rented", Message = "Bilik Iqra' is yours for 30 days. Please pay RM20.00 at the PTTA counter." },
            new Notification { UserId = lecturer.Id, Title = "Booking approved", Message = "Bilik Lestari 1 tomorrow 08:00–10:00 was approved." },
            new Notification { UserId = lecturer.Id, Title = "Booking rejected", Message = "Auditorium AI-Jazari was rejected. Note: Auditorium reserved for convocation rehearsal.", IsRead = true }
        );
        db.SaveChanges();
    }
}
