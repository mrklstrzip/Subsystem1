using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Subsystem1.Areas.Student.ViewModels
{
    public class StudentProfileViewModel
    {
        public string FullName { get; set; }
        public string StudentId { get; set; }
        public string Role { get; set; } = "Student";
        public string Email { get; set; }
    }
}