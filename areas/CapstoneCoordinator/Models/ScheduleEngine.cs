using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Subsystem1.Areas.CapstoneCoordinator.Models
{
    // ----------------------------------------------------------------------
    // The scheduling engine (no database code in here, so it can be tested alone).
    //
    // Flow, as in the system flowchart:
    //   1. match the title's keywords / subject-area terms to each faculty member's expertise tags
    //      (High / Medium / Low match)
    //   2. consider faculty availability, room availability, time slots, panel composition, workload
    //   3. run a CSP with backtracking:
    //        variables : one time slot, one room, PanelSize panelists
    //        constraints: room free, adviser free, every panelist free (no class, no other defense),
    //                     workload limits, adviser is never a panelist, panel has at least one
    //                     High / Medium match (when anyone available has one)
    //   4. conflict-free -> generated panel + schedule, otherwise a step-by-step conflict report.
    // ----------------------------------------------------------------------
    public class EngFaculty
    {
        public string Id;
        public string Name;
        public List<string> Tags = new List<string>();
    }

    public class EngRoom
    {
        public string Id;
        public string Name;
    }

    public class EngPick
    {
        public EngFaculty Faculty;
        public int Points;
        public double Percent;        // exact expertise match, one decimal (e.g. 66.7)
        public string Match;          // high | medium | low
        public string Role = "Member"; // Chairman (highest percentage) | Member
    }

    public class EngStep
    {
        public string Title;
        public string Text;
    }

    public class EngProblem
    {
        public string Title = "";
        public string Abstract = "";
        public List<string> Keywords = new List<string>();
        public string AdviserId;                       // null when unknown

        public List<EngFaculty> Faculty = new List<EngFaculty>();
        public List<EngRoom> Rooms = new List<EngRoom>();

        public List<DateTime> Days = new List<DateTime>();
        public int[] SlotStarts = new[] { 480, 600, 780, 900 };   // minutes after midnight
        public int Duration = 120;

        public int PanelSize = 3;
        public int MaxPerDay = 4;
        public int MaxTotal = 12;

        public Func<string, DateTime, int, int, bool> PersonBusy = (id, day, s, e) => false;
        public Func<string, DateTime, int, int, bool> RoomBusy = (id, day, s, e) => false;
        public Func<string, DateTime, int> DayLoad = (id, day) => 0;
        public Func<string, int> TotalLoad = id => 0;
    }

    public class EngResult
    {
        public bool Success;
        public DateTime Date;
        public int Start;
        public int End;
        public EngRoom Room;
        public List<EngPick> Panel = new List<EngPick>();
        public List<EngStep> Steps = new List<EngStep>();
        public string ConflictType = "";
        public string ConflictDescription = "";
        public bool WeakMatch;                        // nobody with expertise was free; assigned by availability
    }

    public static class ScheduleEngine
    {
        private static readonly HashSet<string> Stop = new HashSet<string>(new[]
        {
            "the","and","for","with","using","based","system","study","analysis","application","development",
            "design","web","of","in","on","to","a","an","by","at","from","into","its","their","that","this"
        });

        public static string Norm(string s)
        {
            return Regex.Replace((s ?? "").ToLowerInvariant().Trim(), @"\s+", " ");
        }

        private static List<string> Words(string s)
        {
            return Regex.Matches(Norm(s), @"[a-z0-9\u00f1]{4,}").Cast<Match>()
                .Select(m => m.Value).Where(w => !Stop.Contains(w)).Distinct().ToList();
        }

        public static string Level(int points)
        {
            return points >= 3 ? "high" : points >= 1 ? "medium" : "low";
        }

        /// <summary>Score of one faculty member for this title. Each expertise tag counts once.</summary>
        public static int Points(EngFaculty f, EngProblem p)
        {
            List<string> keywords = p.Keywords.Select(Norm).Where(k => k != "").ToList();
            string title = Norm(p.Title);
            string abs = Norm(p.Abstract);
            var topicWords = new HashSet<string>(Words(string.Join(" ", keywords) + " " + p.Title));

            int points = 0;
            foreach (string raw in f.Tags)
            {
                string tag = Norm(raw);
                if (tag == "") continue;

                if (keywords.Any(k => k == tag || k.Contains(tag) || tag.Contains(k))) { points += 2; continue; }
                if (title.Contains(tag)) { points += 2; continue; }
                if (abs.Contains(tag)) { points += 1; continue; }
                if (Words(tag).Any(w => topicWords.Contains(w))) { points += 1; }
            }
            return points;
        }

        /// <summary>
        /// Exact expertise match in percent (one decimal, not rounded to a level).
        /// Every topic keyword that a faculty tag covers is worth 2 points, so 100% means
        /// the faculty's tags cover every keyword of the title.
        /// </summary>
        public static double Percent(EngFaculty f, EngProblem p)
        {
            int topic = p.Keywords.Select(Norm).Where(k => k != "").Distinct().Count();
            if (topic == 0) topic = Words(p.Title).Count;
            if (topic < 1) topic = 1;

            double pct = Points(f, p) * 100.0 / (topic * 2.0);
            if (pct > 100.0) pct = 100.0;
            return Math.Round(pct, 1);
        }

        /// <summary>Highest percentage first; the first one is the Chairman, the others are Members.</summary>
        public static List<EngPick> AssignRoles(List<EngPick> panel, Func<string, int> totalLoad)
        {
            List<EngPick> ordered = panel
                .OrderByDescending(x => x.Percent)
                .ThenByDescending(x => x.Points)
                .ThenBy(x => totalLoad(x.Faculty.Id))
                .ThenBy(x => x.Faculty.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            for (int i = 0; i < ordered.Count; i++) ordered[i].Role = i == 0 ? "Chairman" : "Member";
            return ordered;
        }

        private static string Pl(int n, string word) { return n + " " + word + (n == 1 ? "" : "s"); }

        // ------------------------------------------------------------------
        public static EngResult Solve(EngProblem p)
        {
            var r = new EngResult();

            // ---- step 1: expertise match ----
            List<EngPick> cands = p.Faculty
                .Where(f => p.AdviserId == null || f.Id != p.AdviserId)
                .Select(f => { int pts = Points(f, p); return new EngPick { Faculty = f, Points = pts, Percent = Percent(f, p), Match = Level(pts) }; })
                .OrderByDescending(c => c.Percent)
                .ThenByDescending(c => c.Points)
                .ThenBy(c => p.TotalLoad(c.Faculty.Id))
                .ThenBy(c => c.Faculty.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int hi = cands.Count(c => c.Match == "high"), me = cands.Count(c => c.Match == "medium"), lo = cands.Count(c => c.Match == "low");
            r.Steps.Add(new EngStep
            {
                Title = "Match expertise",
                Text = "Compared the title's keywords and subject-area terms with the expertise tags of " + Pl(cands.Count, "panelist")
                    + ": " + hi + " High, " + me + " Medium and " + lo + " Low match."
            });

            if (p.Rooms.Count == 0)
            {
                return Fail(r, p, "Room Unavailability", "There are no active rooms to hold the defense.", 0, 0, 0, 0, 0, 0);
            }
            if (cands.Count < p.PanelSize)
            {
                return Fail(r, p, "Insufficient Panelists",
                    "Only " + Pl(cands.Count, "panelist") + " available in the system; " + p.PanelSize + " are needed.", 0, 0, 0, 0, 0, 0);
            }

            // ---- steps 2-3: CSP with backtracking over the slots ----
            int slotsTried = 0, noRoom = 0, adviserBusy = 0, shortPanel = 0, noStrong = 0, rejectedCombos = 0;

            foreach (DateTime day in p.Days)
            {
                foreach (int start in p.SlotStarts)
                {
                    int end = start + p.Duration;
                    slotsTried++;

                    // variable: room
                    EngRoom room = p.Rooms.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                        .FirstOrDefault(x => !p.RoomBusy(x.Id, day, start, end));
                    if (room == null) { noRoom++; continue; }                          // backtrack: next slot

                    // constraint: the adviser attends too
                    if (p.AdviserId != null && p.PersonBusy(p.AdviserId, day, start, end)) { adviserBusy++; continue; }

                    // variable: panelists (domain = free, not overloaded)
                    List<EngPick> avail = cands.Where(c =>
                        !p.PersonBusy(c.Faculty.Id, day, start, end)
                        && p.DayLoad(c.Faculty.Id, day) < p.MaxPerDay
                        && p.TotalLoad(c.Faculty.Id) < p.MaxTotal).ToList();
                    if (avail.Count < p.PanelSize) { shortPanel++; continue; }

                    bool needStrong = avail.Any(a => a.Match != "low");
                    int leaves = 0;
                    List<EngPick> panel = ChoosePanel(avail, p.PanelSize, needStrong, new List<EngPick>(), 0, ref leaves, ref rejectedCombos);
                    if (panel == null) { noStrong++; continue; }

                    r.Success = true;
                    r.Date = day;
                    r.Start = start;
                    r.End = end;
                    r.Room = room;
                    r.Panel = AssignRoles(panel, p.TotalLoad);
                    r.WeakMatch = panel.All(x => x.Match == "low");
                    return r;
                }
            }

            string type, desc;
            if (noRoom >= adviserBusy && noRoom >= shortPanel && noRoom >= noStrong)
            {
                type = "Room Unavailability";
                desc = "No free room in " + noRoom + " of " + slotsTried + " time slots.";
            }
            else if (adviserBusy >= shortPanel && adviserBusy >= noStrong)
            {
                type = "Adviser Unavailability";
                desc = "The adviser is busy in " + adviserBusy + " of " + slotsTried + " time slots.";
            }
            else
            {
                type = "Panelist Unavailability";
                desc = "Fewer than " + p.PanelSize + " free panelists in " + (shortPanel + noStrong) + " of " + slotsTried + " time slots.";
            }
            return Fail(r, p, type, desc, slotsTried, noRoom, adviserBusy, shortPanel, noStrong, rejectedCombos);
        }

        // depth-first search with backtracking: pick a panelist, go deeper, undo when the panel is rejected
        private static List<EngPick> ChoosePanel(List<EngPick> avail, int size, bool needStrong, List<EngPick> chosen,
            int from, ref int leaves, ref int rejected)
        {
            if (chosen.Count == size)
            {
                leaves++;
                if (!needStrong || chosen.Any(c => c.Match != "low")) return new List<EngPick>(chosen);
                rejected++;
                return null;
            }
            if (leaves > 20000) return null;

            for (int i = from; i < avail.Count; i++)
            {
                if (avail.Count - i < size - chosen.Count) break;
                chosen.Add(avail[i]);                                                     // try
                List<EngPick> found = ChoosePanel(avail, size, needStrong, chosen, i + 1, ref leaves, ref rejected);
                if (found != null) return found;
                chosen.RemoveAt(chosen.Count - 1);                                        // undo (backtrack)
            }
            return null;
        }

        private static EngResult Fail(EngResult r, EngProblem p, string type, string desc,
            int slots, int noRoom, int adviserBusy, int shortPanel, int noStrong, int rejected)
        {
            r.Success = false;
            r.ConflictType = type;
            r.ConflictDescription = desc;

            string window = p.Days.Count == 0 ? "no days"
                : p.Days.First().ToString("MMM d", CultureInfo.InvariantCulture) + " to " + p.Days.Last().ToString("MMM d", CultureInfo.InvariantCulture);
            r.Steps.Add(new EngStep
            {
                Title = "Check availability",
                Text = "Checked " + Pl(p.Days.Count, "day") + " (" + window + ") with " + Pl(p.SlotStarts.Length, "time slot") + " each and "
                    + Pl(p.Rooms.Count, "room") + ": " + Pl(slots, "slot") + " in total. Considered class schedules, other defenses, "
                    + "room bookings and each panelist's workload."
            });
            r.Steps.Add(new EngStep
            {
                Title = "Backtracking",
                Text = slots == 0
                    ? "No slot could be tried: " + desc
                    : "Tried each slot in date order and backed out of it when a constraint failed: " + Pl(noRoom, "slot") + " had no free room, "
                        + Pl(adviserBusy, "slot") + " had a busy adviser, " + Pl(shortPanel, "slot") + " had fewer than " + p.PanelSize
                        + " free panelists, and " + Pl(noStrong, "slot") + " had no panel with an expertise match. "
                        + Pl(rejected, "panel combination") + " were tried and rejected."
            });
            r.Steps.Add(new EngStep
            {
                Title = "Result",
                Text = "No conflict-free panel and schedule exists in this window (" + type + "). "
                    + "Resolve it by freeing a room or panelist, or by choosing the panelists, date, time and room yourself."
            });
            return r;
        }
    }
}
