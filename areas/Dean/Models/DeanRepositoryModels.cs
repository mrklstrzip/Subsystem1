using System.Collections.Generic;
using Subsystem1.Areas.CapstoneCoordinator.Models;

namespace Subsystem1.Areas.Dean.Models
{
    public sealed class DeanRepositoryVm
    {
        public string Query { get; set; }
        public IList<ClearedSubmission> Items { get; set; }
        public string Program { get; set; }
        public int? Year { get; set; }
        public string Filing { get; set; }
        public string Adviser { get; set; }
        public string Sort { get; set; }
        public IList<string> Programs { get; set; }
        public IList<int> Years { get; set; }
        public IList<string> Filings { get; set; }
        public IList<string> Advisers { get; set; }
    }

    public sealed class DeanManuscriptVm
    {
        public ClearedSubmission Item { get; set; }
    }
}
