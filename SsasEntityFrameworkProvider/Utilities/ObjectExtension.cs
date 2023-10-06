using System;
using System.Text;

namespace AgileDesign.SsasEntityFrameworkProvider.Utilities
{
    static class ObjectExtension
    {
        public static string Enquote(this object value)
        { //TODO: move to extensions
            var result = new StringBuilder();
            if (value is DateTime)
            {
                result.Append("CDate(");
            }
            if (value.AreQuotesRequired())
            {
                result.Append("'");
            }
            result.Append(value);
            if (value.AreQuotesRequired())
            {
                result.Append("'");
            }
            if (value is DateTime)
            {
                result.Append(")");
            }
            return result.ToString();
        }

        static bool AreQuotesRequired(this object value)
        {
            return (value is string)
                || value is DateTime;
        }

    }
}
