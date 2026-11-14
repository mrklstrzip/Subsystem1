using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;

namespace Subsystem1.Services
{
    // ------------------------------------------------------------------
    // Sends e-mail through SMTP. Settings come from <appSettings> in Web.config, and the
    // secret ones (user / password) from smtp.secrets.config, which is NOT part of source control:
    //
    //   Mail:Host         SMTP server              (smtp.gmail.com)
    //   Mail:Port         587 (STARTTLS). Port 465 (implicit SSL) is not supported by SmtpClient.
    //   Mail:EnableSsl    true
    //   Mail:User         SMTP login               (the Gmail address)
    //   Mail:Password     SMTP password            (a Gmail "App password", not the normal one)
    //   Mail:FromAddress  sender address           (same as Mail:User for Gmail)
    //   Mail:FromName     sender name shown to students
    //   App:BaseUrl       optional, e.g. https://capstone.qcu.edu.ph  (used for the links in the e-mail)
    // ------------------------------------------------------------------
    public static class Mailer
    {
        private static string Setting(string key, string fallback)
        {
            string v = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(v) ? fallback : v.Trim();
        }

        public static string BaseUrl
        {
            get { return Setting("App:BaseUrl", "").TrimEnd('/'); }
        }

        public static int InviteExpiryDays
        {
            get
            {
                int d;
                return int.TryParse(Setting("Invite:ExpiryDays", "7"), out d) && d >= 1 && d <= 60 ? d : 7;
            }
        }

        // Returns null when mail can be sent, otherwise what is missing.
        public static string Problem()
        {
            if (Setting("Mail:Host", "") == "") return "Email is not set up yet: Mail:Host is empty in Web.config.";
            if (Setting("Mail:User", "") == "" || Setting("Mail:Password", "") == "")
            {
                return "Email is not set up yet: add the SMTP user and password to smtp.secrets.config.";
            }
            return null;
        }

        // One connection that can send several messages. The caller disposes it.
        public static SmtpClient Connect()
        {
            int port;
            if (!int.TryParse(Setting("Mail:Port", "587"), out port)) port = 587;
            bool ssl;
            if (!bool.TryParse(Setting("Mail:EnableSsl", "true"), out ssl)) ssl = true;

            // Older runtimes default to TLS 1.0 / 1.1, which Gmail and Office 365 refuse.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            return new SmtpClient(Setting("Mail:Host", ""), port)
            {
                EnableSsl = ssl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(Setting("Mail:User", ""), Setting("Mail:Password", "")),
                Timeout = 20000
            };
        }

        public static MailMessage Compose(string to, string subject, string html, string text)
        {
            string fromAddress = Setting("Mail:FromAddress", Setting("Mail:User", ""));
            string fromName = Setting("Mail:FromName", "QCU CCS Capstone");

            var msg = new MailMessage
            {
                From = new MailAddress(fromAddress, fromName, Encoding.UTF8),
                Subject = subject,
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8
            };
            msg.To.Add(new MailAddress(to));

            // Plain text first, HTML second: mail apps show the last alternative they understand.
            msg.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(text, Encoding.UTF8, MediaTypeNames.Text.Plain));
            msg.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, Encoding.UTF8, MediaTypeNames.Text.Html));
            return msg;
        }

        // Sends one message. Returns null on success, otherwise a short reason for the person using the page.
        public static string TrySend(SmtpClient client, MailMessage msg)
        {
            try
            {
                client.Send(msg);
                return null;
            }
            catch (SmtpFailedRecipientException)
            {
                return "The mail server rejected this address.";
            }
            catch (SmtpException ex)
            {
                string m = (ex.Message ?? "").ToLowerInvariant();
                if (m.Contains("authentication") || m.Contains("5.7.0") || m.Contains("5.7.8") || m.Contains("username and password"))
                {
                    return "The mail server refused the SMTP login. Check Mail:User and the app password.";
                }
                if (m.Contains("starttls") || m.Contains("secure connection"))
                {
                    return "The mail server needs a secure connection (Mail:EnableSsl = true, port 587).";
                }
                return Detail("The mail server could not send this message.", ex);
            }
            catch (Exception ex)
            {
                return Detail("Could not reach the mail server.", ex);
            }
        }

        private static string Detail(string text, Exception ex)
        {
            var http = System.Web.HttpContext.Current;
            bool debug = http != null && http.IsDebuggingEnabled;
            return debug ? text + " (" + ex.Message + ")" : text;
        }

        // ---------------- HTML helpers for e-mail bodies ----------------

        public static string Html(string s)
        {
            return System.Net.WebUtility.HtmlEncode(s ?? "");
        }

        // Login details for an account the Admin just created (HTML + plain text).
        public static void AccountBody(string name, string role, string email, string password, string loginUrl, out string html, out string text)
        {
            string who = string.IsNullOrWhiteSpace(name) ? "there" : name.Trim();

            text =
                "Your " + role + " account is ready\r\n\r\n" +
                "Hello " + who + ",\r\n" +
                "The Administrator created a " + role + " account for you in the QCU CCS capstone defense scheduling system.\r\n\r\n" +
                "Email: " + email + "\r\n" +
                "Temporary password: " + password + "\r\n" +
                (string.IsNullOrEmpty(loginUrl) ? "" : "Log in: " + loginUrl + "\r\n") +
                "\r\nPlease log in and change your password in your Profile.\r\n" +
                "If you did not expect this e-mail, you can ignore it.\r\n";

            html =
@"<!DOCTYPE html>
<html><body style=""margin:0;padding:0;background:#E1E5F0;"">
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#E1E5F0;padding:32px 12px;"">
<tr><td align=""center"">
  <table role=""presentation"" width=""560"" cellpadding=""0"" cellspacing=""0"" style=""max-width:560px;width:100%;background:#ffffff;border-radius:14px;overflow:hidden;font-family:Segoe UI,Arial,sans-serif;color:#111;"">
    <tr><td style=""height:6px;background:#A33F53;font-size:0;line-height:0;"">&nbsp;</td></tr>
    <tr><td style=""padding:28px 32px 8px;"">
      <div style=""font-size:12px;letter-spacing:1px;text-transform:uppercase;color:#595C6E;"">Quezon City University &middot; College of Computer Studies</div>
      <h1 style=""margin:10px 0 0;font-family:Georgia,'Times New Roman',serif;font-size:26px;line-height:1.25;color:#2C3F56;"">Your " + Html(role) + @" account is ready</h1>
    </td></tr>
    <tr><td style=""padding:12px 32px 4px;font-size:15px;line-height:1.65;color:#2A2D3C;"">
      <p style=""margin:0 0 14px;"">Hello <strong>" + Html(who) + @"</strong>, the Administrator created a <strong>" + Html(role) + @"</strong> account for you in the capstone defense scheduling system.</p>
      <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin:0 0 18px;background:#F4F5F9;border-radius:10px;width:100%;"">
        <tr><td style=""padding:14px 18px;font-size:14px;"">
          <div style=""color:#595C6E;font-size:12px;"">Email</div>
          <div style=""margin-bottom:10px;"">" + Html(email) + @"</div>
          <div style=""color:#595C6E;font-size:12px;"">Temporary password</div>
          <div style=""font-family:Consolas,monospace;font-size:18px;letter-spacing:1px;font-weight:700;color:#2C3F56;"">" + Html(password) + @"</div>
        </td></tr>
      </table>
    </td></tr>" + (string.IsNullOrEmpty(loginUrl) ? "" : @"
    <tr><td align=""center"" style=""padding:0 32px 22px;"">
      <a href=""" + Html(loginUrl) + @""" style=""display:inline-block;background:#2C3F56;color:#ffffff;text-decoration:none;font-weight:600;font-size:15px;padding:14px 28px;border-radius:10px;"">Log in</a>
    </td></tr>") + @"
    <tr><td style=""padding:0 32px 26px;font-size:13px;line-height:1.6;color:#595C6E;"">
      <p style=""margin:0 0 8px;"">Please change your password in your Profile after logging in.</p>
      <p style=""margin:0;"">If you did not expect this e-mail, you can ignore it.</p>
    </td></tr>
  </table>
</td></tr>
</table>
</body></html>";
        }

        // Password reset code (HTML + plain text).
        public static void ResetCodeBody(string name, string code, int minutes, out string html, out string text)
        {
            string who = string.IsNullOrWhiteSpace(name) ? "there" : name.Trim();

            text =
                "Reset your password\r\n\r\n" +
                "Hello " + who + ",\r\n" +
                "Use this code to reset your password: " + code + "\r\n\r\n" +
                "The code expires in " + minutes + " minutes and can only be used for your account.\r\n" +
                "If you did not ask for this, you can ignore this e-mail. Your password stays the same.\r\n";

            html =
@"<!DOCTYPE html>
<html><body style=""margin:0;padding:0;background:#E1E5F0;"">
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#E1E5F0;padding:32px 12px;"">
<tr><td align=""center"">
  <table role=""presentation"" width=""560"" cellpadding=""0"" cellspacing=""0"" style=""max-width:560px;width:100%;background:#ffffff;border-radius:14px;overflow:hidden;font-family:Segoe UI,Arial,sans-serif;color:#111;"">
    <tr><td style=""height:6px;background:#A33F53;font-size:0;line-height:0;"">&nbsp;</td></tr>
    <tr><td style=""padding:28px 32px 8px;"">
      <div style=""font-size:12px;letter-spacing:1px;text-transform:uppercase;color:#595C6E;"">Quezon City University &middot; College of Computer Studies</div>
      <h1 style=""margin:10px 0 0;font-family:Georgia,'Times New Roman',serif;font-size:26px;line-height:1.25;color:#2C3F56;"">Reset your password</h1>
    </td></tr>
    <tr><td style=""padding:12px 32px 4px;font-size:15px;line-height:1.65;color:#2A2D3C;"">
      <p style=""margin:0 0 14px;"">Hello <strong>" + Html(who) + @"</strong>, use this code to reset your password:</p>
      <p style=""margin:0 0 18px;text-align:center;font-family:Consolas,monospace;font-size:32px;letter-spacing:8px;font-weight:700;color:#2C3F56;background:#F4F5F9;border-radius:10px;padding:14px 0;"">" + Html(code) + @"</p>
    </td></tr>
    <tr><td style=""padding:0 32px 26px;font-size:13px;line-height:1.6;color:#595C6E;"">
      <p style=""margin:0 0 8px;"">The code expires in <strong>" + minutes + @" minutes</strong>.</p>
      <p style=""margin:0;"">If you did not ask for this, you can ignore this e-mail. Your password stays the same.</p>
    </td></tr>
  </table>
</td></tr>
</table>
</body></html>";
        }

        // The Project Manager invitation (HTML + plain text).
        public static void InviteBody(string coordinatorName, string link, DateTime expiresManila, out string html, out string text)
        {
            string who = string.IsNullOrWhiteSpace(coordinatorName) ? "The Capstone Coordinator" : coordinatorName.Trim();
            string until = expiresManila.ToString("MMMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture);

            text =
                "You are validated as a Project Manager\r\n\r\n" +
                who + " (Capstone Coordinator, QCU College of Computer Studies) has validated you as a Project Manager " +
                "for the capstone defense scheduling system.\r\n\r\n" +
                "Open this link to sign up and set your password:\r\n" + link + "\r\n\r\n" +
                "The link works once and expires on " + until + ".\r\n" +
                "If you did not expect this e-mail, you can ignore it.\r\n";

            html =
@"<!DOCTYPE html>
<html><body style=""margin:0;padding:0;background:#E1E5F0;"">
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#E1E5F0;padding:32px 12px;"">
<tr><td align=""center"">
  <table role=""presentation"" width=""560"" cellpadding=""0"" cellspacing=""0"" style=""max-width:560px;width:100%;background:#ffffff;border-radius:14px;overflow:hidden;font-family:Segoe UI,Arial,sans-serif;color:#111;"">
    <tr><td style=""height:6px;background:#A33F53;font-size:0;line-height:0;"">&nbsp;</td></tr>
    <tr><td style=""padding:28px 32px 8px;"">
      <div style=""font-size:12px;letter-spacing:1px;text-transform:uppercase;color:#595C6E;"">Quezon City University &middot; College of Computer Studies</div>
      <h1 style=""margin:10px 0 0;font-family:Georgia,'Times New Roman',serif;font-size:26px;line-height:1.25;color:#2C3F56;"">You are validated as a Project Manager</h1>
    </td></tr>
    <tr><td style=""padding:12px 32px 4px;font-size:15px;line-height:1.65;color:#2A2D3C;"">
      <p style=""margin:0 0 14px;""><strong>" + Html(who) + @"</strong>, the Capstone Coordinator, has validated you as a
      <strong>Project Manager</strong> for the capstone defense scheduling system.</p>
      <p style=""margin:0 0 22px;"">Use the button below to create your account and choose your password.</p>
    </td></tr>
    <tr><td align=""center"" style=""padding:0 32px 22px;"">
      <a href=""" + Html(link) + @""" style=""display:inline-block;background:#2C3F56;color:#ffffff;text-decoration:none;font-weight:600;font-size:15px;padding:14px 28px;border-radius:10px;"">Sign up as Project Manager</a>
    </td></tr>
    <tr><td style=""padding:0 32px 26px;font-size:13px;line-height:1.6;color:#595C6E;"">
      <p style=""margin:0 0 8px;"">This link can be used once and expires on <strong>" + Html(until) + @"</strong>.</p>
      <p style=""margin:0 0 8px;"">If the button does not work, copy this address into your browser:<br>
        <a href=""" + Html(link) + @""" style=""color:#724389;word-break:break-all;"">" + Html(link) + @"</a></p>
      <p style=""margin:0;"">If you did not expect this e-mail, you can ignore it. No account is created until you sign up.</p>
    </td></tr>
  </table>
</td></tr>
</table>
</body></html>";
        }
    }
}
