using Subsystem1.Areas.Dean.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Subsystem1.Areas.Dean.Controllers
{
    public class DefenseScheduleController : Controller
    {
        // GET: Dean/DefenseSchedule
        public ActionResult DefenseScheduleView(string search, string program = "All",
                                                string status = "All", string adviser = "All")
        {
            var all = GetSchedules(); // TODO: replace with your DB query

            var q = all.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                q = q.Where(x => x.Manuscript.ToLower().Contains(s)
                              || x.Author.ToLower().Contains(s)
                              || x.Adviser.ToLower().Contains(s)
                              || x.Panelists.Any(p => p.ToLower().Contains(s)));
            }
            if (program != "All") q = q.Where(x => x.Program == program);
            if (status != "All") q = q.Where(x => x.Status == status);
            if (adviser != "All") q = q.Where(x => x.Adviser == adviser);

            var vm = new DefenseScheduleViewModel
            {
                Items = q.ToList(),
                Search = search,
                Program = program,
                Status = status,
                Adviser = adviser,
                Programs = all.Select(x => x.Program).Distinct().OrderBy(x => x).ToList(),
                Advisers = all.Select(x => x.Adviser).Distinct().OrderBy(x => x).ToList()
            };

            ViewBag.Title = "Defense Schedule";
            return View(vm);
        }

        private List<DefenseScheduleItem> GetSchedules()
        {
            var d = new DateTime(2027, 10, 25);
            var st = new TimeSpan(8, 30, 0);
            var en = new TimeSpan(10, 30, 0);
            return new List<DefenseScheduleItem>
            {
                new DefenseScheduleItem { Id=1, Manuscript="Lips of An Angel", Section="BSIT-4A", Author="Sarah G", Adviser="Naruto Uzumaki", Date=d, StartTime=st, EndTime=en, Room="IK506", Panelists=new List<string>{"Nayeon","Jeongyeon","Momo"}, Status="Completed" },
                new DefenseScheduleItem { Id=2, Manuscript="Huwag na Huwag Mong Sasabihin", Section="BSCS-4A", Author="Lea Salonga", Adviser="Sasuke Uchiha", Date=d, StartTime=st, EndTime=en, Room="IK506", Panelists=new List<string>{"Sana","Jihyo","Mina"}, Status="Ongoing" },
                new DefenseScheduleItem { Id=3, Manuscript="Pangarap Lang Kita", Section="BSIS-4A", Author="Apple D App", Adviser="Bon Jovie", Date=d, StartTime=st, EndTime=en, Room="IK506", Panelists=new List<string>{"Dahyun","Chaeyoung","Tzuyu"}, Status="Upcoming" },
                new DefenseScheduleItem { Id=4, Manuscript="The Greatest Love of All", Section="BSCS-4D", Author="Bamboo", Adviser="The Beatles", Date=d, StartTime=st, EndTime=en, Room="IK506", Panelists=new List<string>{"Jett","Sage","Reyna"}, Status="Upcoming" }
            };
        }
        public ActionResult Index()
        {
            return View();
        }
    }
}