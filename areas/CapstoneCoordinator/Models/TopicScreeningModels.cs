using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace Subsystem1.Areas.CapstoneCoordinator.Models
{
    public class TopicMatch
    {
        public string Id { get; set; } public string Title { get; set; } public string Abstract { get; set; }
        public string Program { get; set; } public string Keywords { get; set; } public double Similarity { get; set; }
        public string MatchType { get; set; }
    }
    public class TopicScreening
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; }
        public string Title { get; set; } public string Description { get; set; } public string GroupId { get; set; }
        public string Actor { get; set; } public DateTime CreatedAt { get; set; } public int RepositoryCount { get; set; }
        public double HighestScore { get; set; } public List<TopicMatch> Matches { get; set; }
    }
    public class TopicCase
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string Id { get; set; }
        public string Reference { get; set; } public string ScreeningId { get; set; } public string Title { get; set; }
        public string Status { get; set; } public string Notes { get; set; } public string Contribution { get; set; }
        public DateTime CreatedAt { get; set; } public DateTime UpdatedAt { get; set; } public string Actor { get; set; } public int Version { get; set; }
    }
    public class TopicScreenInput { public string Title { get; set; } public string Description { get; set; } public string GroupId { get; set; } }
    public class TopicCaseInput { public string ScreeningId { get; set; } public string Status { get; set; } public string Notes { get; set; } public string Contribution { get; set; } public int Version { get; set; } }
}
