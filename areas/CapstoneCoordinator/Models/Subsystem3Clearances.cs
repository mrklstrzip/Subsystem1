using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Data;

namespace Subsystem1.Areas.CapstoneCoordinator.Models
{
    public sealed class ClearedSubmission
    {
        public string Id { get; set; }
        public string Title { get; set; }
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

            return Collection().Find(filter)
                .Sort(Builders<BsonDocument>.Sort.Descending("subsystem3.DisseminatedAt"))
                .ToList()
                .Select(Map)
                .ToList();
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
                Program = First(Str(state, "Program"), Str(clearance, "Program")),
                Section = First(Str(state, "Section"), Str(clearance, "Section")),
                Status = Str(state, "Status"),
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
