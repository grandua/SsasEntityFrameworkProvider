using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using AgileDesign.Utilities;
using Xunit;

namespace UtilitiesFacts
{
    public class ConvertExtensionsFacts
    {
        [Fact]
        public void ConvertTo()
        {
            Assert.Null(ConvertExtensions.ConvertTo(null, typeof (ConvertExtensionsFacts)));
            Assert.IsType(typeof (ConvertExtensionsFacts),
                          ConvertExtensions.ConvertTo(((object)this), typeof (ConvertExtensionsFacts)));
        }
    }
}
