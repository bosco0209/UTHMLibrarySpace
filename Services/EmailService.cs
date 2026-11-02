using System.Net;
using System.Net.Mail;

namespace UTHMLibrarySpace.Services;

public class EmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _log;

    public EmailService(IConfiguration config, ILogger<EmailService> log)
    {
        _config = config;
        _log = log;
    }

    // ================================================================
    // CORE SEND METHOD
    // ================================================================
    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        var host = _config["Email:Host"];
        var port = int.TryParse(_config["Email:Port"], out var p) ? p : 587;
        var user = _config["Email:User"];
        var pass = _config["Email:Password"];
        var from = _config["Email:From"] ?? "no-reply@uthmlibraryspace.local";
        var fromName = _config["Email:FromName"] ?? "UTHM Library Space";

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
        {
            _log.LogWarning("Email not configured. Would have sent to {To}: {Subject}", toEmail, subject);
            _log.LogInformation("Email body:\n{Body}", htmlBody);
            return;
        }

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(user, pass),
        };

        using var msg = new MailMessage
        {
            From = new MailAddress(from, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        msg.To.Add(toEmail);

        try
        {
            await client.SendMailAsync(msg);
            _log.LogInformation("Email sent to {To}: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to send email to {To}", toEmail);
            throw;
        }
    }

    // ================================================================
    // ? VERIFICATION EMAIL (REGISTRATION)
    // ================================================================
    public async Task SendVerificationCodeAsync(string toEmail, string code, string recipientName)
    {
        var html = BuildVerificationEmailHtml(recipientName, code, toEmail);
        await SendAsync(toEmail, $"🔐 Your UTHM Library Space Verification Code: {code}", html);
    }

    // ================================================================
    // ? PASSWORD RESET EMAIL (BONUS)
    // ================================================================
    public async Task SendPasswordResetCodeAsync(string toEmail, string code, string recipientName)
    {
        var html = BuildPasswordResetEmailHtml(recipientName, code, toEmail);
        await SendAsync(toEmail, $"🔑 Password Reset Code: {code}", html);
    }

    // ================================================================
    // ? BOOKING CONFIRMATION EMAIL (BONUS)
    // ================================================================
    public async Task SendBookingConfirmationAsync(
        string toEmail,
        string recipientName,
        string bookingReference,
        string facilityName,
        string dateText,
        string timeText,
        string status)
    {
        var html = BuildBookingConfirmationHtml(
            recipientName, bookingReference, facilityName, dateText, timeText, status);
        await SendAsync(toEmail,
            $"? Booking {status}: {facilityName} ({bookingReference})", html);
    }

    // ================================================================
    // ? ADMIN ACCOUNT GRANTED (existing user promoted)
    // ================================================================
    public async Task SendAdminGrantedAsync(string toEmail, string recipientName, string previousRole)
    {
        var html = BuildAdminGrantedHtml(recipientName, previousRole);
        await SendAsync(toEmail,
            "🛡️ You have been granted Admin access — UTHM Library Space", html);
    }

    // ================================================================
    // ? ADMIN WELCOME (new admin account + temporary password)
    // ================================================================
    public async Task SendAdminWelcomeAsync(
        string toEmail, string recipientName, string staffId, string tempPassword)
    {
        var html = BuildAdminWelcomeHtml(recipientName, staffId, tempPassword);
        await SendAsync(toEmail,
            "🛡️ Your PTTA Admin account is ready — UTHM Library Space", html);
    }

    // ================================================================
    // HTML TEMPLATE: VERIFICATION
    // ================================================================
    private string BuildVerificationEmailHtml(string recipientName, string code, string email)
    {
        var firstName = !string.IsNullOrEmpty(recipientName)
            ? recipientName.Split(' ')[0]
            : "there";

        var digits = code.Select(c =>
            $"<span style=\"display:inline-block; width:42px; height:56px; line-height:56px; background:#f8fafc; border:1.5px solid #e2e8f0; border-radius:12px; text-align:center; font-family:'Courier New',monospace; font-size:24px; font-weight:700; color:#0f172a; margin:0 4px;\">{c}</span>"
        ).ToArray();
        var codeHtml = string.Join("", digits);

        return $@"
<!DOCTYPE html>
<html>
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<title>Verify Your Email</title>
</head>
<body style=""margin:0; padding:0; background-color:#f5f2ed; font-family:-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;"">

<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background-color:#f5f2ed; padding:40px 20px;"">
<tr><td align=""center"">

<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""600"" style=""max-width:600px; width:100%; background-color:#ffffff; border-radius:20px; overflow:hidden; box-shadow:0 10px 40px rgba(15,23,42,0.08);"">

    <!-- HEADER -->
    <tr>
    <td style=""background:linear-gradient(135deg,#0f172a 0%,#1e1b4b 50%,#4c1d95 100%); padding:40px 40px 32px; text-align:center;"">
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:0 auto 20px;"">
        <tr>
        <td style=""width:64px; height:64px; background:linear-gradient(135deg,#8b5cf6,#06b6d4); border-radius:18px; text-align:center; vertical-align:middle; box-shadow:0 8px 24px rgba(139,92,246,0.4);"">
            <span style=""font-size:28px; line-height:64px; color:white;"">📚</span>
        </td>
        </tr>
        </table>
        <h1 style=""margin:0 0 8px; font-size:24px; font-weight:700; color:#ffffff; letter-spacing:-0.5px;"">
            Welcome to UTHM Library Space
        </h1>
        <p style=""margin:0; font-size:14px; color:#c4b5fd; font-weight:500;"">
            Verify your email to get started
        </p>
    </td>
    </tr>

    <!-- BODY -->
    <tr>
    <td style=""padding:40px 40px 24px;"">
        <p style=""margin:0 0 12px; font-size:16px; color:#0f172a; font-weight:600;"">
            Hi {firstName} 👋
        </p>
        <p style=""margin:0 0 24px; font-size:14px; color:#475569; line-height:1.6;"">
            Thank you for signing up for <strong style=""color:#0f172a;"">UTHM Library Space</strong>. 
            To activate your account, please use the verification code below:
        </p>

        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background-color:#faf5ff; border-radius:16px;"">
        <tr>
        <td style=""padding:28px 20px; text-align:center;"">
            <div style=""font-size:11px; color:#7c3aed; font-weight:700; letter-spacing:2px; text-transform:uppercase; margin-bottom:16px;"">
                YOUR VERIFICATION CODE
            </div>
            <div style=""font-size:0; white-space:nowrap;"">
                {codeHtml}
            </div>
            <div style=""font-size:11px; color:#94a3b8; margin-top:16px;"">
                This code expires in <strong>10 minutes</strong>
            </div>
        </td>
        </tr>
        </table>

        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""margin:24px 0;"">
        <tr><td style=""border-top:1px dashed #e2e8f0; font-size:0; line-height:0;"">&nbsp;</td></tr>
        </table>

        <p style=""margin:0 0 12px; font-size:13px; color:#0f172a; font-weight:600;"">
            What happens next?
        </p>

        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""margin-bottom:12px;"">
        <tr>
        <td width=""28"" style=""vertical-align:top; padding-top:2px;"">
            <span style=""display:inline-block; width:20px; height:20px; background:#ede9fe; border-radius:50%; text-align:center; line-height:20px; font-size:11px; color:#7c3aed; font-weight:700;"">1</span>
        </td>
        <td style=""font-size:13px; color:#475569; line-height:1.6; padding-bottom:8px;"">
            Enter the code on the verification page
        </td>
        </tr>
        <tr>
        <td width=""28"" style=""vertical-align:top; padding-top:2px;"">
            <span style=""display:inline-block; width:20px; height:20px; background:#ede9fe; border-radius:50%; text-align:center; line-height:20px; font-size:11px; color:#7c3aed; font-weight:700;"">2</span>
        </td>
        <td style=""font-size:13px; color:#475569; line-height:1.6; padding-bottom:8px;"">
            Complete your profile setup
        </td>
        </tr>
        <tr>
        <td width=""28"" style=""vertical-align:top; padding-top:2px;"">
            <span style=""display:inline-block; width:20px; height:20px; background:#ede9fe; border-radius:50%; text-align:center; line-height:20px; font-size:11px; color:#7c3aed; font-weight:700;"">3</span>
        </td>
        <td style=""font-size:13px; color:#475569; line-height:1.6;"">
            Start booking library facilities
        </td>
        </tr>
        </table>
    </td>
    </tr>

    <!-- SECURITY NOTICE -->
    <tr>
    <td style=""padding:0 40px 24px;"">
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background-color:#fef3c7; border-left:4px solid #f59e0b; border-radius:10px;"">
        <tr>
        <td style=""padding:14px 18px;"">
            <p style=""margin:0; font-size:12px; color:#92400e; line-height:1.5;"">
                <strong>🔒 Security Tip:</strong> Never share this code with anyone. UTHM Library Space will never ask for your code via phone, email, or chat.
            </p>
        </td>
        </tr>
        </table>
    </td>
    </tr>

    <!-- DIDN'T REQUEST -->
    <tr>
    <td style=""padding:0 40px 32px;"">
        <p style=""margin:0; font-size:12px; color:#94a3b8; line-height:1.6; text-align:center;"">
            Didn't sign up for UTHM Library Space?<br>
            You can safely ignore this email � no account will be created.
        </p>
    </td>
    </tr>

    <!-- FOOTER -->
    <tr>
    <td style=""background-color:#0f172a; padding:28px 40px; text-align:center;"">
        <p style=""margin:0 0 8px; font-size:13px; color:#cbd5e1; font-weight:600;"">
            📚 UTHM Library Space
        </p>
        <p style=""margin:0 0 12px; font-size:11px; color:#94a3b8; line-height:1.5;"">
            Perpustakaan Tunku Tun Aminah<br>
            Universiti Tun Hussein Onn Malaysia
        </p>
        <p style=""margin:0; font-size:10px; color:#64748b;"">
            This is an automated message � please do not reply.
        </p>
    </td>
    </tr>

</table>

<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""600"" style=""max-width:600px;"">
<tr>
<td style=""padding:20px 0; text-align:center;"">
    <p style=""margin:0; font-size:10px; color:#94a3b8;"">
        � {DateTime.Now.Year} UTHM Library Space � Final Year Project
    </p>
</td>
</tr>
</table>

</td></tr>
</table>

</body>
</html>";
    }

    // ================================================================
    // HTML TEMPLATE: PASSWORD RESET
    // ================================================================
    private string BuildPasswordResetEmailHtml(string recipientName, string code, string email)
    {
        var firstName = !string.IsNullOrEmpty(recipientName)
            ? recipientName.Split(' ')[0]
            : "there";

        var digits = code.Select(c =>
            $"<span style=\"display:inline-block; width:42px; height:56px; line-height:56px; background:#f8fafc; border:1.5px solid #e2e8f0; border-radius:12px; text-align:center; font-family:'Courier New',monospace; font-size:24px; font-weight:700; color:#0f172a; margin:0 4px;\">{c}</span>"
        ).ToArray();
        var codeHtml = string.Join("", digits);

        return $@"
<!DOCTYPE html>
<html>
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<title>Password Reset</title>
</head>
<body style=""margin:0; padding:0; background-color:#f5f2ed; font-family:-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;"">

<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background-color:#f5f2ed; padding:40px 20px;"">
<tr><td align=""center"">

<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""600"" style=""max-width:600px; width:100%; background-color:#ffffff; border-radius:20px; overflow:hidden; box-shadow:0 10px 40px rgba(15,23,42,0.08);"">

    <tr>
    <td style=""background:linear-gradient(135deg,#0f172a 0%,#1e1b4b 50%,#4c1d95 100%); padding:40px 40px 32px; text-align:center;"">
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:0 auto 20px;"">
        <tr>
        <td style=""width:64px; height:64px; background:linear-gradient(135deg,#f59e0b,#d97706); border-radius:18px; text-align:center; vertical-align:middle; box-shadow:0 8px 24px rgba(245,158,11,0.4);"">
            <span style=""font-size:28px; line-height:64px; color:white;"">🔑</span>
        </td>
        </tr>
        </table>
        <h1 style=""margin:0 0 8px; font-size:24px; font-weight:700; color:#ffffff; letter-spacing:-0.5px;"">
            Reset Your Password
        </h1>
        <p style=""margin:0; font-size:14px; color:#fcd34d; font-weight:500;"">
            We received a request to reset your password
        </p>
    </td>
    </tr>

    <tr>
    <td style=""padding:40px 40px 24px;"">
        <p style=""margin:0 0 12px; font-size:16px; color:#0f172a; font-weight:600;"">
            Hi {firstName} 👋
        </p>
        <p style=""margin:0 0 24px; font-size:14px; color:#475569; line-height:1.6;"">
            Use the code below to set a new password for your 
            <strong style=""color:#0f172a;"">UTHM Library Space</strong> account.
        </p>

        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background-color:#fffbeb; border-radius:16px;"">
        <tr>
        <td style=""padding:28px 20px; text-align:center;"">
            <div style=""font-size:11px; color:#b45309; font-weight:700; letter-spacing:2px; text-transform:uppercase; margin-bottom:16px;"">
                PASSWORD RESET CODE
            </div>
            <div style=""font-size:0; white-space:nowrap;"">
                {codeHtml}
            </div>
            <div style=""font-size:11px; color:#94a3b8; margin-top:16px;"">
                This code expires in <strong>10 minutes</strong>
            </div>
        </td>
        </tr>
        </table>

        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""margin:24px 0;"">
        <tr><td style=""border-top:1px dashed #e2e8f0; font-size:0; line-height:0;"">&nbsp;</td></tr>
        </table>

        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background-color:#fef2f2; border-left:4px solid #ef4444; border-radius:10px;"">
        <tr>
        <td style=""padding:14px 18px;"">
            <p style=""margin:0; font-size:12px; color:#991b1b; line-height:1.5;"">
                <strong>⚠️ Didn't request this?</strong> If you didn't ask to reset your password, ignore this email. Your password will not change unless you enter this code on our website.
            </p>
        </td>
        </tr>
        </table>
    </td>
    </tr>

    <tr>
    <td style=""background-color:#0f172a; padding:28px 40px; text-align:center;"">
        <p style=""margin:0 0 8px; font-size:13px; color:#cbd5e1; font-weight:600;"">
            📚 UTHM Library Space
        </p>
        <p style=""margin:0 0 12px; font-size:11px; color:#94a3b8; line-height:1.5;"">
            Perpustakaan Tunku Tun Aminah<br>
            Universiti Tun Hussein Onn Malaysia
        </p>
        <p style=""margin:0; font-size:10px; color:#64748b;"">
            This is an automated message � please do not reply.
        </p>
    </td>
    </tr>

</table>

<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""600"" style=""max-width:600px;"">
<tr>
<td style=""padding:20px 0; text-align:center;"">
    <p style=""margin:0; font-size:10px; color:#94a3b8;"">
        � {DateTime.Now.Year} UTHM Library Space � Final Year Project
    </p>
</td>
</tr>
</table>

</td></tr>
</table>

</body>
</html>";
    }

    // ================================================================
    // HTML TEMPLATE: BOOKING CONFIRMATION
    // ================================================================
    private string BuildBookingConfirmationHtml(
        string recipientName,
        string bookingReference,
        string facilityName,
        string dateText,
        string timeText,
        string status)
    {
        var firstName = !string.IsNullOrEmpty(recipientName)
            ? recipientName.Split(' ')[0]
            : "there";

        var (statusColor, statusBg, statusIcon) = status switch
        {
            "Approved" or "Confirmed" => ("#10b981", "#ecfdf5", "✅"),
            "Rejected" => ("#ef4444", "#fef2f2", "❌"),
            "Cancelled" => ("#f59e0b", "#fffbeb", "⚠️"),
            _ => ("#8b5cf6", "#faf5ff", "📅"),
        };

        return $@"
<!DOCTYPE html>
<html>
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<title>Booking Confirmation</title>
</head>
<body style=""margin:0; padding:0; background-color:#f5f2ed; font-family:-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;"">

<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background-color:#f5f2ed; padding:40px 20px;"">
<tr><td align=""center"">

<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""600"" style=""max-width:600px; width:100%; background-color:#ffffff; border-radius:20px; overflow:hidden; box-shadow:0 10px 40px rgba(15,23,42,0.08);"">

    <tr>
    <td style=""background:linear-gradient(135deg,#0f172a 0%,#1e1b4b 50%,#4c1d95 100%); padding:40px 40px 32px; text-align:center;"">
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:0 auto 20px;"">
        <tr>
        <td style=""width:64px; height:64px; background:linear-gradient(135deg,{statusColor},#0f172a); border-radius:18px; text-align:center; vertical-align:middle; box-shadow:0 8px 24px rgba(139,92,246,0.4);"">
            <span style=""font-size:28px; line-height:64px; color:white;"">{statusIcon}</span>
        </td>
        </tr>
        </table>
        <h1 style=""margin:0 0 8px; font-size:24px; font-weight:700; color:#ffffff; letter-spacing:-0.5px;"">
            Booking {status}
        </h1>
        <p style=""margin:0; font-size:14px; color:#c4b5fd; font-weight:500;"">
            Reference: {bookingReference}
        </p>
    </td>
    </tr>

    <tr>
    <td style=""padding:40px 40px 24px;"">
        <p style=""margin:0 0 20px; font-size:16px; color:#0f172a; font-weight:600;"">
            Hi {firstName} 👋
        </p>
        <p style=""margin:0 0 24px; font-size:14px; color:#475569; line-height:1.6;"">
            Here are your booking details for <strong style=""color:#0f172a;"">UTHM Library Space</strong>:
        </p>

        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background-color:{statusBg}; border-radius:16px; margin-bottom:24px;"">
        <tr>
        <td style=""padding:24px;"">
            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""margin-bottom:14px;"">
            <tr>
                <td style=""font-size:12px; color:#64748b; font-weight:600; text-transform:uppercase; letter-spacing:1px;"">Facility</td>
                <td style=""font-size:14px; color:#0f172a; font-weight:700; text-align:right;"">{facilityName}</td>
            </tr>
            </table>
            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""margin-bottom:14px;"">
            <tr>
                <td style=""font-size:12px; color:#64748b; font-weight:600; text-transform:uppercase; letter-spacing:1px;"">Date</td>
                <td style=""font-size:14px; color:#0f172a; font-weight:700; text-align:right;"">{dateText}</td>
            </tr>
            </table>
            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""margin-bottom:14px;"">
            <tr>
                <td style=""font-size:12px; color:#64748b; font-weight:600; text-transform:uppercase; letter-spacing:1px;"">Time</td>
                <td style=""font-size:14px; color:#0f172a; font-weight:700; text-align:right;"">{timeText}</td>
            </tr>
            </table>
            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""padding-top:14px; border-top:1px dashed #cbd5e1;"">
            <tr>
                <td style=""font-size:12px; color:#64748b; font-weight:600; text-transform:uppercase; letter-spacing:1px;"">Status</td>
                <td style=""font-size:14px; color:{statusColor}; font-weight:700; text-align:right;"">{status}</td>
            </tr>
            </table>
        </td>
        </tr>
        </table>

        <p style=""margin:0; font-size:12px; color:#94a3b8; line-height:1.6; text-align:center;"">
            Please arrive on time. If you can't attend, cancel from your dashboard.
        </p>
    </td>
    </tr>

    <tr>
    <td style=""background-color:#0f172a; padding:28px 40px; text-align:center;"">
        <p style=""margin:0 0 8px; font-size:13px; color:#cbd5e1; font-weight:600;"">
            📚 UTHM Library Space
        </p>
        <p style=""margin:0 0 12px; font-size:11px; color:#94a3b8; line-height:1.5;"">
            Perpustakaan Tunku Tun Aminah<br>
            Universiti Tun Hussein Onn Malaysia
        </p>
        <p style=""margin:0; font-size:10px; color:#64748b;"">
            This is an automated message � please do not reply.
        </p>
    </td>
    </tr>

</table>

</td></tr>
</table>

</body>
</html>";
    }

    // ================================================================
    // HTML TEMPLATE: ADMIN GRANTED (existing account promoted)
    // ================================================================
    private string BuildAdminGrantedHtml(string recipientName, string previousRole)
    {
        var firstName = !string.IsNullOrEmpty(recipientName)
            ? recipientName.Split(' ')[0]
            : "there";

        return $@"
<!DOCTYPE html>
<html>
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<title>Admin Access Granted</title>
</head>
<body style=""margin:0; padding:0; background-color:#f5f2ed; font-family:-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;"" >

<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background-color:#f5f2ed; padding:40px 20px;"">
<tr><td align=""center"">

<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""600"" style=""max-width:600px; width:100%; background-color:#ffffff; border-radius:20px; overflow:hidden; box-shadow:0 10px 40px rgba(15,23,42,0.08);"">

    <tr>
    <td style=""background:linear-gradient(135deg,#1E2264 0%,#2E3192 55%,#3B2F8F 100%); padding:40px 40px 32px; text-align:center;"">
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:0 auto 20px;"">
        <tr>
        <td style=""width:64px; height:64px; background:linear-gradient(135deg,#D97706,#b45309); border-radius:18px; text-align:center; vertical-align:middle; box-shadow:0 8px 24px rgba(217,119,6,0.4);"">
            <span style=""font-size:28px; line-height:64px; color:white;"">🛡️</span>
        </td>
        </tr>
        </table>
        <h1 style=""margin:0 0 8px; font-size:24px; font-weight:700; color:#ffffff; letter-spacing:-0.5px;"">
            Admin Access Granted
        </h1>
        <p style=""margin:0; font-size:14px; color:#fcd34d; font-weight:500;"">
            Perpustakaan Tunku Tun Aminah · UTHM
        </p>
    </td>
    </tr>

    <tr>
    <td style=""padding:40px 40px 24px;"">
        <p style=""margin:0 0 12px; font-size:16px; color:#0f172a; font-weight:600;"">
            Hi {firstName} 👋
        </p>
        <p style=""margin:0 0 20px; font-size:14px; color:#475569; line-height:1.6;"">
            A PTTA administrator has promoted your <strong style=""color:#0f172a;"">UTHM Library Space</strong>
            account from <strong style=""color:#D97706;"">{previousRole}</strong> to
            <strong style=""color:#1E2264;"">Admin</strong>. Your existing password still works.
        </p>

        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background-color:#eef2ff; border-left:4px solid #2E3192; border-radius:12px;"">
        <tr>
        <td style=""padding:20px 24px;"">
            <div style=""font-size:12px; color:#1E2264; font-weight:700; letter-spacing:1.5px; text-transform:uppercase; margin-bottom:10px;"">
                You can now
            </div>
            <ul style=""margin:0; padding-left:18px; font-size:14px; color:#334155; line-height:1.9;"">
                <li>Approve or reject booking requests</li>
                <li>Manage rooms, booking rules and users</li>
                <li>Collect Bilik Iqra' room keys and export reports</li>
                <li>Publish announcements and reply to feedback</li>
            </ul>
        </td>
        </tr>
        </table>
    </td>
    </tr>

    <tr>
    <td style=""padding:0 40px 32px;"">
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background-color:#fffbeb; border-radius:14px;"">
        <tr>
        <td style=""padding:20px 24px; font-size:13px; color:#92400e; line-height:1.6;"">
            🔒 <strong>Security tip:</strong> sign in, open <strong>My Profile</strong> and change your
            password to something only you know. Never share your login with anyone.
        </td>
        </tr>
        </table>
    </td>
    </tr>

    <tr>
    <td style=""background-color:#f8fafc; padding:24px 40px; border-top:1px solid #e2e8f0;"">
        <p style=""margin:0 0 8px; font-size:12px; color:#64748b; line-height:1.5;"">
            Perpustakaan Tunku Tun Aminah<br>
            Universiti Tun Hussein Onn Malaysia
        </p>
        <p style=""margin:0; font-size:10px; color:#94a3b8;"">
            This is an automated message — please do not reply.
        </p>
    </td>
    </tr>

</table>

</td></tr>
</table>

</body>
</html>";
    }

    // ============================================================
    // HTML TEMPLATE: ADMIN WELCOME (new account + temporary password)
    // ============================================================
    private string BuildAdminWelcomeHtml(
        string recipientName, string staffId, string tempPassword)
    {
        var firstName = !string.IsNullOrEmpty(recipientName)
            ? recipientName.Split(' ')[0]
            : "there";

        return $@"
<!DOCTYPE html>
<html>
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<title>Your Admin Account</title>
</head>
<body style=""margin:0; padding:0; background-color:#f5f2ed; font-family:-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;"" >

<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background-color:#f5f2ed; padding:40px 20px;"">
<tr><td align=""center"">

<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""600"" style=""max-width:600px; width:100%; background-color:#ffffff; border-radius:20px; overflow:hidden; box-shadow:0 10px 40px rgba(15,23,42,0.08);"">

    <tr>
    <td style=""background:linear-gradient(135deg,#1E2264 0%,#2E3192 55%,#3B2F8F 100%); padding:40px 40px 32px; text-align:center;"">
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:0 auto 20px;"">
        <tr>
        <td style=""width:64px; height:64px; background:linear-gradient(135deg,#D97706,#b45309); border-radius:18px; text-align:center; vertical-align:middle; box-shadow:0 8px 24px rgba(217,119,6,0.4);"">
            <span style=""font-size:28px; line-height:64px; color:white;"">🛡️</span>
        </td>
        </tr>
        </table>
        <h1 style=""margin:0 0 8px; font-size:24px; font-weight:700; color:#ffffff; letter-spacing:-0.5px;"">
            Your Admin Account Is Ready
        </h1>
        <p style=""margin:0; font-size:14px; color:#fcd34d; font-weight:500;"">
            Perpustakaan Tunku Tun Aminah · UTHM
        </p>
    </td>
    </tr>

    <tr>
    <td style=""padding:40px 40px 24px;"">
        <p style=""margin:0 0 12px; font-size:16px; color:#0f172a; font-weight:600;"">
            Hi {firstName} 👋
        </p>
        <p style=""margin:0 0 24px; font-size:14px; color:#475569; line-height:1.6;"">
            A PTTA administrator has created an <strong style=""color:#0f172a;"">Admin</strong> account
            for you on <strong style=""color:#0f172a;"">UTHM Library Space</strong>.
            Use these sign-in details:
        </p>

        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background-color:#eef2ff; border-radius:16px;"">
        <tr>
        <td style=""padding:24px;"">
            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""font-size:14px; color:#0f172a;"">
                <tr>
                    <td style=""padding:6px 0; width:110px; color:#64748b; font-weight:600;"">Staff ID</td>
                    <td style=""padding:6px 0; font-family:'Courier New',monospace; font-weight:700;"">{staffId}</td>
                </tr>
                <tr>
                    <td style=""padding:6px 0; color:#64748b; font-weight:600;"">Temporary password</td>
                    <td style=""padding:6px 0;"">
                        <span style=""display:inline-block; background:#ffffff; border:1.5px dashed #2E3192; border-radius:10px; padding:6px 14px; font-family:'Courier New',monospace; font-size:16px; font-weight:700; letter-spacing:1px; color:#1E2264;"">{tempPassword}</span>
                    </td>
                </tr>
            </table>
        </td>
        </tr>
        </table>
    </td>
    </tr>

    <tr>
    <td style=""padding:0 40px 32px;"">
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background-color:#fffbeb; border-radius:14px;"">
        <tr>
        <td style=""padding:20px 24px; font-size:13px; color:#92400e; line-height:1.7;"">
            ⚠️ <strong>Change this password immediately.</strong> After signing in, go to
            <strong>Forgot password?</strong> on the sign-in page, request a reset code to this email
            address, and set a password only you know.
        </td>
        </tr>
        </table>
    </td>
    </tr>

    <tr>
    <td style=""background-color:#f8fafc; padding:24px 40px; border-top:1px solid #e2e8f0;"">
        <p style=""margin:0 0 8px; font-size:12px; color:#64748b; line-height:1.5;"">
            Perpustakaan Tunku Tun Aminah<br>
            Universiti Tun Hussein Onn Malaysia
        </p>
        <p style=""margin:0; font-size:10px; color:#94a3b8;"">
            This is an automated message — please do not reply.
        </p>
    </td>
    </tr>

</table>

</td></tr>
</table>

</body>
</html>";
    }
}
