using System;
using AgileDesign.Utilities;

namespace AgileDesign.SsasEntityFrameworkProvider.Utilities
{
    static class StringMdxExtension
    {
        /// <summary>
        ///   We use the normal box quotes for SQL server.  We do not deal with ANSI quotes
        ///   i.e. double quotes.
        /// </summary>
        /// <param name = "name"></param>
        /// <returns></returns>
        public static string InQuoteIdentifier(this string name)
        {
            Contract.Requires<ArgumentException>(!String.IsNullOrEmpty(name));
            // We assume that the names are not quoted to begin with.
            return "[" + name.Replace("]", "]]") + "]";
        }

    }
}
