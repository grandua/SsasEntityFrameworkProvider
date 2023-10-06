//---------------------------------------------------------------------
// <copyright file="SqlBuilder.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//---------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SqlEntityFrameworkProvider
{
    /// <summary>
    /// This class is like StringBuilder.  While traversing the tree for the first time, 
    /// we do not know all the strings that need to be appended e.g. things that need to be
    /// renamed, nested select statements etc.  So, we use a builder that can collect
    /// all kinds of sql fragments.
    /// </summary>
    internal class SqlBuilder : ISqlFragment
    {
        public SqlBuilder()
        {
        }

        public SqlBuilder(string firstFragment)
            : this()
        {
            if(string.IsNullOrWhiteSpace(firstFragment))
            { //this makes 'IsEmpty' to be 'true'
                return;
            }
            Append(firstFragment);
        }

        public string Alias { get; set; }

        public void Clear()
        {
            if(sqlFragments != null)
                sqlFragments.Clear();
        }

        public static implicit operator SqlBuilder(string value)
        {
            return new SqlBuilder(value);
        }

        private List<object> sqlFragments;
        protected List<object> SqlFragments
        {
            get
            {
                return sqlFragments 
                    ?? ( sqlFragments = new List<object>() );
            }
        }

        protected IEnumerable<T> GetFragmentFlatList<T>()
            where T : SqlBuilder
        {
            return GetFragmentFlatList<T>(this);
        }

        IEnumerable<T> GetFragmentFlatList<T>(SqlBuilder sqlBuilder)
            where T : SqlBuilder
        {
            var result = new List<T>();
            foreach (var sqlFragment in sqlBuilder.SqlFragments.OfType<SqlBuilder>())
            {
                if (sqlFragment is T)
                {
                    result.Add((T)sqlFragment);
                }
                result.AddRange(GetFragmentFlatList<T>(sqlFragment));
            }
            return result;
        }

        /// <summary>
        /// Add an object to the list
        /// </summary>
        public void Append(ISqlFragment s)
        {
            AppendInternal(s);
        }
        /// <summary>
        /// Add an object to the list
        /// </summary>
        public void Append(string s)
        {
            AppendInternal(s);
        }

        void AppendInternal(object s)
        {
            Debug.Assert(s != null);
            //This would cause a stackOverflowException: 
            //(indirect dependency is a problem too, but it cannot be detected)
            Debug.Assert(s != this, 
                "Attempt to add parent SqlBuilder to one of its direct children SqlFragments!");

            SqlFragments.Add(s);
        }

        public void AppendLine(string s)
        {
            Append(s);
            AppendLine();
        }
        public void AppendLine(ISqlFragment s)
        {
            Append(s);
            AppendLine();
        }

        /// <summary>
        /// This is to pretty print the SQL.  The writer <see cref="SqlWriter.Write"/>
        /// needs to know about new lines so that it can add the right amount of 
        /// indentation at the beginning of lines.
        /// </summary>
        public void AppendLine()
        {
            SqlFragments.Add("\r\n");
        }

        /// <summary>
        /// Whether the builder is empty.  This is used by the <see cref="SqlGenerator.Visit(DbProjectExpression)"/>
        /// to determine whether a sql statement can be reused.
        /// </summary>
        public bool IsEmpty
        {
            get { return ((null == sqlFragments) 
                || (0 == sqlFragments.Count))
                || ContainsEmptyOnly();
            }
        }

        public bool IsExpression { get; set; }

        bool ContainsEmptyOnly()
        {
            return sqlFragments.All(
                fragment => 
                    (fragment is SqlBuilder && ((SqlBuilder)fragment).IsEmpty)
                        || (fragment is string && string.IsNullOrWhiteSpace(fragment.ToString())));
        }


        #region ISqlFragment Members

        /// <summary>
        /// We delegate the writing of the fragment to the appropriate type.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="sqlGenerator"></param>
        public virtual void WriteSql(SqlWriter writer, SqlGenerator sqlGenerator)
        {
            if (null != sqlFragments)
            {
                foreach (object o in sqlFragments)
                {
                    string str = (o as String);
                    if (null != str)
                    {
                        writer.Write(str);
                    }
                    else
                    {
                        ISqlFragment sqlFragment = (o as ISqlFragment);
                        if (null != sqlFragment)
                        {
                            sqlFragment.WriteSql(writer, sqlGenerator);
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }
                    }
                }
            }
        }

        #endregion

        public override string ToString()
        {
            if (sqlFragments == null)
            {
                return "";
            }
            var result = new StringBuilder();
            foreach (object sqlFragment in sqlFragments.Where(f => !(f is AliasOrSubquery)))
            {
                result.Append(sqlFragment);
            }
            return result.ToString();
        }

        public void Set(string newValue)
        {
            Clear();
            Append(newValue);
        }
    }
}
