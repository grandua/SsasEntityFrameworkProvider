using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModelExample
{
    public class Employee : Person
    {
        public string EmployeeId { get; set; }
        public Employee Manager { get; set; }
        public IList<Employee> Subordinates { get; set; }
    }
}
