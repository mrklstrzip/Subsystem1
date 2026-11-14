using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using Subsystem1.Data;

namespace Subsystem1.Areas.CapstoneCoordinator.Models
{
    public sealed class ClearedSubmission
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Authors { get; set; }
        public string Abstract { get; set; }
        public List<string> Keywords { get; set; }
        public string Adviser { get; set; }
        public string Program { get; set; }
        public string Section { get; set; }
        public string Status { get; set; }
        public DateTime? DisseminatedAt { get; set; }
        public DateTime? FiledAt { get; set; }
        public string ClearanceReference { get; set; }
        public string ClearanceGroup { get; set; }
        public string ClearanceIssuedBy { get; set; }
        public DateTime? ClearanceIssuedAt { get; set; }
        public string ApprovedManuscriptId { get; set; }
        public string ApprovedManuscriptName { get; set; }
        public string ApprovedManuscriptHash { get; set; }
        public bool IsFiledImport { get; set; }
        public string RepositoryReference { get; set; }
        public string RepositoryState { get; set; }
        public bool IsHistorical { get; set; }
        public string RepositoryGridFsId { get; set; }
        public string RepositoryFileHash { get; set; }
        public string CloudFileId { get; set; }
        public string CloudFileHash { get; set; }
        public string CloudFileLength { get; set; }
        public string CurrentManuscriptId { get; set; }
        public string CurrentManuscriptHash { get; set; }
        public string Subsystem2AdviserId { get; set; }
        public DateTime? Subsystem2AdviserApprovedAt { get; set; }
        public DateTime? RepositoryRevalidatedAt { get; set; }
        public DateTime? MetadataApprovedAt { get; set; }
        public string StaffReviewer { get; set; }
        public string StaffReviewerId { get; set; }
        public string StaffReviewRole { get; set; }
        public string StaffReviewAction { get; set; }
        public DateTime? StaffReviewAt { get; set; }
        public List<ValidationSummary> ValidationSummaries { get; set; }
    }

    public sealed class ValidationSummary
    {
        public DateTime? SubmittedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Staff { get; set; }
        public string Comments { get; set; }
        public List<ValidationItem> Items { get; set; }
    }

    public sealed class ValidationItem
    {
        public string Requirement { get; set; }
        public string Status { get; set; }
        public string Feedback { get; set; }
        public string FileName { get; set; }
    }

    public static class Subsystem3Clearances
    {
        private static IMongoCollection<BsonDocument> Collection()
        {
            return Db.Database.GetCollection<BsonDocument>("capstone_submissions");
        }

        public static List<ClearedSubmission> ReadCleared()
        {
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.In("subsystem3.Status", new[] { "Disseminated", "Filed" }),
                Builders<BsonDocument>.Filter.Exists("subsystem3.DisseminatedAt", true),
                Builders<BsonDocument>.Filter.Ne("subsystem3.DisseminatedAt", BsonNull.Value));

            var submissions = Collection().Find(filter)
                .Sort(Builders<BsonDocument>.Sort.Descending("subsystem3.DisseminatedAt"))
                .ToList()
                .Select(Map)
                .ToList();

            submissions.AddRange(ReadFiledImports());
            return submissions
                .OrderByDescending(x => x.FiledAt ?? x.DisseminatedAt ?? DateTime.MinValue)
                .ToList();
        }

        // Mirrors Subsystem 3's coordinator RepositoryGroups(): filed, staff-approved
        // manuscripts that are published to the exclusive repository, plus eligible
        // historical repository records. The coordinator reads these shared collections
        // directly; no handoff copy or local cache is involved.
        public static List<ClearedSubmission> ReadCompleted()
        {
            var completed = new List<ClearedSubmission>();
            var filedFilter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("subsystem3.Status", "Filed"),
                Builders<BsonDocument>.Filter.Exists("subsystem3.FiledAt", true),
                Builders<BsonDocument>.Filter.Ne("subsystem3.FiledAt", BsonNull.Value));

            foreach (BsonDocument submission in Collection().Find(filedFilter).ToList())
            {
                ClearedSubmission item = Map(submission);
                BsonDocument state = Document(submission, "subsystem3");
                BsonDocument repository = FindRepositoryRecord(item.Id);
                BsonDocument info = Document(repository, "fileInfo");
                if (!Bool(state, "PublishToExclusive") || repository == null ||
                    !SameId(item.ApprovedManuscriptId, Str(info, "gridFsId")) ||
                    !SameHash(item.ApprovedManuscriptHash, Str(info, "sha256Hash"))) continue;

                item.RepositoryState = "Filed";
                completed.Add(item);
            }

            completed.AddRange(ReadFiledImports());
            completed.AddRange(ReadHistoricalCompleted());
            return completed
                .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderByDescending(x => x.FiledAt ?? DateTime.MinValue)
                .ToList();
        }

        private static List<ClearedSubmission> ReadHistoricalCompleted()
        {
            var records = Db.Database.GetCollection<BsonDocument>("repository_records");
            var filter = Builders<BsonDocument>.Filter.Exists("subsystem3Historical", true);
            var result = new List<ClearedSubmission>();
            foreach (BsonDocument record in records.Find(filter).ToList())
            {
                BsonDocument historical = Document(record, "subsystem3Historical");
                string reference = Str(historical, "Reference");
                if (reference.StartsWith("Subsystem 2 final approval ", StringComparison.Ordinal)) continue;

                BsonDocument catalog = Document(record, "subsystem3Catalog");
                BsonDocument fileInfo = Document(record, "fileInfo");
                string approvedId = Str(catalog, "ApprovedManuscriptId");
                string approvedHash = Str(catalog, "ApprovedManuscriptHash");
                DateTime? filedAt = Date(catalog, "FiledAt");
                DateTime? approvedAt = Date(catalog, "MetadataApprovedAt");
                if (catalog == null || !Bool(catalog, "PublishToExclusive") ||
                    !String.Equals(Str(catalog, "RepositoryState"), "Filed", StringComparison.Ordinal) ||
                    !filedAt.HasValue || !approvedAt.HasValue ||
                    String.IsNullOrWhiteSpace(Str(catalog, "MetadataApprovedBy")) ||
                    !SameId(approvedId, Str(fileInfo, "gridFsId")) ||
                    !SameHash(approvedHash, Str(fileInfo, "sha256Hash"))) continue;

                BsonDocument cloudFile = FindCloudFile(approvedId);
                if (cloudFile != null && (!SameId(Str(cloudFile, "_id"), approvedId) ||
                    !SameHash(Str(cloudFile, "sha256"), approvedHash))) continue;

                result.Add(new ClearedSubmission
                {
                    Id = Str(record, "_id"),
                    Title = First(Str(record, "title"), Str(catalog, "Title")),
                    Authors = String.Join("; ", StringValues(record, "authors", "Authors")),
                    Adviser = First(Str(record, "adviserName"), Str(catalog, "Adviser")),
                    Program = First(Str(record, "program"), Str(catalog, "Program")),
                    Section = First(Str(record, "section"), Str(catalog, "Section")),
                    Status = "Historical",
                    RepositoryState = "Historical",
                    IsHistorical = true,
                    FiledAt = filedAt,
                    ClearanceGroup = First(Str(catalog, "Name"), reference),
                    ApprovedManuscriptId = approvedId,
                    ApprovedManuscriptHash = approvedHash,
                    ApprovedManuscriptName = First(Str(cloudFile, "filename"), Str(fileInfo, "fileName")),
                    RepositoryReference = reference,
                    CloudFileId = Str(cloudFile, "_id"),
                    CloudFileHash = Str(cloudFile, "sha256"),
                    CloudFileLength = Str(cloudFile, "length")
                });
            }
            return result;
        }

        private static BsonDocument FindRepositoryRecord(string submissionId)
        {
            var records = Db.Database.GetCollection<BsonDocument>("repository_records");
            BsonDocument record = records.Find(Builders<BsonDocument>.Filter.Eq("subsystem3GroupId", submissionId)).FirstOrDefault();
            if (record != null) return record;
            ObjectId id;
            return ObjectId.TryParse(submissionId, out id)
                ? records.Find(Builders<BsonDocument>.Filter.Eq("submissionId", id)).FirstOrDefault()
                : null;
        }

        private static List<ClearedSubmission> ReadFiledImports()
        {
            const string referencePrefix = "Subsystem 2 final approval ";
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Regex(
                    "subsystem3Historical.Reference",
                    new BsonRegularExpression("^" + System.Text.RegularExpressions.Regex.Escape(referencePrefix))),
                Builders<BsonDocument>.Filter.Eq("subsystem3Catalog.RepositoryState", "Filed"),
                Builders<BsonDocument>.Filter.Exists("subsystem3Catalog.RepositoryRevalidatedAt", true),
                Builders<BsonDocument>.Filter.Ne("subsystem3Catalog.RepositoryRevalidatedAt", BsonNull.Value),
                Builders<BsonDocument>.Filter.Exists("subsystem3Catalog.MetadataApprovedAt", true),
                Builders<BsonDocument>.Filter.Ne("subsystem3Catalog.MetadataApprovedAt", BsonNull.Value),
                Builders<BsonDocument>.Filter.Exists("subsystem3Catalog.FiledAt", true),
                Builders<BsonDocument>.Filter.Ne("subsystem3Catalog.FiledAt", BsonNull.Value),
                Builders<BsonDocument>.Filter.Exists("subsystem2AdviserApproval.ApprovedAt", true),
                Builders<BsonDocument>.Filter.Ne("subsystem2AdviserApproval.ApprovedAt", BsonNull.Value),
                Builders<BsonDocument>.Filter.Exists("subsystem2AdviserApproval.AdviserId", true),
                Builders<BsonDocument>.Filter.Ne("subsystem2AdviserApproval.AdviserId", ""),
                Builders<BsonDocument>.Filter.Exists("subsystem2AdviserApproval.PdfSha256", true),
                Builders<BsonDocument>.Filter.Ne("subsystem2AdviserApproval.PdfSha256", ""));

            var imports = new List<ClearedSubmission>();
            foreach (BsonDocument record in Db.Database
                .GetCollection<BsonDocument>("repository_records")
                .Find(filter)
                .ToList())
            {
                ClearedSubmission item = MapFiledImport(record, referencePrefix);
                if (item != null) imports.Add(item);
            }

            return imports;
        }

        private static ClearedSubmission MapFiledImport(BsonDocument record, string referencePrefix)
        {
            BsonDocument historical = Document(record, "subsystem3Historical");
            BsonDocument approval = Document(record, "subsystem2AdviserApproval");
            BsonDocument catalog = Document(record, "subsystem3Catalog");
            BsonDocument fileInfo = Document(record, "fileInfo");
            string reference = Str(historical, "Reference");
            if (!reference.StartsWith(referencePrefix, StringComparison.Ordinal)) return null;

            string currentId = reference.Substring(referencePrefix.Length).Trim();
            string adviserHash = Str(approval, "PdfSha256");
            string approvedId = Str(catalog, "ApprovedManuscriptId");
            string approvedHash = Str(catalog, "ApprovedManuscriptHash");
            string fileHash = Str(fileInfo, "sha256Hash");
            string repositoryId = Str(record, "_id");
            string gridFsId = Str(fileInfo, "gridFsId");
            BsonDocument cloudFile = FindCloudFile(approvedId);
            string cloudFileId = Str(cloudFile, "_id");
            string cloudFileHash = Str(cloudFile, "sha256");

            // The historical reference identifies the exact Subsystem 2 manuscript UUID.
            // Its adviser-approved PDF hash must equal both the staff-approved catalog hash
            // and the imported repository file hash before the row is admitted.
            BsonDocument source = FindCurrentManuscript(currentId);
            if (source == null ||
                !string.Equals(Str(catalog, "RepositoryState"), "Filed", StringComparison.Ordinal) ||
                !SameId(approvedId, repositoryId) ||
                !SameId(gridFsId, repositoryId) ||
                !SameHash(adviserHash, approvedHash) ||
                !SameHash(adviserHash, fileHash) ||
                !SameId(cloudFileId, approvedId) ||
                !SameHash(adviserHash, cloudFileHash))
                return null;

            BsonDocument sourceData = Document(source, "subsystem2Data");
            BsonDocument sourceManuscript = Document(sourceData, "manuscript");
            if (sourceManuscript == null) sourceManuscript = Document(source, "manuscript");
            if (sourceManuscript == null && sourceData != null && Value(sourceData, "title") != null) sourceManuscript = sourceData;
            if (sourceManuscript == null && (Value(source, "Title") != null || Value(source, "title") != null)) sourceManuscript = source;
            string currentFileReference = First(
                Str(sourceManuscript, "FinalManuscriptPublicId"),
                Str(sourceManuscript, "CloudinaryPublicId"));
            if (!SameId(ManuscriptId(sourceManuscript), currentId) ||
                currentFileReference.IndexOf(currentId, StringComparison.OrdinalIgnoreCase) < 0)
                return null;

            BsonDocument staffEvent = ArrayDocuments(catalog, "History")
                .Where(h => Str(h, "Action").IndexOf("Staff metadata approved", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(h => Date(h, "At"))
                .FirstOrDefault();
            if (staffEvent == null || string.IsNullOrWhiteSpace(Str(staffEvent, "Actor"))) return null;

            DateTime? revalidatedAt = Date(catalog, "RepositoryRevalidatedAt");
            DateTime? metadataApprovedAt = Date(catalog, "MetadataApprovedAt");
            DateTime? filedAt = Date(catalog, "FiledAt");
            DateTime? adviserApprovedAt = Date(approval, "ApprovedAt");
            if (!revalidatedAt.HasValue || !metadataApprovedAt.HasValue || !filedAt.HasValue || !adviserApprovedAt.HasValue)
                return null;

            return new ClearedSubmission
            {
                Id = repositoryId,
                Title = First(Str(record, "title"), Str(catalog, "Title")),
                Authors = First(Str(sourceManuscript, "StudentName"), First(Str(sourceManuscript, "Authors"), Str(sourceManuscript, "ProjectManager"))),
                Abstract = First(Str(sourceManuscript, "Abstract"), Str(source, "abstract")),
                Keywords = StringValues(sourceManuscript, "Keywords", "keywords").Count > 0 ? StringValues(sourceManuscript, "Keywords", "keywords") : StringValues(source, "keywords"),
                Adviser = First(Str(sourceManuscript, "AdviserName"), Str(approval, "AdviserId")),
                Program = First(Str(record, "program"), Str(catalog, "Program")),
                Section = First(Str(record, "section"), Str(catalog, "Section")),
                Status = "Filed",
                FiledAt = filedAt,
                IsFiledImport = true,
                ClearanceGroup = First(Str(sourceManuscript, "GroupName"), First(Str(sourceManuscript, "GroupId"), Str(source, "groupId"))),
                ApprovedManuscriptId = approvedId,
                ApprovedManuscriptName = First(Str(cloudFile, "filename"), Str(fileInfo, "fileName")),
                ApprovedManuscriptHash = approvedHash,
                RepositoryReference = reference,
                RepositoryState = Str(catalog, "RepositoryState"),
                RepositoryGridFsId = gridFsId,
                RepositoryFileHash = fileHash,
                CloudFileId = cloudFileId,
                CloudFileHash = cloudFileHash,
                CloudFileLength = Str(cloudFile, "length"),
                CurrentManuscriptId = currentId,
                CurrentManuscriptHash = adviserHash,
                Subsystem2AdviserId = Str(approval, "AdviserId"),
                Subsystem2AdviserApprovedAt = adviserApprovedAt,
                RepositoryRevalidatedAt = revalidatedAt,
                MetadataApprovedAt = metadataApprovedAt,
                StaffReviewer = Str(staffEvent, "Actor"),
                StaffReviewerId = First(Str(staffEvent, "ActorId"), Str(catalog, "MetadataApprovedBy")),
                StaffReviewRole = Str(staffEvent, "Role"),
                StaffReviewAction = Str(staffEvent, "Action"),
                StaffReviewAt = Date(staffEvent, "At"),
                ValidationSummaries = new List<ValidationSummary>()
            };
        }

        private static BsonDocument FindCloudFile(string fileId)
        {
            ObjectId id;
            if (!ObjectId.TryParse(fileId, out id)) return null;
            return Db.Database.GetCollection<BsonDocument>("subsystem3_cloud_files")
                .Find(Builders<BsonDocument>.Filter.Eq("_id", id))
                .FirstOrDefault();
        }

        // Subsystem 3 stores private Cloudinary asset IDs in subsystem3_cloud_files.assets.
        // Resolve only the exact approved file, then verify the complete bytes against the
        // approved catalog hash before returning them to the authenticated Dean reader.
        public static byte[] ReadApprovedManuscript(ClearedSubmission item)
        {
            if (item == null || String.IsNullOrWhiteSpace(item.ApprovedManuscriptId) ||
                String.IsNullOrWhiteSpace(item.ApprovedManuscriptHash)) return null;

            BsonDocument cloudFile = FindCloudFile(item.ApprovedManuscriptId);
            if (cloudFile == null)
            {
                ObjectId legacyId;
                if (!ObjectId.TryParse(item.ApprovedManuscriptId, out legacyId)) return null;
                BsonDocument legacy = Db.Database.GetCollection<BsonDocument>("subsystem3_files.files")
                    .Find(Builders<BsonDocument>.Filter.Eq("_id", legacyId)).FirstOrDefault();
                if (legacy == null) return null;
                byte[] legacyBytes = new GridFSBucket(Db.Database, new GridFSBucketOptions { BucketName = "subsystem3_files" })
                    .DownloadAsBytes(legacyId);
                return SameHash(Hash(legacyBytes), item.ApprovedManuscriptHash) ? legacyBytes : null;
            }
            if (!SameId(Str(cloudFile, "_id"), item.ApprovedManuscriptId) ||
                !SameHash(Str(cloudFile, "sha256"), item.ApprovedManuscriptHash)) return null;

            long length;
            BsonValue lengthValue = Value(cloudFile, "length");
            if (lengthValue == null || !lengthValue.IsNumeric ||
                !long.TryParse(lengthValue.ToString(), out length) || length <= 0 || length > 100_000_000)
                return null;

            BsonValue assetsValue = Value(cloudFile, "assets");
            if (assetsValue == null || !assetsValue.IsBsonArray || assetsValue.AsBsonArray.Count == 0) return null;

            var settings = CloudinarySettings();
            if (settings == null) return null;
            byte[] bytes = new byte[(int)length];
            const int partSize = 7_000_000;
            int offset = 0;
            foreach (BsonValue asset in assetsValue.AsBsonArray)
            {
                if (offset >= bytes.Length || !asset.IsString || String.IsNullOrWhiteSpace(asset.AsString)) return null;
                int expectedPartLength = (int)Math.Min(partSize, length - offset);
                int expectedEncodedLength = 4 * ((expectedPartLength + 2) / 3);
                byte[] encoded = DownloadCloudinaryAsset(settings.Item1, settings.Item2,
                    settings.Item3, asset.AsString, expectedEncodedLength);
                if (encoded == null) return null;
                byte[] part;
                try { part = Convert.FromBase64String(Encoding.ASCII.GetString(encoded)); }
                catch (FormatException) { return null; }
                if (part.Length != expectedPartLength) return null;
                Buffer.BlockCopy(part, 0, bytes, offset, part.Length);
                offset += part.Length;
            }

            if (offset != bytes.Length || !SameHash(Hash(bytes), item.ApprovedManuscriptHash)) return null;
            return bytes;
        }

        private static Tuple<string, string, string> CloudinarySettings()
        {
            string value = Environment.GetEnvironmentVariable("CLOUDINARY_URL");
            if (String.IsNullOrWhiteSpace(value)) value = ConfigurationManager.AppSettings["CLOUDINARY_URL"];
            if (String.IsNullOrWhiteSpace(value)) value = ConfigurationManager.AppSettings["Cloudinary:Url"];
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) ||
                !String.Equals(uri.Scheme, "cloudinary", StringComparison.OrdinalIgnoreCase) ||
                String.IsNullOrWhiteSpace(uri.Host)) return null;
            string[] credentials = Uri.UnescapeDataString(uri.UserInfo ?? "").Split(new[] { ':' }, 2);
            if (credentials.Length != 2 || String.IsNullOrWhiteSpace(credentials[0]) ||
                String.IsNullOrWhiteSpace(credentials[1])) return null;
            return Tuple.Create(uri.Host, credentials[0], credentials[1]);
        }

        private static byte[] DownloadCloudinaryAsset(string cloud, string key, string secret, string assetId, int expectedLength)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                { "asset_id", assetId }, { "timestamp", timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture) }
            };
            string canonical = String.Join("&", parameters.Select(p => p.Key + "=" + p.Value)) + secret;
            string signature;
            using (var sha = SHA256.Create())
                signature = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-", "").ToLowerInvariant();
            string body = "asset_id=" + HttpUtility.UrlEncode(assetId) + "&timestamp=" + timestamp +
                "&api_key=" + HttpUtility.UrlEncode(key) + "&signature=" + signature;
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            var request = (HttpWebRequest)WebRequest.Create("https://api.cloudinary.com/v1_1/" + Uri.EscapeDataString(cloud) + "/asset/download");
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = bodyBytes.Length;
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;
            request.UserAgent = "Subsystem1-Dean-Subsystem3-Reader";
            using (Stream output = request.GetRequestStream()) output.Write(bodyBytes, 0, bodyBytes.Length);
            using (var response = (HttpWebResponse)request.GetResponse())
            using (Stream input = response.GetResponseStream())
            using (var buffer = new MemoryStream())
            {
                byte[] chunk = new byte[32768];
                int read;
                while ((read = input.Read(chunk, 0, chunk.Length)) > 0)
                {
                    if (buffer.Length + read > expectedLength) return null;
                    buffer.Write(chunk, 0, read);
                }
                return buffer.Length == expectedLength ? buffer.ToArray() : null;
            }
        }

        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }

        private static BsonDocument FindCurrentManuscript(string manuscriptId)
        {
            Guid id;
            if (!Guid.TryParseExact(manuscriptId, "N", out id) &&
                !Guid.TryParse(manuscriptId, out id)) return null;

            var submissions = Collection();
            var binaryId = new BsonBinaryData(id, GuidRepresentation.Standard);
            var ownerFilter = Builders<BsonDocument>.Filter.Eq("subsystem2Owner", "DigitalRevisionAndPanelApproval");
            BsonDocument source = submissions.Find(
                Builders<BsonDocument>.Filter.And(ownerFilter,
                    Builders<BsonDocument>.Filter.Eq("subsystem2Data.manuscript._id", binaryId)))
                .FirstOrDefault();
            if (source != null) return source;

            string normalized = id.ToString("N");
            return submissions.Find(Builders<BsonDocument>.Filter.And(ownerFilter,
                Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Eq("subsystem2Id", id.ToString("D")),
                    Builders<BsonDocument>.Filter.Eq("subsystem2Id", normalized),
                    Builders<BsonDocument>.Filter.Eq("subsystem2Data.manuscript._id", id.ToString("D")),
                    Builders<BsonDocument>.Filter.Eq("subsystem2Data.manuscript._id", normalized))))
                .FirstOrDefault();
        }

        private static string ManuscriptId(BsonDocument manuscript)
        {
            BsonValue id = Value(manuscript, "_id");
            if (id == null || id.IsBsonNull) return "";
            if (id.IsBsonBinaryData)
            {
                try { return id.AsBsonBinaryData.ToGuid(GuidRepresentation.Standard).ToString("N"); }
                catch { return ""; }
            }
            return id.ToString().Replace("-", "").ToLowerInvariant();
        }

        private static bool SameId(string first, string second)
        {
            return string.Equals((first ?? "").Replace("-", ""), (second ?? "").Replace("-", ""), StringComparison.OrdinalIgnoreCase);
        }

        private static bool SameHash(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) &&
                   string.Equals(first.Trim(), (second ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static ClearedSubmission Map(BsonDocument submission)
        {
            BsonDocument state = Document(submission, "subsystem3");
            BsonDocument clearance = Document(state, "CapstoneClearance");
            string approvedId = Str(state, "ApprovedManuscriptId");
            BsonDocument approved = ArrayDocuments(state, "Files")
                .FirstOrDefault(f => Str(f, "_id") == approvedId);

            return new ClearedSubmission
            {
                Id = Str(submission, "_id"),
                Title = First(Str(submission, "title"), Str(state, "Title")),
                Authors = First(Str(submission, "student"), First(Str(state, "StudentName"), Str(state, "Authors"))),
                Abstract = First(Str(state, "Abstract"), First(Str(state, "abstract"), Str(submission, "abstract"))),
                Keywords = StringValues(state, "Keywords", "keywords").Count > 0 ? StringValues(state, "Keywords", "keywords") : StringValues(submission, "keywords"),
                Adviser = First(Str(submission, "adviser"), Str(state, "Adviser")),
                Program = First(Str(state, "Program"), Str(clearance, "Program")),
                Section = First(Str(state, "Section"), Str(clearance, "Section")),
                Status = Str(state, "Status"),
                RepositoryState = First(Str(state, "RepositoryState"), Str(state, "Status")),
                DisseminatedAt = Date(state, "DisseminatedAt"),
                FiledAt = Date(state, "FiledAt"),
                ClearanceReference = Str(clearance, "Reference"),
                ClearanceGroup = First(Str(clearance, "GroupName"), Str(clearance, "GroupId")),
                ClearanceIssuedBy = Str(clearance, "IssuedBy"),
                ClearanceIssuedAt = Date(clearance, "IssuedAt"),
                ApprovedManuscriptId = approvedId,
                ApprovedManuscriptName = Str(approved, "FileName"),
                ApprovedManuscriptHash = Str(state, "ApprovedManuscriptHash"),
                ValidationSummaries = ArrayDocuments(state, "ValidationSummaries")
                    .Select(MapSummary)
                    .ToList()
            };
        }

        private static ValidationSummary MapSummary(BsonDocument summary)
        {
            return new ValidationSummary
            {
                SubmittedAt = Date(summary, "SubmittedAt"),
                CompletedAt = Date(summary, "CompletedAt"),
                Staff = Str(summary, "Staff"),
                Comments = Str(summary, "Comments"),
                Items = ArrayDocuments(summary, "Items").Select(i => new ValidationItem
                {
                    Requirement = Str(i, "Requirement"),
                    Status = Str(i, "Status"),
                    Feedback = Str(i, "Feedback"),
                    FileName = Str(i, "FileName")
                }).ToList()
            };
        }

        private static List<BsonDocument> ArrayDocuments(BsonDocument document, string key)
        {
            BsonValue value = Value(document, key);
            return value != null && value.IsBsonArray
                ? value.AsBsonArray.Where(v => v.IsBsonDocument).Select(v => v.AsBsonDocument).ToList()
                : new List<BsonDocument>();
        }

        private static List<string> StringValues(BsonDocument document, params string[] keys)
        {
            foreach (string key in keys)
            {
                BsonValue value = Value(document, key);
                if (value == null || value.IsBsonNull) continue;
                if (value.IsBsonArray)
                    return value.AsBsonArray.Where(item => item != null && !item.IsBsonNull)
                        .Select(item => item.ToString()).Where(item => !String.IsNullOrWhiteSpace(item)).ToList();
                if (value.IsString)
                    return value.AsString.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(item => item.Trim()).Where(item => item.Length > 0).ToList();
            }
            return new List<string>();
        }

        private static BsonDocument Document(BsonDocument document, string key)
        {
            BsonValue value = Value(document, key);
            return value != null && value.IsBsonDocument ? value.AsBsonDocument : null;
        }

        private static BsonValue Value(BsonDocument document, string key)
        {
            BsonValue value;
            return document != null && document.TryGetValue(key, out value) ? value : null;
        }

        private static string Str(BsonDocument document, string key)
        {
            BsonValue value = Value(document, key);
            return value == null || value.IsBsonNull ? "" : value.ToString();
        }

        private static bool Bool(BsonDocument document, string key)
        {
            BsonValue value = Value(document, key);
            return value != null && value.IsBoolean && value.AsBoolean;
        }

        private static DateTime? Date(BsonDocument document, string key)
        {
            BsonValue value = Value(document, key);
            return value != null && value.IsBsonDateTime ? (DateTime?)value.ToUniversalTime() : null;
        }

        private static string First(string first, string second)
        {
            return string.IsNullOrWhiteSpace(first) ? (second ?? "") : first;
        }
    }
}
