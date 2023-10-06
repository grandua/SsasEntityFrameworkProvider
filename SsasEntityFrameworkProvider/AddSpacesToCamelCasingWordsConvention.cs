using System;
using System.Text.RegularExpressions;

namespace AgileDesign.SsasEntityFrameworkProvider
{
    /// <summary>
    /// It is a thread safe class
    /// </summary>
    public class AddSpacesToCamelCasingWordsConvention 
        : IMdxNamingConvention
    {
        private string upperUpperLowerCasePattern = @"([^a-z])([^a-z][a-z])";
        public string UpperUpperLowerCasePattern
        {
            get { return upperUpperLowerCasePattern; }
            set { upperUpperLowerCasePattern = value; }
        }

        private string lowerUpperCasePattern = @"([a-z])([^a-z])";
        public string LowerUpperCasePattern
        {
            get { return lowerUpperCasePattern; }
            set { lowerUpperCasePattern = value; }
        }

        public string GetHierarchyAndColumnName(string sqlColumnName)
        {
            if (IsStoreFunction(sqlColumnName))
            {
                return sqlColumnName;
            }
            return String.Format("{0}{0}", GetMdxName(sqlColumnName));
        }

        bool IsStoreFunction(string sqlColumnName)
        {
            return sqlColumnName.StartsWith("'<");
        }

        public string GetMdxName(string sqlName)
        {
            return AddSpacesBetweenWords(sqlName);
        }

        string AddSpacesBetweenWords(string dbColumnName)
        {
            //TODO: test what happens if Column attribute points to column name with spaces
            return Regex.Replace
                (
                    Regex.Replace
                        (
                            dbColumnName,
                            UpperUpperLowerCasePattern,
                            "$1 $2"
                        ),
                    LowerUpperCasePattern,
                    "$1 $2"
                ).Replace("[ ", "[").Replace(" ]", "]");
        }
    }
}