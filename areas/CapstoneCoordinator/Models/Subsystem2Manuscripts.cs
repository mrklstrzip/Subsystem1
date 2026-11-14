using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
            public BsonDocument RevisionCase;
            public string GroupId;
            public string Subsystem2Id;
            public string StudentNumber;
            public string StudentUserId;
            public string SubmittedById;
            public string Program;
            public string Section;
            public string Title;
            public DateTime SubmittedAt;
            public int Version;
        }

        private static IMongoCollection<BsonDocument> Collection()
        {
            return Db.Database.GetCollection<BsonDocument>("capstone_submissions");
        }

        private static List<Record> ReadRecords(bool includeRevisionCase = false)
        {
            var records = new List<Record>();
            var ownerFilter = Builders<BsonDocument>.Filter.Eq("subsystem2Owner", "DigitalRevisionAndPanelApproval");
            foreach (BsonDocument submission in Collection().Find(ownerFilter).ToList())
            {
                BsonDocument state = Document(submission, "subsystem2Data");
                BsonDocument manuscript = Document(state, "manuscript");
                if (manuscript == null) manuscript = Document(submission, "manuscript");
                // Current Subsystem 2 writes scalar title data under subsystem2Data.title,
                // with the remaining manuscript metadata on the submission document.
                if (manuscript == null && state != null && Value(state, "title") != null) manuscript = state;
                // The Subsystem 2 workflow model may be stored directly on its version document.
                if (manuscript == null && (Value(submission, "Title") != null || Value(submission, "title") != null)) manuscript = submission;
                if (manuscript == null) continue;

                string subsystem2Id = GuidText(Value(submission, "subsystem2Id"));
                if (subsystem2Id == "") continue;

                string studentNumber = First(manuscript, "StudentId", submission, "studentId");
                string userId = StudentUserId(studentNumber);
                string submittedById = AnyStr(submission, "submittedById");

                BsonValue managerValue = Value(submission, "projectManagerId");
                if (userId == "" && managerValue != null && managerValue.IsObjectId)
                    userId = managerValue.AsObjectId.ToString();
                if (userId == "" && managerValue != null && managerValue.IsString)
                    userId = managerValue.AsString;
                if (userId == "") userId = AnyStr(submission, "submittedById");
                if (userId == "") userId = studentNumber;

                string id = First(manuscript, "GroupId", submission, "groupId");
                if (id == "") id = subsystem2Id;

                string title = First(manuscript, "Title", submission, "Title");
                if (title == "") title = First(manuscript, "Title", submission, "title");
                if (title == "") title = First(manuscript, "title", submission, "title");
                if (id == "" || title == "") continue;

                BsonValue submitted = Value(manuscript, "SubmittedAt");
                if (submitted == null || submitted.IsBsonNull)
                    submitted = Value(submission, "SubmittedAt");
                if (submitted == null || submitted.IsBsonNull) submitted = Value(submission, "submissionDate");

                DateTime submittedAt = submitted != null && submitted.IsBsonDateTime
                    ? submitted.ToUniversalTime()
                    : DateTime.MinValue;

                BsonValue versionValue = Value(manuscript, "VersionNumber");
                if (versionValue == null || versionValue.IsBsonNull) versionValue = Value(submission, "VersionNumber");
                if (versionValue == null || versionValue.IsBsonNull) versionValue = Value(submission, "versions");
                int version;
                if (versionValue != null && versionValue.IsNumeric) version = versionValue.ToInt32();
                else if (!int.TryParse(versionValue == null ? "" : versionValue.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out version)) version = 1;

                records.Add(new Record
                {
                    Submission = submission,
                    Manuscript = manuscript,
                    RevisionCase = includeRevisionCase ? FindRevisionCase(subsystem2Id) : null,
                    GroupId = id,
                    Subsystem2Id = subsystem2Id,
                    StudentNumber = studentNumber,
                    StudentUserId = userId,
                    SubmittedById = submittedById,
                    Program = First(manuscript, "Program", submission, "program"),
                    Section = First(manuscript, "Section", submission, "section"),
                    Title = title,
                    SubmittedAt = submittedAt,
                    Version = version
                });
            }

            return records;
        }

        private static IEnumerable<Record> SchedulableRecords(bool includeRevisionCase = true)
        {
            return ReadRecords(includeRevisionCase)
                .GroupBy(VersionGroupKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(record => record.Version)
                    .ThenByDescending(record => record.SubmittedAt)
                    .First())
                .Where(r => !IsFinal(r.Manuscript));
        }

        private static string VersionGroupKey(Record record)
        {
            string submitter = !String.IsNullOrWhiteSpace(record.SubmittedById)
                ? record.SubmittedById
                : record.StudentUserId;

            if (String.IsNullOrWhiteSpace(record.GroupId) || record.GroupId == record.Subsystem2Id ||
                String.IsNullOrWhiteSpace(submitter) && String.IsNullOrWhiteSpace(record.StudentNumber))
                return "submission:" + record.Subsystem2Id;

            return String.Join("|", new[]
            {
                "project",
                Normalize(record.GroupId),
                ScopeKey(record.Program, record.Section),
                Normalize(submitter),
                Normalize(record.StudentNumber)
            });
        }

        private static string ScopeKey(string program, string section)
        {
            string p = (program ?? "").Trim().ToUpperInvariant();
            if (p == "SBIT") p = "BSIT";
            else if (p == "SBCS") p = "BSCS";
            else if (p == "SBIS") p = "BSIS";

            string s = (section ?? "").Trim().ToUpperInvariant();
            Match prefix = Regex.Match(s, @"^(SBIT|BSIT|SBCS|BSCS|SBIS|BSIS)[\s-]*");
            if (prefix.Success)
            {
                string prefixValue = prefix.Groups[1].Value;
                string inferred = prefixValue == "SBIT" ? "BSIT"
                    : prefixValue == "SBCS" ? "BSCS"
                    : prefixValue == "SBIS" ? "BSIS"
                    : prefixValue;
                if (p == "") p = inferred;
                s = inferred + "-" + s.Substring(prefix.Length);
            }

            return p + ":" + s;
        }

        public static List<BsonDocument> SchedulingTitles()
        {
            return SchedulableRecords(true).Select(ToCoordinatorTitle).ToList();
        }

        public static HashSet<string> SubmissionIds()
        {
            return new HashSet<string>(
                SchedulableRecords(false).Select(r => DocId(r.Submission)),
                StringComparer.OrdinalIgnoreCase);
        }

        public static BsonDocument ForVersion(string subsystem2Id)
        {
            string requested = GuidText(subsystem2Id == null ? null : new BsonString(subsystem2Id));
            if (requested == "") return null;
            Record record = ReadRecords().FirstOrDefault(r =>
                String.Equals(r.Subsystem2Id, requested, StringComparison.OrdinalIgnoreCase));
            return record == null ? null : ToCoordinatorTitle(record);
        }

        public static BsonDocument ForScheduleSubmission(string submissionId)
        {
            if (String.IsNullOrWhiteSpace(submissionId)) return null;
            Record record = ReadRecords(true).FirstOrDefault(r =>
                String.Equals(DocId(r.Submission), submissionId, StringComparison.OrdinalIgnoreCase) ||
                String.Equals(r.Subsystem2Id, submissionId, StringComparison.OrdinalIgnoreCase));
            return record == null ? null : ToCoordinatorTitle(record);
        }

        public static BsonDocument LatestForSchedule(
            string titleId,
            string managerId,
            string title)
        {
            List<Record> records = ReadRecords(true);
            Record source = records.FirstOrDefault(r => DocId(r.Submission) == titleId ||
                String.Equals(r.Subsystem2Id, titleId, StringComparison.OrdinalIgnoreCase));

            IEnumerable<Record> candidates;
            if (source != null)
            {
                // A schedule's titleId points at the exact submission/version. Do not replace
                // its manuscript with a later version from the same student or group.
                return ToCoordinatorTitle(source);
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
                ObjectId? managerId = ParseId(record.StudentUserId);
                if (!managerId.HasValue) managerId = ParseId(AnyStr(record.Submission, "submittedById"));
                if (!managerId.HasValue) managerId = ParseId(AnyStr(record.Submission, "projectManagerId"));

                if (managerId.HasValue)
                {
                    BsonDocument manager = Users()
                        .Find(Builders<BsonDocument>.Filter.Eq("_id", managerId.Value))
                        .FirstOrDefault();

                    if (manager != null) studentName = PlainName(manager);
                }
            }
            if (studentName == "") studentName = record.StudentNumber;

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

            string publicId = First(manuscript, "CloudinaryPublicId", record.Submission, "cloudinaryPublicId");
            if (publicId == "") publicId = First(manuscript, "cloudinaryPublicId", record.Submission, "CloudinaryPublicId");
            if (publicId == "") publicId = First(file, "CloudinaryPublicId", file, "cloudinaryPublicId");
            if (publicId == "") publicId = First(file, "publicId", file, "PublicId");

            string resourceType = First(manuscript, "CloudinaryResourceType", record.Submission, "cloudinaryResourceType");
            if (resourceType == "") resourceType = First(manuscript, "cloudinaryResourceType", record.Submission, "CloudinaryResourceType");
            if (resourceType == "") resourceType = First(file, "CloudinaryResourceType", file, "cloudinaryResourceType");
            if (resourceType == "") resourceType = First(file, "resourceType", file, "ResourceType");

            // Subsystem 2 has shipped both a nested preDefenseFile object and a
            // top-level filepath field. Accept either shape when locating the PDF.
            string filePath = First(
                manuscript, "filepath",
                record.Submission, "filepath");
            if (filePath == "") filePath = First(
                manuscript, "FilePath",
                record.Submission, "filePath");
            if (filePath == "") filePath = First(file, "FilePath", file, "filepath");
            if (filePath == "") filePath = First(file, "filePath", file, "path");
            string cloudinaryUrl = First(
                manuscript, "CloudinaryUrl",
                record.Submission, "cloudinaryUrl");
            if (cloudinaryUrl == "") cloudinaryUrl = First(manuscript, "cloudinaryUrl", record.Submission, "CloudinaryUrl");
            if (cloudinaryUrl == "") cloudinaryUrl = First(manuscript, "cloudinaryUrl", record.Submission, "cloudinaryUrl");
            if (cloudinaryUrl == "") cloudinaryUrl = First(file, "CloudinaryUrl", file, "cloudinaryUrl");
            if (cloudinaryUrl == "") cloudinaryUrl = First(file, "url", file, "Url");

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
            if (fileName == "") fileName = First(file, "FileName", file, "fileName");
            if (fileName == "") fileName = First(file, "filename", file, "name");

            string status = First(
                manuscript, "Status",
                record.Submission, "subsystem2Status");

            var title = new BsonDocument
            {
                { "_id", record.Submission["_id"] },
                { "subsystem2Id", record.Subsystem2Id },
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

            if (record.RevisionCase != null)
                title["revisionCase"] = record.RevisionCase.DeepClone();

            BsonValue keywords = Value(manuscript, "Keywords");
            if (keywords == null || !keywords.IsBsonArray)
                keywords = Value(record.Submission, "keywords");

            title["keywords"] = keywords != null && keywords.IsBsonArray
                ? keywords.DeepClone()
                : new BsonArray();

            string privateUrl = CreatePrivateDownloadUrl(publicId, resourceType);
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

    public static string CreatePrivateDownloadUrl(string publicId, string resourceType)
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
            if (String.IsNullOrWhiteSpace(cloudinaryUrl))
                cloudinaryUrl = System.Configuration.ConfigurationManager.AppSettings["CLOUDINARY_URL"];
            if (String.IsNullOrWhiteSpace(cloudinaryUrl))
                cloudinaryUrl = System.Configuration.ConfigurationManager.AppSettings["Cloudinary:Url"];

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

        private static BsonDocument FindRevisionCase(string subsystem2Id)
        {
            if (String.IsNullOrWhiteSpace(subsystem2Id)) return null;
            var cases = Db.Database.GetCollection<BsonDocument>("revision_case");
            var filters = Builders<BsonDocument>.Filter;
            var matches = new List<FilterDefinition<BsonDocument>>
            {
                filters.Eq("submissionID", subsystem2Id),
                filters.Eq("submissionID", subsystem2Id.Replace("-", ""))
            };
            Guid id;
            if (Guid.TryParse(subsystem2Id, out id))
            {
                matches.Add(filters.Eq("submissionID", new BsonBinaryData(id, GuidRepresentation.Standard)));
            }
            return cases.Find(filters.Or(matches)).FirstOrDefault();
        }

        private static string GuidText(BsonValue value)
        {
            if (value == null || value.IsBsonNull) return "";
            if (value.IsBsonBinaryData)
            {
                try { return value.AsBsonBinaryData.ToGuid(GuidRepresentation.Standard).ToString("D"); }
                catch { return ""; }
            }
            Guid id;
            return Guid.TryParse(value.ToString(), out id) ? id.ToString("D") : "";
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
