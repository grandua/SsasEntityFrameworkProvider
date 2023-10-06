using System;

namespace AgileDesign.SsasEntityFrameworkProvider
{
    /// <summary>
    /// It is a thread safe class
    /// </summary>
    public class PreserveSpecifiedNameConvention 
        : IMdxNamingConvention
    {
        public string GetHierarchyAndColumnName(string sqlColumnName)
        {
            return String.Format("{0}{0}", sqlColumnName);
        }

        public string GetMdxName(string sqlName)
        {
            return sqlName;
        }
    }
}