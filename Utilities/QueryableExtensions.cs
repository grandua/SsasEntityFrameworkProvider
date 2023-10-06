using System;
using System.Linq;

namespace AgileDesign.Utilities
{
    public static class QueryableExtensions
    {
        public static string NormalizedMdx(this IQueryable query)
        {
            Contract.Requires<ArgumentNullException>(query != null);

            string inputMdx = query.ToString();
            return inputMdx.Left(inputMdx.IndexOf("--")).NormalizedSpaces();
        }
    }
}
