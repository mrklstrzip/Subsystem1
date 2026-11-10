using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Data;
using static Subsystem1.Areas.Student.Models.StudentDb;

namespace Subsystem1.Areas.CapstoneCoordinator.Models
{
    /// <summary>
    /// Reads Subsystem 2 submissions and maps them to the coordinator's schedule format.
    /// </summary>
    internal static class Subsystem2Manuscripts
    {
        private sealed class Record
        {
            public BsonDocument Submission;
            public BsonDocument Manuscript;
            public string GroupId;
            public string StudentNumber;
            public string StudentUserId;
            public string Title;
            public DateTime SubmittedAt;
            public int Version;
        }

        private static IMongoCollection<BsonDocument> Collection()
        {
            return Db.Database.GetCollection<BsonDocument>("capstone_submissions");
        }

        private static List<Record> ReadRecords()
        {
            var records = new List<Record>();

            foreach (BsonDocument submission in Collection().Find(new BsonDocument()).ToList())
            {
                BsonDocument state = Document(submission, "subsystem2Data");
                BsonDocument manuscript = Document(state, "manuscript");

                // Also support the top-level schema in the JSON export.
                bool topLevelSchema = manuscript == null && Value(submission, "title") != null;
                if (topLevelSchema) manuscript = submission;
                if (manuscript == null) continue;

                string studentNumber = First(manuscript, "StudentId", submission, "studentId");
                string userId = StudentUserId(studentNumber);

                BsonValue managerValue = Value(submission, "projectManagerId");
                if (userId == "" && managerValue != null && managerValue.IsObjectId)
                    userId = managerValue.AsObjectId.ToString();
                if (userId == "" && managerValue != null && managerValue.IsString)
                    userId = managerValue.AsString;
                if (userId == "") continue;

                string id = First(manuscript, "GroupId", submission, "groupId");
                if (id == "") id = First(manuscript, "Id", submission, "subsystem2Id");

                string title = First(manuscript, "Title", submission, "title");
                if (topLevelSchema && id == "") id = userId + "|" + Normalize(title);
                if (id == "" || title == "") continue;

                BsonValue submitted = Value(manuscript, "SubmittedAt");
                if (submitted == null || submitted.IsBsonNull)
                    submitted = Value(submission, "submissionDate");

                DateTime submittedAt = submitted != null && submitted.IsBsonDateTime
                    ? submitted.ToUniversalTime()
                    : DateTime.MinValue;

                BsonValue versionValue = Value(manuscript, "VersionNumber");
                if (versionValue == null || versionValue.IsBsonNull)
                    versionValue = Value(submission, "versions");

                int version;
                if (versionValue != null && versionValue.IsInt32)
                    version = versionValue.AsInt32;
                else if (!int.TryParse(
                    versionValue == null ? "" : versionValue.ToString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out version))
                    version = 1;

                records.Add(new Record
                {
                    Submission = submission,
                    Manuscript = manuscript,
                    GroupId = id,
                    StudentNumber = studentNumber,
                    StudentUserId = userId,
                    Title = title,
                    SubmittedAt = submittedAt,
                    Version = version
                });
            }

            return records;
        }

        private static IEnumerable<Record> LatestRecords()
        {
            return ReadRecords()
                .GroupBy(r => r.GroupId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(r => r.Version)
                    .ThenByDescending(r => r.SubmittedAt)
                    .First())
                .Where(r => !IsFinal(r.Manuscript));
        }

        public static List<BsonDocument> SchedulingTitles()
        {
            return LatestRecords().Select(ToCoordinatorTitle).ToList();
        }

        public static HashSet<string> SubmissionIds()
        {
            return new HashSet<string>(
                ReadRecords().Select(r => DocId(r.Submission)),
                StringComparer.OrdinalIgnoreCase);
        }

        public static BsonDocument LatestForSchedule(
            string titleId,
            string managerId,
            string title)
        {
            List<Record> records = ReadRecords();
            Record source = records.FirstOrDefault(r => DocId(r.Submission) == titleId);

            IEnumerable<Record> candidates;
            if (source != null)
            {
                candidates = records.Where(r =>
                    r.GroupId.Equals(source.GroupId, StringComparison.OrdinalIgnoreCase) &&
                    r.StudentUserId == source.StudentUserId);
            }
            else if (!string.IsNullOrWhiteSpace(managerId))
            {
                candidates = records.Where(r => r.StudentUserId == managerId);

                if (!string.IsNullOrWhiteSpace(title))
                {
                    string normalizedTitle = Normalize(title);
                    var exact = candidates
                        .Where(r => Normalize(r.Title) == normalizedTitle)
                        .ToList();

                    if (exact.Count > 0) candidates = exact;
                }
            }
            else
            {
                return null;
            }

            Record latest = candidates
                .OrderByDescending(r => r.Version)
                .ThenByDescending(r => r.SubmittedAt)
                .FirstOrDefault();

            return latest == null ? null : ToCoordinatorTitle(latest);
        }

        private static BsonDocument ToCoordinatorTitle(Record record)
        {
            BsonDocument manuscript = record.Manuscript;

            string studentName = First(
                manuscript, "StudentName",
                record.Submission, "studentName");

            if (studentName == "")
            {
                ObjectId? managerId =
                    ParseId(AnyStr(record.Submission, "projectManagerId"));

                if (managerId.HasValue)
                {
                    BsonDocument manager = Users()
                        .Find(Builders<BsonDocument>.Filter.Eq("_id", managerId.Value))
                        .FirstOrDefault();

                    if (manager != null) studentName = PlainName(manager);
                }
            }

            string adviserId = First(
                manuscript, "AdviserId",
                record.Submission, "adviserId");

            string adviserName = "";
            ObjectId? adviserObjectId = ParseId(adviserId);

            if (adviserObjectId.HasValue)
            {
                BsonDocument adviser = Users()
                    .Find(Builders<BsonDocument>.Filter.Eq("_id", adviserObjectId.Value))
                    .FirstOrDefault();

                if (adviser != null) adviserName = PlainName(adviser);
            }

            BsonDocument file = Document(record.Submission, "preDefenseFile");

            string publicId = First(
                manuscript, "CloudinaryPublicId",
                record.Submission, "cloudinaryPublicId");
            if (publicId == "") publicId = AnyStr(file, "cloudinaryPublicId");

            string resourceType = First(
                manuscript, "CloudinaryResourceType",
                record.Submission, "cloudinaryResourceType");
            if (resourceType == "") resourceType = AnyStr(file, "cloudinaryResourceType");

            // Subsystem 2 has shipped both a nested preDefenseFile object and a
            // top-level filepath field. Accept either shape when locating the PDF.
            string filePath = First(
                manuscript, "filepath",
                record.Submission, "filepath");
            if (filePath == "") filePath = First(
                manuscript, "FilePath",
                record.Submission, "filePath");
            if (filePath == "") filePath = AnyStr(file, "FilePath");
            string cloudinaryUrl = First(
                manuscript, "cloudinaryUrl",
                record.Submission, "cloudinaryUrl");
            if (cloudinaryUrl == "") cloudinaryUrl = AnyStr(file, "cloudinaryUrl");

            // Some export rows store only the Cloudinary public ID in FilePath.
            // In that shape there is no separate CloudinaryPublicId field.
            if (publicId == "" &&
                filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) &&
                !filePath.StartsWith("/", StringComparison.Ordinal) &&
                !filePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !filePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                publicId = filePath;
            }
            if (resourceType == "" && publicId != "") resourceType = "raw";

            string fileName = First(
                manuscript, "FileName",
                record.Submission, "fileName");
            if (fileName == "") fileName = AnyStr(file, "FileName");

            string status = First(
                manuscript, "Status",
                record.Submission, "subsystem2Status");

            var title = new BsonDocument
            {
                { "_id", record.Submission["_id"] },
                { "studentId", record.StudentUserId },
                { "title", record.Title },
                { "abstract", First(manuscript, "Abstract", record.Submission, "abstract") },
                { "authors", studentName },
                { "projectManager", studentName },
                { "program", First(manuscript, "Program", record.Submission, "program") },
                { "section", First(manuscript, "Section", record.Submission, "section") },
                { "adviserId", adviserId },
                { "adviser", adviserName },
                { "group", First(manuscript, "GroupName", record.Submission, "groupName") },
                { "sourceSubsystem2", true },
                { "sourceGroupId", record.GroupId },
                { "sourceStudentNumber", record.StudentNumber },
                { "sourceStatus", status },
                { "fileName", fileName },
                { "filePath", filePath },
                { "versionNumber", record.Version },
                { "cloudinaryPublicId", publicId },
                { "cloudinaryResourceType", resourceType },
                { "submittedAt", new BsonDateTime(record.SubmittedAt) }
            };

            BsonValue keywords = Value(manuscript, "Keywords");
            if (keywords == null || !keywords.IsBsonArray)
                keywords = Value(record.Submission, "keywords");

            title["keywords"] = keywords != null && keywords.IsBsonArray
                ? keywords.DeepClone()
                : new BsonArray();

            string privateUrl = PrivateDownloadUrl(publicId, resourceType);
            title["manuscriptUrl"] = privateUrl != "" ? privateUrl : cloudinaryUrl;

            if (title["manuscriptUrl"].AsString == "" &&
                filePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                title["manuscriptUrl"] = filePath;
            }

            return title;
        }

        private static bool IsFinal(BsonDocument manuscript)
        {
            string status = Str(manuscript, "Status");

            return status.Equals("Final", StringComparison.OrdinalIgnoreCase) ||
                   status.Equals("AdviserEndorsed", StringComparison.OrdinalIgnoreCase);
        }

        private static string PrivateDownloadUrl(string publicId, string resourceType)
        {
            if (string.IsNullOrWhiteSpace(publicId) ||
                !string.Equals(resourceType, "raw", StringComparison.OrdinalIgnoreCase))
                return "";

            string lower = publicId.ToLowerInvariant();
            if (!(lower.EndsWith(".pdf") ||
                  lower.EndsWith(".doc") ||
                  lower.EndsWith(".docx")))
                return "";

            string cloudinaryUrl = Environment.GetEnvironmentVariable("CLOUDINARY_URL");

            Uri configured;
            if (!Uri.TryCreate(cloudinaryUrl, UriKind.Absolute, out configured) ||
                !configured.Scheme.Equals("cloudinary", StringComparison.OrdinalIgnoreCase))
                return "";

            string[] auth = Uri.UnescapeDataString(configured.UserInfo ?? "")
                .Split(new[] { ':' }, 2);

            if (auth.Length != 2 || auth[1] == "" || configured.Host == "")
                return "";

            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long expiresAt = timestamp + 600;

            string canonical =
                "attachment=false&expires_at=" +
                expiresAt.ToString(CultureInfo.InvariantCulture) +
                "&public_id=" + publicId +
                "&timestamp=" + timestamp.ToString(CultureInfo.InvariantCulture) +
                "&type=authenticated";

            string signature;
            using (var sha1 = SHA1.Create())
            {
                byte[] digest = sha1.ComputeHash(
                    Encoding.UTF8.GetBytes(canonical + auth[1]));

                signature = BitConverter.ToString(digest)
                    .Replace("-", "")
                    .ToLowerInvariant();
            }

            string apiKey = Uri.EscapeDataString(auth[0]);
            string safePublicId = Uri.EscapeDataString(publicId);

            return "https://api.cloudinary.com/v1_1/" +
                Uri.EscapeDataString(configured.Host) +
                "/raw/download?timestamp=" +
                timestamp.ToString(CultureInfo.InvariantCulture) +
                "&public_id=" + safePublicId +
                "&type=authenticated&expires_at=" +
                expiresAt.ToString(CultureInfo.InvariantCulture) +
                "&attachment=false&api_key=" + apiKey +
                "&signature=" + signature;
        }

        private static string StudentUserId(string studentNumber)
        {
            if (string.IsNullOrWhiteSpace(studentNumber)) return "";

            var filter = Builders<BsonDocument>.Filter;
            BsonDocument student = Users().Find(filter.Or(
                filter.Eq("studentId", studentNumber),
                filter.Eq("studentID", studentNumber),
                filter.Eq("idNumber", studentNumber)))
                .FirstOrDefault();

            return student == null ? "" : DocId(student);
        }

        private static string Normalize(string value)
        {
            return string.Join(" ",
                (value ?? "").Split(
                    new[] { ' ', '\t', '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries))
                .Trim()
                .ToLowerInvariant();
        }

        private static string First(
            BsonDocument first,
            string firstName,
            BsonDocument second,
            string secondName)
        {
            string value = AnyStr(first, firstName).Trim();
            return value != "" ? value : AnyStr(second, secondName).Trim();
        }

        private static BsonDocument Document(BsonDocument source, string name)
        {
            BsonValue value;

            return source != null &&
                   source.TryGetValue(name, out value) &&
                   value != null &&
                   value.IsBsonDocument
                ? value.AsBsonDocument
                : null;
        }

        private static BsonValue Value(BsonDocument source, string name)
        {
            BsonValue value;

            return source != null &&
                   source.TryGetValue(name, out value) &&
                   value != null
                ? value
                : null;
        }
    }
}
