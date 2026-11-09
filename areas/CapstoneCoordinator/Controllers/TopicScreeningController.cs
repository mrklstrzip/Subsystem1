using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Subsystem1.Areas.CapstoneCoordinator.Models;
using Subsystem1.Data;
namespace Subsystem1.Areas.CapstoneCoordinator.Controllers
{
    public class TopicScreeningController : CoordinatorControllerBase
    {
        private static IMongoCollection<TopicScreening> Screenings { get { return Db.Database.GetCollection<TopicScreening>("topic_screenings"); } }
        private static IMongoCollection<TopicCase> CasesCollection { get { return Db.Database.GetCollection<TopicCase>("topic_cases"); } }
        private static IMongoCollection<BsonDocument> AuditCollection { get { return Db.Database.GetCollection<BsonDocument>("capstone_audit"); } }
        private static readonly string[] Stop = {"the","and","for","with","from","this","that","using","based","system","application"};
        private static HashSet<string> Tokens(string s) { return new HashSet<string>(Regex.Matches((s??"").ToLowerInvariant(), @"[\p{L}\p{N}]{3,}").Cast<Match>().Select(m=>m.Value).Where(w=>!Stop.Contains(w))); }
        private static void Log(string action,string title,string details) { AuditCollection.InsertOne(new BsonDocument {{"at",DateTime.UtcNow},{"actor","Capstone Coordinator"},{"action",action},{"target",title??""},{"details",details??""}}); }
        public ActionResult Index() { ViewBag.LastName=SharedDb.Str(Me,"lastName"); ViewBag.Title="Topic Screening"; return View("Index", Screenings.Find(FilterDefinition<TopicScreening>.Empty).SortByDescending(x=>x.CreatedAt).Limit(30).ToList()); }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Screen(TopicScreenInput input)
        {
            if(input==null || String.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length>240 || (input.Description??"").Length>10000) { TempData["Error"]="Enter a title (max 240 characters) and description (max 10,000 characters)."; return RedirectToAction("Index"); }
            var docs=SharedDb.Titles().Find(FilterDefinition<BsonDocument>.Empty).Limit(2000).ToList();
            var proposed=Tokens(input.Title+" "+input.Description); var matches=new List<TopicMatch>();
            foreach(var d in docs)
            {
                string title=SharedDb.Str(d,"title"); if(String.IsNullOrWhiteSpace(title)) continue;
                string abs=SharedDb.Str(d,"abstract"), kw=SharedDb.AnyStr(d,"keywords"), program=SharedDb.Str(d,"program");
                var old=Tokens(title+" "+abs+" "+kw); int union=proposed.Union(old).Count();
                double score=union==0?0:Math.Round(100.0*proposed.Intersect(old).Count()/union,1);
                if(String.Equals(Regex.Replace(title.ToLowerInvariant(),@"\s+"," ").Trim(),Regex.Replace(input.Title.ToLowerInvariant(),@"\s+"," ").Trim())) score=100;
                matches.Add(new TopicMatch { Id=SharedDb.DocId(d), Title=title, Abstract=abs, Program=program, Keywords=kw, Similarity=score, MatchType=score>=80?"High overlap":score>=35?"Moderate overlap":score>0?"Keyword overlap":"No meaningful keyword overlap" });
            }
            matches=matches.OrderByDescending(x=>x.Similarity).Take(10).ToList();
            var result=new TopicScreening { Id=ObjectId.GenerateNewId().ToString(), Title=input.Title.Trim(), Description=input.Description??"", GroupId=input.GroupId??"", Actor=SharedDb.Str(Me,"firstName")+" "+SharedDb.Str(Me,"lastName"), CreatedAt=DateTime.UtcNow, RepositoryCount=docs.Count, HighestScore=matches.Count==0?0:matches.Max(x=>x.Similarity), Matches=matches };
            Screenings.InsertOne(result); Log("Topic Screened",result.Title,"Compared against "+docs.Count+" existing manuscript records. Results are advisory and require human assessment.");
            TempData["ScreeningId"]=result.Id; TempData["Success"]="Screening completed. Review the similarity results before making a decision."; return RedirectToAction("Index");
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SaveCase(TopicCaseInput input)
        {
            string[] statuses={"Reviewing","Approved","Revision Required","Rejected"};
            if(input==null || String.IsNullOrWhiteSpace(input.ScreeningId) || String.IsNullOrWhiteSpace(input.Notes) || !statuses.Contains(input.Status)) { TempData["Error"]="Select a screening, choose a recommendation, and enter assessment notes."; return RedirectToAction("Index"); }
            var screening=Screenings.Find(x=>x.Id==input.ScreeningId).FirstOrDefault(); if(screening==null){TempData["Error"]="Screening not found.";return RedirectToAction("Index");}
            var previous=CasesCollection.Find(x=>x.ScreeningId==input.ScreeningId).FirstOrDefault();
            if(previous!=null && previous.Version!=input.Version){TempData["Error"]="This case changed. Reload before editing.";return RedirectToAction("Index");}
            var now=DateTime.UtcNow; var item=previous??new TopicCase { Id=ObjectId.GenerateNewId().ToString(), Reference="CAP-"+now.ToString("yyyyMMddHHmmss"), CreatedAt=now, Version=0 };
            item.ScreeningId=screening.Id; item.Title=screening.Title; item.Status=input.Status; item.Notes=input.Notes.Trim(); item.Contribution=input.Contribution??""; item.Actor=SharedDb.Str(Me,"firstName")+" "+SharedDb.Str(Me,"lastName"); item.UpdatedAt=now; item.Version++;
            if(previous==null) CasesCollection.InsertOne(item); else CasesCollection.ReplaceOne(x=>x.Id==item.Id,item);
            Log(previous==null?"Case Created":"Case Updated",item.Title,item.Status+" — "+item.Notes); TempData["Success"]="Assessment case saved."; return RedirectToAction("Index");
        }
        public ActionResult Cases() { ViewBag.Title="Assessment Cases"; return View("Cases",CasesCollection.Find(FilterDefinition<TopicCase>.Empty).SortByDescending(x=>x.UpdatedAt).ToList()); }
        public ActionResult Audit() { ViewBag.Title="Capstone Audit"; ViewBag.Events=AuditCollection.Find(FilterDefinition<BsonDocument>.Empty).Sort(Builders<BsonDocument>.Sort.Descending("at")).Limit(300).ToList(); return View("Audit"); }
    }
}
