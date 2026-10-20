using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace Subsystem1.Areas.Student.ViewModels
{
    public class ManuscriptItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public DateTime DateAndSubmissionTime { get; set; }
        public string Status { get; set; }
    }

    public class StudentSubmissionViewModel
    {
        // Table
        public List<ManuscriptItemDto> Submissions { get; set; } = new List<ManuscriptItemDto>();

        // Form
        [Required(ErrorMessage = "Title is required")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Overview / Abstract is required")]
        public string OverviewAbstract { get; set; }

        public string Keywords { get; set; }

        [Required(ErrorMessage = "Authors are required")]
        public string Authors { get; set; }

        // Updated Property Name
        public DateTime DateSubmitted { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Program is required")]
        public string Program { get; set; }

        [Required(ErrorMessage = "Professor / Adviser is required")]
        public string Professor { get; set; }

        [Required(ErrorMessage = "Please upload a manuscript file")]
        public HttpPostedFileBase ManuscriptFile { get; set; }
    }
}