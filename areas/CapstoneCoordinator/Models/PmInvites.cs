using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Data;
using Subsystem1.Services;
using static Subsystem1.Areas.Student.Models.StudentDb;

namespace Subsystem1.Areas.CapstoneCoordinator.Models
{
    // ------------------------------------------------------------------
    // Project Manager invitations sent by the Capstone Coordinator.
    //
    // Collection pm_invitations in Sia_set1_db:
    //   { email, tokenHash, status: "Pending" | "Accepted" | "Revoked",
    //     invitedById, invitedByName, createdAt, expiresAt, emailSentAt, emailError, resendCount,
    //     acceptedAt, acceptedUserId, revokedAt }
    //
    // The link in the e-mail carries a random token. Only its SHA-256 hash is stored, so a leaked
    // database cannot be used to sign up. The token works once and expires (Invite:ExpiryDays).
    // "Expired" and "Failed" are not stored: they are worked out when the list is drawn.
    // ------------------------------------------------------------------
    public class InviteResult
    {
        public string Email { get; set; }
        public string Status { get; set; }    // sent | resent | skipped | failed
        public string Message { get; set; }
    }

    public static class PmInvites
    {
        public const int MaxPerRequest = 30;

        private static readonly Regex EmailPattern = new Regex(@"^[^\s@,;]+@[^\s@,;]+\.[^\s@,;]+$", RegexOptions.Compiled);

        public static IMongoCollection<BsonDocument> Invites() { return Db.Database.GetCollection<BsonDocument>("pm_invitations"); }

        // ---------- tokens ----------

        // 32 random bytes, URL-safe, so it can go straight into a link.
        public static string NewToken()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        public static string Hash(string token)
        {
            using (var sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(token ?? ""));
                var sb = new StringBuilder(h.Length * 2);
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // ---------- helpers ----------

        public static List<string> ParseEmails(IEnumerable<string> raw)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<string>();
            foreach (string part in (raw ?? new List<string>())
                .SelectMany(s => Regex.Split(s ?? "", @"[,;\s]+")))
            {
                string e = part.Trim().ToLowerInvariant();
                if (e != "" && seen.Add(e)) list.Add(e);
            }
            return list;
        }

        private static BsonRegularExpression ExactCI(string value)
        {
            return new BsonRegularExpression("^" + Regex.Escape(value) + "$", "i");
        }

        private static bool EmailHasAccount(string email)
        {
            var filter = Builders<BsonDocument>.Filter;
            return Users().Find(filter.Or(
                filter.Regex("email", ExactCI(email)),
                filter.Regex("subsystem3.Email", ExactCI(email)))).Any();
        }

        private static DateTime? Utc(BsonDocument d, string name)
        {
            BsonValue v;
            if (d.TryGetValue(name, out v) && v != null && v.IsBsonDateTime) return v.ToUniversalTime();
            return null;
        }

        // ---------- sending ----------

        /// <summary>
        /// Creates (or renews) one invitation per address and e-mails the sign-up link.
        /// linkFor turns a token into the absolute sign-up URL.
        /// fatal is null when it ran; otherwise it says why nothing was done.
        /// </summary>
        public static List<InviteResult> Send(BsonDocument me, IEnumerable<string> rawEmails, Func<string, string> linkFor, out string fatal)
        {
            fatal = null;
            var results = new List<InviteResult>();

            List<string> emails = ParseEmails(rawEmails);
            if (emails.Count == 0) { fatal = "Enter at least one email address."; return results; }
            if (emails.Count > MaxPerRequest)
            {
                fatal = "You can invite up to " + MaxPerRequest + " students at a time.";
                return results;
            }

            string problem = Mailer.Problem();
            if (problem != null) { fatal = problem; return results; }

            DateTime now = DateTime.UtcNow;
            DateTime expires = now.AddDays(Mailer.InviteExpiryDays);
            string coordinator = PlainName(me);
            var f = Builders<BsonDocument>.Filter;
            var u = Builders<BsonDocument>.Update;

            // what has to be e-mailed: address, invitation id, token, was it renewed
            var toSend = new List<Tuple<string, ObjectId, string, bool>>();

            foreach (string email in emails)
            {
                if (email.Length > 120 || !EmailPattern.IsMatch(email))
                {
                    results.Add(new InviteResult { Email = email, Status = "skipped", Message = "Not a valid email address." });
                    continue;
                }
                if (EmailHasAccount(email))
                {
                    results.Add(new InviteResult { Email = email, Status = "skipped", Message = "Already has an account." });
                    continue;
                }

                string token = NewToken();
                BsonDocument open = Invites().Find(f.And(f.Regex("email", ExactCI(email)), f.Eq("status", "Pending"))).FirstOrDefault();

                if (open != null)
                {
                    // still waiting (or expired): give it a fresh link and a fresh deadline
                    Invites().UpdateOne(f.Eq("_id", open["_id"]), u.Combine(
                        u.Set("tokenHash", Hash(token)),
                        u.Set("expiresAt", expires),
                        u.Set("invitedById", DocId(me)),
                        u.Set("invitedByName", coordinator),
                        u.Set("createdAt", now),
                        u.Inc("resendCount", 1)));
                    toSend.Add(Tuple.Create(email, open["_id"].AsObjectId, token, true));
                }
                else
                {
                    var doc = new BsonDocument
                    {
                        { "email", email },
                        { "tokenHash", Hash(token) },
                        { "status", "Pending" },
                        { "invitedById", DocId(me) },
                        { "invitedByName", coordinator },
                        { "createdAt", now },
                        { "expiresAt", expires },
                        { "emailSentAt", BsonNull.Value },
                        { "emailError", "" },
                        { "resendCount", 0 }
                    };
                    Invites().InsertOne(doc);
                    toSend.Add(Tuple.Create(email, doc["_id"].AsObjectId, token, false));
                }
            }

            if (toSend.Count > 0)
            {
                using (var client = Mailer.Connect())
                {
                    foreach (var item in toSend)
                    {
                        string html, text;
                        Mailer.InviteBody(coordinator, linkFor(item.Item3), expires.AddHours(8), out html, out text);

                        string error;
                        try
                        {
                            using (var msg = Mailer.Compose(item.Item1,
                                "You are validated as a Project Manager - QCU CCS Capstone", html, text))
                            {
                                error = Mailer.TrySend(client, msg);
                            }
                        }
                        catch (FormatException)
                        {
                            error = "Not a valid email address.";
                        }

                        if (error == null)
                        {
                            Invites().UpdateOne(f.Eq("_id", item.Item2), u.Combine(u.Set("emailSentAt", DateTime.UtcNow), u.Set("emailError", "")));
                            results.Add(new InviteResult
                            {
                                Email = item.Item1,
                                Status = item.Item4 ? "resent" : "sent",
                                Message = item.Item4 ? "Invitation sent again." : "Invitation sent."
                            });
                        }
                        else
                        {
                            Invites().UpdateOne(f.Eq("_id", item.Item2), u.Set("emailError", error));
                            results.Add(new InviteResult { Email = item.Item1, Status = "failed", Message = error });
                        }
                    }
                }

                int ok = results.Count(r => r.Status == "sent" || r.Status == "resent");
                if (ok > 0)
                {
                    CoDb.Log("Invited " + ok + " student" + (ok == 1 ? "" : "s") + " to sign up as Project Manager.");
                }
            }

            return results;
        }

        /// <summary>Sends a new link for one waiting invitation. Returns an error message, or null.</summary>
        public static string Resend(BsonDocument me, string id, Func<string, string> linkFor)
        {
            ObjectId? oid = ParseId(id);
            if (!oid.HasValue) return "Invitation not found.";

            BsonDocument inv = Invites().Find(Builders<BsonDocument>.Filter.Eq("_id", oid.Value)).FirstOrDefault();
            if (inv == null) return "Invitation not found.";
            if (Str(inv, "status") != "Pending") return "This invitation was already used or cancelled.";

            string email = Str(inv, "email");
            if (EmailHasAccount(email)) return "That student already has an account.";

            string fatal;
            List<InviteResult> r = Send(me, new[] { email }, linkFor, out fatal);
            if (fatal != null) return fatal;
            InviteResult one = r.FirstOrDefault();
            return one != null && (one.Status == "sent" || one.Status == "resent") ? null : (one != null ? one.Message : "Could not send the email.");
        }

        /// <summary>Cancels a waiting invitation so its link stops working. Returns an error message, or null.</summary>
        public static string Revoke(string id)
        {
            ObjectId? oid = ParseId(id);
            if (!oid.HasValue) return "Invitation not found.";

            var f = Builders<BsonDocument>.Filter;
            var u = Builders<BsonDocument>.Update;
            UpdateResult r = Invites().UpdateOne(
                f.And(f.Eq("_id", oid.Value), f.Eq("status", "Pending")),
                u.Combine(u.Set("status", "Revoked"), u.Set("revokedAt", DateTime.UtcNow)));
            return r.MatchedCount == 0 ? "This invitation was already used or cancelled." : null;
        }

        /// <summary>Newest first. status: pending | expired | failed | accepted | revoked</summary>
        public static List<object> ListAll()
        {
            DateTime now = DateTime.UtcNow;
            return Invites().Find(new BsonDocument())
                .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
                .Limit(300).ToList()
                .Select(d =>
                {
                    string raw = Str(d, "status");
                    DateTime? exp = Utc(d, "expiresAt");
                    DateTime? sent = Utc(d, "emailSentAt");
                    string status;
                    if (raw == "Accepted") status = "accepted";
                    else if (raw == "Revoked") status = "revoked";
                    else if (exp.HasValue && exp.Value < now) status = "expired";
                    else if (sent == null && Str(d, "emailError") != "") status = "failed";
                    else status = "pending";

                    DateTime? created = Utc(d, "createdAt");
                    return (object)new
                    {
                        id = DocId(d),
                        email = Str(d, "email"),
                        status = status,
                        error = status == "failed" ? Str(d, "emailError") : "",
                        sentAt = (sent ?? created).HasValue ? (sent ?? created).Value.AddHours(8).ToString("MMM d, yyyy h:mm tt", System.Globalization.CultureInfo.InvariantCulture) : "-",
                        expiresAt = exp.HasValue ? exp.Value.AddHours(8).ToString("MMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture) : "-",
                        canAct = status == "pending" || status == "expired" || status == "failed"
                    };
                }).ToList();
        }

        // ---------- used by the Sign Up page ----------

        /// <summary>The still-valid invitation for this token, or null with the reason in problem.</summary>
        public static BsonDocument Lookup(string token, out string problem)
        {
            problem = null;
            if (string.IsNullOrWhiteSpace(token) || token.Length > 100)
            {
                problem = "This sign-up link is not valid.";
                return null;
            }

            BsonDocument inv = Invites().Find(Builders<BsonDocument>.Filter.Eq("tokenHash", Hash(token.Trim()))).FirstOrDefault();
            if (inv == null) { problem = "This sign-up link is not valid."; return null; }

            string st = Str(inv, "status");
            if (st == "Accepted") { problem = "This link was already used to create an account. Try logging in instead."; return null; }
            if (st == "Revoked") { problem = "This invitation was cancelled. Ask the Capstone Coordinator for a new one."; return null; }

            DateTime? exp = Utc(inv, "expiresAt");
            if (!exp.HasValue || exp.Value < DateTime.UtcNow)
            {
                problem = "This sign-up link has expired. Ask the Capstone Coordinator to send you a new one.";
                return null;
            }
            return inv;
        }

        /// <summary>Uses the invitation up (one winner even if two people click at once).</summary>
        public static bool Claim(ObjectId id)
        {
            var f = Builders<BsonDocument>.Filter;
            var u = Builders<BsonDocument>.Update;
            UpdateResult r = Invites().UpdateOne(
                f.And(f.Eq("_id", id), f.Eq("status", "Pending"), f.Gt("expiresAt", DateTime.UtcNow)),
                u.Combine(u.Set("status", "Accepted"), u.Set("acceptedAt", DateTime.UtcNow)));
            return r.ModifiedCount == 1;
        }

        public static void Release(ObjectId id)
        {
            var f = Builders<BsonDocument>.Filter;
            var u = Builders<BsonDocument>.Update;
            Invites().UpdateOne(f.And(f.Eq("_id", id), f.Eq("status", "Accepted")),
                u.Combine(u.Set("status", "Pending"), u.Unset("acceptedAt")));
        }

        public static void SetAcceptedUser(ObjectId id, string userId)
        {
            Invites().UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", id),
                Builders<BsonDocument>.Update.Set("acceptedUserId", userId));
        }
    }
}
