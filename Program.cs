using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using UTHMLibrarySpace.Data;
using UTHMLibrarySpace.Infrastructure;
using UTHMLibrarySpace.Models;
using UTHMLibrarySpace.Services;
using Microsoft.Extensions.Logging;

QuestPDF.Settings.License = LicenseType.Community;

// Set a custom TEMP directory for ASP.NET Core before the builder starts
var customTemp = Path.Combine(Directory.GetCurrentDirectory(), "temp");
if (!Directory.Exists(customTemp))
    Directory.CreateDirectory(customTemp);
Environment.SetEnvironmentVariable("TEMP", customTemp);
Environment.SetEnvironmentVariable("TMP", customTemp);


var builder = WebApplication.CreateBuilder(args);
// ✅ Remove Windows Event Log provider (requires admin permissions on Windows)
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/AccessDenied";
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
        o.SlidingExpiration = true;
        o.Cookie.Name = "UTHMLibrarySpace.Auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicies.CanBook, p => p
        .RequireAuthenticatedUser()
        // Admins qualify too when they were promoted from a student/lecturer role — the BookingRole
        // claim carries that preserved booking identity.
        .RequireAssertion(ctx => ctx.User.IsInRole(Roles.Student)
                              || ctx.User.IsInRole(Roles.Lecturer)
                              || ctx.User.HasClaim(UserClaims.BookingRole, Roles.Student)
                              || ctx.User.HasClaim(UserClaims.BookingRole, Roles.Lecturer)));
builder.Services.AddScoped<EmailService>();
builder.Services.AddHttpClient<GeminiChatService>(c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<PdfReportService>();

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

// Create the SQLite database and seed demo data on first run.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    DbSeeder.Seed(db);
}

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();