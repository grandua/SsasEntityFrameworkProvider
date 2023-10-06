using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;

namespace ModelExample
{
    //[Dimension]
    public class User : Employee, IPrincipal, IIdentity
    {
        public string Name { get; set; }

        public bool IsDisabled { get; set; }

        //[Dimension(Name = "Role")]
        public IList<string> Roles { get; set; }
        
        public bool IsInRole(string role)
        {
            return Roles.Contains(role);
        }

        public IIdentity Identity
        {
            get { return this; }
        }

        public string AuthenticationType { get; set; }

        public bool IsAuthenticated { get; set; }

    }
}
