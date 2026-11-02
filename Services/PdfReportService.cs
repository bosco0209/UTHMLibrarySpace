using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using UTHMLibrarySpace.Models;

namespace UTHMLibrarySpace.Services;

/// <summary>Generates the administrator booking report PDF with QuestPDF.</summary>
public class PdfReportService
{
    private const string Navy = "#0B192C";
    private const string Gold = "#D97706";

    public byte[] BookingReport(IReadOnlyList<Booking> bookings, string filterText)
    {
        var active = bookings.Where(b => b.Status is BookingStatus.Confirmed or BookingStatus.Approved).ToList();
        var collected = bookings.Where(b => b.PaymentStatus == PaymentStatus.Paid).Sum(b => b.Fee);
        var outstanding = bookings.Where(b => b.PaymentStatus is PaymentStatus.PayAtCounter or PaymentStatus.AwaitingProof or PaymentStatus.ProofSubmitted && b.IsActive).Sum(b => b.Fee);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor("#0F172A"));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("UTHM LIBRARY SPACE").FontSize(9).Bold().FontColor(Gold).LetterSpacing(0.15f);
                            c.Item().Text("Facility Booking Report").FontSize(20).Bold().FontColor(Navy);
                            c.Item().Text("Perpustakaan Tunku Tun Aminah (PTTA), Universiti Tun Hussein Onn Malaysia").FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(200).AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text($"Generated: {AppClock.Now:dd MMM yyyy, HH:mm}").FontColor(Colors.Grey.Darken1);
                            c.Item().AlignRight().Text($"Filter: {filterText}").FontColor(Colors.Grey.Darken1);
                            c.Item().AlignRight().Text($"Records: {bookings.Count}").Bold();
                        });
                    });
                    col.Item().PaddingVertical(8).LineHorizontal(1.5f).LineColor(Gold);
                    col.Item().PaddingBottom(8).Row(r =>
                    {
                        void Stat(string label, string value)
                        {
                            r.RelativeItem().Background("#F1F5F9").Padding(8).Column(s =>
                            {
                                s.Item().Text(label.ToUpperInvariant()).FontSize(7).FontColor(Colors.Grey.Darken1).LetterSpacing(0.1f);
                                s.Item().Text(value).FontSize(13).Bold().FontColor(Navy);
                            });
                            r.ConstantItem(8);
                        }
                        Stat("Total bookings", bookings.Count.ToString());
                        Stat("Active (confirmed/approved)", active.Count.ToString());
                        Stat("Pending approval", bookings.Count(b => b.Status == BookingStatus.Pending).ToString());
                        Stat("Fees collected", $"RM {collected:0.00}");
                        Stat("Fees outstanding", $"RM {outstanding:0.00}");
                    });
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(78); c.ConstantColumn(62); c.ConstantColumn(62); c.RelativeColumn(2.2f);
                        c.RelativeColumn(2.2f); c.ConstantColumn(52); c.ConstantColumn(30); c.ConstantColumn(70); c.ConstantColumn(62);
                    });
                    table.Header(h =>
                    {
                        foreach (var title in new[] { "Reference", "Date", "Time", "Facility", "Booked by", "Role", "Pax", "Fee", "Status" })
                            h.Cell().Background(Navy).PaddingVertical(5).PaddingHorizontal(4).Text(title).FontColor(Colors.White).Bold().FontSize(8);
                    });
                    var i = 0;
                    foreach (var b in bookings)
                    {
                        string bg = i++ % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
                        IContainer Cell(IContainer c) => c.Background(bg).BorderBottom(0.5f).BorderColor("#E2E8F0").PaddingVertical(4).PaddingHorizontal(4);
                        table.Cell().Element(Cell).Text(b.Reference).FontFamily(Fonts.CourierNew);
                        table.Cell().Element(Cell).Text(b.Date.ToString("dd MMM yy"));
                        table.Cell().Element(Cell).Text($"{b.StartTime:HH\\:mm}-{b.EndTime:HH\\:mm}");
                        table.Cell().Element(Cell).Text(b.Facility?.Name ?? "-");
                        table.Cell().Element(Cell).Text($"{b.User?.Name} ({b.User?.IdNumber})");
                        table.Cell().Element(Cell).Text(b.User?.Role ?? "-");
                        table.Cell().Element(Cell).Text(b.Attendees.ToString());
                        table.Cell().Element(Cell).Text(b.Fee > 0 ? $"RM{b.Fee:0.00} ({b.PaymentText.ToLower()})" : "Free");
                        table.Cell().Element(Cell).Text(b.Status.ToString().ToUpperInvariant()).Bold().FontColor(b.Status switch
                        {
                            BookingStatus.Confirmed or BookingStatus.Approved => "#047857",
                            BookingStatus.Pending => "#B45309",
                            BookingStatus.Rejected => "#BE123C",
                            _ => "#64748B",
                        });
                    }
                });

                page.Footer().Row(r =>
                {
                    r.RelativeItem().Text("UTHM Library Space · Generated automatically by the PTTA facility booking system").FontSize(7).FontColor(Colors.Grey.Medium);
                    r.ConstantItem(80).AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(7).FontColor(Colors.Grey.Medium));
                        t.Span("Page "); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages();
                    });
                });
            });
        }).GeneratePdf();
    }

    /// <summary>Key-return report: students whose Bilik Iqra' rental ended and key not yet collected, at least 1 day overdue.</summary>
    public byte[] KeyReturnReport(IReadOnlyList<Booking> bookings)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor("#0F172A"));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("UTHM LIBRARY SPACE").FontSize(9).Bold().FontColor(Gold).LetterSpacing(0.15f);
                            c.Item().Text("Bilik Iqra' — Key Return Report (overdue)").FontSize(20).Bold().FontColor(Navy);
                            c.Item().Text("Users whose Bilik Iqra' rental ended at least 1 day ago and key is not yet returned.").FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(200).AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text($"Generated: {AppClock.Now:dd MMM yyyy, HH:mm}").FontColor(Colors.Grey.Darken1);
                            c.Item().AlignRight().Text($"Records: {bookings.Count}").Bold();
                        });
                    });
                    col.Item().PaddingVertical(8).LineHorizontal(1.5f).LineColor(Gold);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2.2f); c.RelativeColumn(3f); c.ConstantColumn(90); c.RelativeColumn(1.8f);
                        c.ConstantColumn(85); c.ConstantColumn(95); c.ConstantColumn(60); c.ConstantColumn(60);
                    });
                    table.Header(h =>
                    {
                        foreach (var title in new[] { "Name", "Email", "Phone", "Faculty", "Reference", "Rental period", "Overdue", "Status" })
                            h.Cell().Background(Navy).PaddingVertical(5).PaddingHorizontal(4).Text(title).FontColor(Colors.White).Bold().FontSize(8);
                    });

                    var i = 0;
                    foreach (var b in bookings)
                    {
                        string bg = i++ % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
                        IContainer Cell(IContainer c) => c.Background(bg).BorderBottom(0.5f).BorderColor("#E2E8F0").PaddingVertical(4).PaddingHorizontal(4);
                        var overdueDays = (AppClock.Today.DayNumber - b.EndDate.DayNumber);
                        table.Cell().Element(Cell).Text(b.User?.Name ?? "-");
                        table.Cell().Element(Cell).Text(b.User?.Email ?? "-");
                        table.Cell().Element(Cell).Text(string.IsNullOrEmpty(b.User?.Phone) ? "-" : b.User!.Phone);
                        table.Cell().Element(Cell).Text(b.User?.Faculty ?? "-");
                        table.Cell().Element(Cell).Text(b.Reference).FontFamily(Fonts.CourierNew);
                        table.Cell().Element(Cell).Text($"{b.Date:dd MMM yy} – {b.EndDate:dd MMM yy}");
                        table.Cell().Element(Cell).Text($"{overdueDays}d").Bold().FontColor("#BE123C");
                        table.Cell().Element(Cell).Text(b.Status.ToString().ToUpperInvariant());
                    }
                });

                page.Footer().Row(r =>
                {
                    r.RelativeItem().Text("UTHM Library Space · Generated automatically by the PTTA facility booking system").FontSize(7).FontColor(Colors.Grey.Medium);
                    r.ConstantItem(80).AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(7).FontColor(Colors.Grey.Medium));
                        t.Span("Page "); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages();
                    });
                });
            });
        }).GeneratePdf();
    }
}
