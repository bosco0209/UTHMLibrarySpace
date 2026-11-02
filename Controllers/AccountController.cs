using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UTHMLibrarySpace.Data;
using UTHMLibrarySpace.Infrastructure;
using UTHMLibrarySpace.Models;
using UTHMLibrarySpace.Services;

namespace UTHMLibrarySpace.Controllers;

public class AccountController : Controller
{
    private static readonly Regex MatricRegex = new(@"^[A-Z]{2}\d{6}$");
    private readonly AppDbContext _db;
    private readonly PasswordHasher<User> _hasher = new();
    private readonly EmailService _email;
    private readonly string _avatarDir;
    private const long MaxAvatarBytes = 2 * 1024 * 1024;

    public AccountController(AppDbContext db, IWebHostEnvironment env, EmailService email)
    {
        _db = db;
        _email = email;
        _avatarDir = Path.Combine(env.ContentRootPath, "App_Data", "avatars");
    }

    // ---------- LOGIN ----------
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");
        return View(new LoginVm { ReturnUrl = returnUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var ident = vm.Identifier.Trim();
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email == ident.ToLower() || u.IdNumber == ident.ToUpper());

        if (user is null ||
            _hasher.VerifyHashedPassword(user, user.PasswordHash, vm.Password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Invalid credentials. Check your email / ID and password.");
            return View(vm);
        }

        if (!user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "This account has been disabled. Please contact the PTTA counter.");
            return View(vm);
        }

        await SignInAsync(user);
        this.Toast($"Welcome back, {user.Name.Split(' ')[0]}!");

        if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
            return Redirect(vm.ReturnUrl);

        return user.Role == Roles.Admin
            ? RedirectToAction("Index", "Admin")
            : RedirectToAction("Index", "Dashboard");
    }

    // ---------- REGISTER (step 1 — send code) ----------
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");
        return View(new RegisterVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        // Normalize (never null)
        vm.Name = (vm.Name ?? "").Trim();
        vm.Email = (vm.Email ?? "").Trim().ToLowerInvariant();
        vm.IdNumber = (vm.IdNumber ?? "").Trim().ToUpperInvariant();
        vm.Faculty = (vm.Faculty ?? "").Trim();
        vm.CountryCode = (vm.CountryCode ?? "+60").Trim();
        vm.Phone = (vm.Phone ?? "").Trim();

        // Role must be Student or Lecturer
        if (vm.Role != Roles.Student && vm.Role != Roles.Lecturer)
            ModelState.AddModelError(nameof(vm.Role), "Choose Student or Lecturer.");

        // Student-specific rules
        if (vm.Role == Roles.Student)
        {
            if (!MatricRegex.IsMatch(vm.IdNumber))
                ModelState.AddModelError(nameof(vm.IdNumber),
                    "Matric number must look like AI240119 (2 letters + 6 digits).");

            if (!vm.Email.EndsWith("@student.uthm.edu.my", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(vm.Email),
                    "Students must register with a student email (…@student.uthm.edu.my).");
            }
            else
            {
                var emailPrefix = vm.Email.Split('@')[0];
                if (!string.Equals(vm.IdNumber, emailPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(vm.IdNumber),
                        "Matric number must match your student email.");
                    ModelState.AddModelError(nameof(vm.Email),
                        $"Student email prefix must match your matric number ({vm.IdNumber.ToLowerInvariant()}@student.uthm.edu.my).");
                }
            }
        }

        // Lecturer-specific rules
        if (vm.Role == Roles.Lecturer &&
            !vm.Email.EndsWith("@uthm.edu.my", StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(vm.Email),
                "Lecturers must register with a staff email (…@uthm.edu.my).");

        // Duplicates (against real users)
        if (await _db.Users.AnyAsync(u => u.Email == vm.Email || u.IdNumber == vm.IdNumber))
            ModelState.AddModelError(string.Empty, "An account with this email or ID already exists.");

        if (!ModelState.IsValid)
        {
            vm.Password = "";
            vm.ConfirmPassword = "";
            ModelState.Remove(nameof(vm.Password));
            ModelState.Remove(nameof(vm.ConfirmPassword));
            return View(vm);
        }

        // ---- Generate a 6-digit code and store the pending registration ----
        var code = Random.Shared.Next(0, 1_000_000).ToString("D6");

        // Remove any earlier pending rows for this email (only one active at a time)
        var existing = await _db.PendingRegistrations
            .Where(p => p.Email == vm.Email)
            .ToListAsync();
        if (existing.Count > 0)
            _db.PendingRegistrations.RemoveRange(existing);

        var pending = new PendingRegistration
        {
            Email = vm.Email,
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Name = vm.Name,
            IdNumber = vm.IdNumber,
            Role = vm.Role,
            Faculty = vm.Faculty,
            Phone = string.IsNullOrWhiteSpace(vm.Phone) ? "" : $"{vm.CountryCode} {vm.Phone}",
            PasswordHash = _hasher.HashPassword(new User(), vm.Password),
            Attempts = 0,
        };
        _db.PendingRegistrations.Add(pending);
        await _db.SaveChangesAsync();

        // ---- Send the verification email ----
        try
        {
            await _email.SendVerificationCodeAsync(vm.Email, code, vm.Name);
        }
        catch
        {
            // Email sending failed — remove the pending row so the user can try again
            _db.PendingRegistrations.Remove(pending);
            await _db.SaveChangesAsync();
            ModelState.AddModelError(string.Empty,
                "We couldn't send the verification email. Please try again in a moment, or contact the PTTA counter.");
            vm.Password = "";
            vm.ConfirmPassword = "";
            ModelState.Remove(nameof(vm.Password));
            ModelState.Remove(nameof(vm.ConfirmPassword));
            return View(vm);
        }

        this.Toast("We emailed you a 6-digit code. It expires in 10 minutes.", "info");
        return RedirectToAction(nameof(VerifyCode), new { email = vm.Email });
    }

    // ---------- REGISTER (step 2 — verify code) ----------
    [HttpGet]
    public async Task<IActionResult> VerifyCode(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return RedirectToAction(nameof(Register));

        var pending = await _db.PendingRegistrations
            .FirstOrDefaultAsync(p => p.Email == email);
        if (pending is null)
        {
            this.Toast("That registration has expired. Please register again.", "warning");
            return RedirectToAction(nameof(Register));
        }

        return View(new VerifyCodeVm { Email = email });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyCode(VerifyCodeVm vm)
    {
        var pending = await _db.PendingRegistrations
            .FirstOrDefaultAsync(p => p.Email == vm.Email);

        if (pending is null)
        {
            this.Toast("That registration has expired. Please register again.", "danger");
            return RedirectToAction(nameof(Register));
        }

        if (pending.ExpiresAt < DateTime.UtcNow)
        {
            _db.PendingRegistrations.Remove(pending);
            await _db.SaveChangesAsync();
            this.Toast("That code has expired. Please register again.", "danger");
            return RedirectToAction(nameof(Register));
        }

        if (pending.Attempts >= 5)
        {
            _db.PendingRegistrations.Remove(pending);
            await _db.SaveChangesAsync();
            this.Toast("Too many wrong attempts. Please register again.", "danger");
            return RedirectToAction(nameof(Register));
        }

        if (!string.Equals(vm.Code?.Trim(), pending.Code, StringComparison.Ordinal))
        {
            pending.Attempts++;
            await _db.SaveChangesAsync();
            ModelState.AddModelError(string.Empty, "That code is incorrect. Please try again.");
            return View(vm);
        }

        // ---- Code is correct — create the real user ----
        if (await _db.Users.AnyAsync(u => u.Email == pending.Email || u.IdNumber == pending.IdNumber))
        {
            _db.PendingRegistrations.Remove(pending);
            await _db.SaveChangesAsync();
            this.Toast("An account with this email or ID already exists.", "danger");
            return RedirectToAction(nameof(Login));
        }

        var user = new User
        {
            Name = pending.Name,
            Email = pending.Email,
            IdNumber = pending.IdNumber,
            Role = pending.Role,
            Faculty = pending.Faculty,
            Phone = pending.Phone,
            IsActive = true,
            PasswordHash = pending.PasswordHash,
        };
        _db.Users.Add(user);
        _db.PendingRegistrations.Remove(pending);
        await _db.SaveChangesAsync();   // now user.Id is set

        _db.Notifications.Add(new Notification
        {
            UserId = user.Id,
            Title = "Welcome to UTHM Library Space",
            Message = user.Role == Roles.Student
                ? "You can now book Bilik Iqra' carrels (RM20, 1-month rental, instant) and Meeting Rooms (free, needs approval)."
                : "You can now book Meeting, Tutorial, Hall and Technology rooms. Requests are reviewed by PTTA administrators.",
        });
        await _db.SaveChangesAsync();

        await SignInAsync(user);
        this.Toast("Account verified — welcome to UTHM Library Space!");
        return RedirectToAction("Index", "Dashboard");
    }

    // ---------- FORGOT PASSWORD ----------
    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email == vm.Email || u.IdNumber == vm.Email);

        if (user is not null)
        {
            var code = Random.Shared.Next(0, 1_000_000).ToString("D6");
            var stale = await _db.PasswordResetRequests
                .Where(p => p.Email == user.Email).ToListAsync();
            _db.PasswordResetRequests.RemoveRange(stale);
            _db.PasswordResetRequests.Add(new PasswordResetRequest
            {
                Email = user.Email,
                Code = code,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                Verified = false,
            });
            await _db.SaveChangesAsync();

            try
            {
                await _email.SendPasswordResetCodeAsync(user.Email, code, user.Name);
            }
            catch
            {
                ModelState.AddModelError(string.Empty,
                    "We couldn't send the verification email. Please try again in a moment, or contact the PTTA counter.");
                return View(vm);
            }
        }

        // Same response whether or not the account exists (avoid account enumeration)
        this.Toast("If that account exists, a 6-digit verification code has been emailed to it.", "info");
        return RedirectToAction(nameof(ConfirmResetCode), new { email = vm.Email });
    }

    [HttpGet]
    public IActionResult ConfirmResetCode(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return RedirectToAction(nameof(ForgotPassword));
        return View(new VerifyCodeVm { Email = email });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmResetCode(VerifyCodeVm vm)
    {
        var req = await _db.PasswordResetRequests.FirstOrDefaultAsync(p => p.Email == vm.Email);

        if (req is null)
        {
            this.Toast("That reset request has expired. Please request a new code.", "danger");
            return RedirectToAction(nameof(ForgotPassword));
        }

        if (req.ExpiresAt < DateTime.UtcNow)
        {
            _db.PasswordResetRequests.Remove(req);
            await _db.SaveChangesAsync();
            this.Toast("That code has expired. Please request a new code.", "danger");
            return RedirectToAction(nameof(ForgotPassword));
        }

        if (req.Attempts >= 5)
        {
            _db.PasswordResetRequests.Remove(req);
            await _db.SaveChangesAsync();
            this.Toast("Too many wrong attempts. Please request a new code.", "danger");
            return RedirectToAction(nameof(ForgotPassword));
        }

        if (!string.Equals(vm.Code?.Trim(), req.Code, StringComparison.Ordinal))
        {
            req.Attempts++;
            await _db.SaveChangesAsync();
            ModelState.AddModelError(string.Empty, "That code is incorrect. Please try again.");
            return View(vm);
        }

        req.Verified = true;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(ResetPassword), new { email = req.Email });
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return RedirectToAction(nameof(ForgotPassword));

        var req = await _db.PasswordResetRequests.FirstOrDefaultAsync(p => p.Email == email && p.Verified);
        if (req is null)
        {
            this.Toast("Please verify the email code first.", "warning");
            return RedirectToAction(nameof(ForgotPassword));
        }
        return View(new ResetPasswordVm { Email = email });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordVm vm)
    {
        var req = await _db.PasswordResetRequests.FirstOrDefaultAsync(p => p.Email == vm.Email && p.Verified);
        if (req is null)
        {
            this.Toast("Please verify the email code first.", "warning");
            return RedirectToAction(nameof(ForgotPassword));
        }

        if (!ModelState.IsValid) return View(vm);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == vm.Email);
        if (user is null) return RedirectToAction(nameof(Login));

        user.PasswordHash = _hasher.HashPassword(user, vm.NewPassword);
        _db.PasswordResetRequests.Remove(req);
        await _db.SaveChangesAsync();

        this.Toast("Password updated. You can now sign in with your new password.");
        return RedirectToAction(nameof(Login));
    }

    // ---------- PROFILE ----------
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var user = await _db.Users.FindAsync(User.CurrentUserId()!.Value);
        if (user is null) return RedirectToAction(nameof(Login));
        return View(new ProfilePageVm { User = user, Profile = new ProfileVm { Name = user.Name, Phone = user.Phone } });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> UpdateProfile(
        [Bind(Prefix = "Profile")] ProfileVm vm,
        IFormFile? avatar,
        bool removeAvatar = false)
    {
        var user = await _db.Users.FindAsync(User.CurrentUserId()!.Value);
        if (user is null) return RedirectToAction(nameof(Login));

        if (!ModelState.IsValid)
            return View(nameof(Profile), new ProfilePageVm { User = user, Profile = vm });

        if (avatar is { Length: > 0 })
        {
            if (avatar.Length > MaxAvatarBytes)
                ModelState.AddModelError("Profile.Name", "Profile picture is too large. The maximum size is 2 MB.");
            else
            {
                var head = new byte[12];
                await using (var s = avatar.OpenReadStream()) _ = await s.ReadAsync(head);
                var ext = head[0] == 0xFF && head[1] == 0xD8 ? ".jpg"
                    : head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47 ? ".png"
                    : head[0] == 0x52 && head[1] == 0x49 && head[8] == 0x57 && head[9] == 0x45 ? ".webp" : null;

                if (ext is null)
                {
                    ModelState.AddModelError("Profile.Name", "Profile picture must be a JPG, PNG or WebP image.");
                }
                else
                {
                    Directory.CreateDirectory(_avatarDir);
                    DeleteAvatarFile(user);
                    var name = $"u{user.Id}-{Guid.NewGuid():N}{ext}";
                    await using var outStream = System.IO.File.Create(Path.Combine(_avatarDir, name));
                    await avatar.CopyToAsync(outStream);
                    user.AvatarFileName = name;
                }
            }
            if (!ModelState.IsValid)
                return View(nameof(Profile), new ProfilePageVm { User = user, Profile = vm });
        }
        else if (removeAvatar)
        {
            DeleteAvatarFile(user);
            user.AvatarFileName = null;
        }

        user.Name = vm.Name.Trim();
        user.Phone = vm.Phone?.Trim() ?? "";
        await _db.SaveChangesAsync();
        await SignInAsync(user);
        this.Toast("Profile updated.");
        return RedirectToAction(nameof(Profile));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword([Bind(Prefix = "Password")] ChangePasswordVm vm)
    {
        var user = await _db.Users.FindAsync(User.CurrentUserId()!.Value);
        if (user is null) return RedirectToAction(nameof(Login));

        if (ModelState.IsValid &&
            _hasher.VerifyHashedPassword(user, user.PasswordHash, vm.OldPassword) == PasswordVerificationResult.Failed)
            ModelState.AddModelError("Password.OldPassword", "Your current password is incorrect.");

        if (ModelState.IsValid && vm.OldPassword == vm.NewPassword)
            ModelState.AddModelError("Password.NewPassword", "The new password must be different from the current one.");

        if (!ModelState.IsValid)
        {
            ViewData["PasswordTab"] = true;
            return View(nameof(Profile), new ProfilePageVm
            {
                User = user,
                Profile = new ProfileVm { Name = user.Name, Phone = user.Phone },
                Password = new ChangePasswordVm()
            });
        }

        user.PasswordHash = _hasher.HashPassword(user, vm.NewPassword);
        _db.Notifications.Add(new Notification
        {
            UserId = user.Id,
            Title = "Password changed",
            Message = "Your account password was changed. If this wasn't you, contact the PTTA counter immediately."
        });
        await _db.SaveChangesAsync();
        this.Toast("Password changed successfully.");
        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    public async Task<IActionResult> Avatar(int id)
    {
        var user = await _db.Users.FindAsync(id);
        var path = user?.AvatarFileName is { } f ? Path.Combine(_avatarDir, f) : null;
        if (path is null || !System.IO.File.Exists(path)) return NotFound();
        var type = Path.GetExtension(path) switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
        return PhysicalFile(path, type);
    }

    private void DeleteAvatarFile(User user)
    {
        if (user.AvatarFileName is { } f && System.IO.File.Exists(Path.Combine(_avatarDir, f)))
            System.IO.File.Delete(Path.Combine(_avatarDir, f));
    }

    // ---------- LOGOUT ----------
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        this.Toast("You have been signed out.", "info");
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();

    // ---------- HELPERS ----------
    private Task SignInAsync(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(UserClaims.IdNumber, user.IdNumber),
            new(UserClaims.Avatar, user.AvatarFileName is null ? "" : $"/Account/Avatar/{user.Id}?v={user.AvatarFileName.GetHashCode():x}"),
        };

        // An admin who was promoted from a student/lecturer account keeps booking rooms under
        // that original role, so carry the booking identity in its own claim.
        if (user.BookingRole is Roles.Student or Roles.Lecturer)
            claims.Add(new Claim(UserClaims.BookingRole, user.BookingRole));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            claims, CookieAuthenticationDefaults.AuthenticationScheme));

        return HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });
    }
}
