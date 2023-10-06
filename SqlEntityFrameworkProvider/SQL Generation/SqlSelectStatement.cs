//---------------------------------------------------------------------
// <copyright file="SqlSelectStatement.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//---------------------------------------------------------------------

using System.Collections.Generic;
using System.Diagnostics;

namespace SqlEntityFrameworkProvider
{
    /// <summary>
    /// A SqlSelectStatement represents a canonical SQL SELECT statement.
    /// It has fields for the 5 main clauses
    /// <list type="number">
    /// <item>SELECT</item>
    /// <item>FROM</item>
    /// <item>WHERE</item>
    /// <item>GROUP BY</item>
    /// <item>ORDER BY</item>
    /// </list>
    /// We do not have HAVING, since it does not correspond to anything in the DbCommandTree.
    /// Each of the fields is a SqlBuilder, so we can keep appending SQL strings
    /// or other fragments to build up the clause.
    ///
    /// We have a IsDistinct property to indicate that we want distict columns.
    /// This is given out of band, since the input expression to the select clause
    /// may already have some columns projected out, and we use append-only SqlBuilders.
    /// The DISTINCT is inserted when we finally write the object into a string.
    /// 
    /// Also, we have a Top property, which is non-null if the number of results should
    /// be limited to certain number. It is given out of band for the same reasons as DISTINCT.
    ///
    /// The FromExtents contains the list of inputs in use for the select statement.
    /// There is usually just one element in this - Select statements for joins may
    /// temporarily have more than one.
    ///
    /// If the select statement is created by a Join node, we maintain a list of
    /// all the extents that have been flattened in the join in AllJoinExtents
    /// <example>
    /// in J(j1= J(a,b), c)
    /// FromExtents has 2 nodes JoinSymbol(name=j1, ...) and Symbol(name=c)
    /// AllJoinExtents has 3 nodes Symbol(name=a), Symbol(name=b), Symbol(name=c)
    /// </example>
    ///
    /// If any expression in the non-FROM clause refers to an extent in a higher scope,
    /// we add that extent to the OuterExtents list.  This list denotes the list
    /// of extent aliases that may collide with the aliases used in this select statement.
    /// It is set by <see cref="SqlGenerator.Visit(DbVariableReferenceExpression)"/>.
    /// An extent is an outer extent if it is not one of the FromExtents.
    ///
    ///
    /// </summary>
    internal class SqlSelectStatement : ISqlFragment
    {
        protected virtual SqlBuilder CreateSelectStatement()
        {
            return new SqlBuilder();
        }

        /// <summary>
        /// Do we need to add a DISTINCT at the beginning of the SELECT
        /// </summary>
        public bool IsDistinct { get; set; }

        private List<AliasOrSubquery> allJoinExtents;
        internal List<AliasOrSubquery> AllJoinExtents
        {
            get
            {
                return allJoinExtents 
                    ?? ( allJoinExtents = new List<AliasOrSubquery>() );
            }
            // We have a setter as well, even though this is a list,
            // since we use this field only in special cases.
            //set { allJoinExtents = value; }
        }

        private List<AliasOrSubquery> fromExtents;

        public List<AliasOrSubquery> FromExtents
        {
            get
            {
                if (null == fromExtents)
                {
                    fromExtents = new List<AliasOrSubquery>();
                }
                return fromExtents;
            }
        }

        private Dictionary<AliasOrSubquery, bool> outerExtents;
        internal Dictionary<AliasOrSubquery, bool> OuterExtents
        {
            get
            {
                if (null == outerExtents)
                {
                    outerExtents = new Dictionary<AliasOrSubquery, bool>();
                }
                return outerExtents;
            }
        }

        private TopClause top;
        public TopClause Top
        {
            get { return top; }
            set 
            {
                Debug.Assert(top == null, "SqlSelectStatement.Top has already been set");
                top = value; 
            }
        }

        private SqlBuilder select;
        public SqlBuilder Select
        {
            get
            {
                return select 
                    ?? ( select = CreateSelectStatement() );
            }
        }

        private FromClause from = new FromClause();

        public FromClause From
        {
            get { return from; }
        }

        private SqlBuilder where;
        public SqlBuilder Where
        {
            get
            {
                if (null == where)
                {
                    where = new SqlBuilder();
                }
                return where;
            }
        }

        private SqlBuilder groupBy;
        internal SqlBuilder GroupBy
        {
            get
            {
                if (null == groupBy)
                {
                    groupBy = new SqlBuilder();
                }
                return groupBy;
            }
        }

        private SqlBuilder orderBy;
        public SqlBuilder OrderBy
        {
            get
            {
                if (null == orderBy)
                {
                    orderBy = new SqlBuilder();
                }
                return orderBy;
            }
        }

        //indicates whether it is the top most select statement, 
        // if not Order By should be omitted unless there is a corresponding TOP
        bool isTopMost = true;

        public bool IsTopMost
        {
            get { return isTopMost; }
            set { isTopMost = value; }
        }

        #region ISqlFragment Members

        /// <summary>
        /// Write out a SQL select statement as a string.
        /// We have to
        /// <list type="number">
        /// <item>Check whether the aliases extents we use in this statement have
        /// to be renamed.
        /// We first create a list of all the aliases used by the outer extents.
        /// For each of the FromExtents( or AllJoinExtents if it is non-null),
        /// rename it if it collides with the previous list.
        /// </item>
        /// <item>Write each of the clauses (if it exists) as a string</item>
        /// </list>
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="sqlGenerator"></param>
        public void WriteSql(SqlWriter writer, SqlGenerator sqlGenerator)
        {
            RenameTableAliasesIfNeeded(sqlGenerator);
            // Increase the indent, so that the Sql statement is nested by one tab.
            writer.Indent++;
            DoWriteSql(sqlGenerator, writer);
            writer.Indent--;
        }

        protected virtual void DoWriteSql
            (
                SqlGenerator sqlGenerator
                , SqlWriter writer)
        {
            writer.Write("SELECT ");
            WriteDistinct(writer);
            WriteTop(sqlGenerator, writer);
            WriteSelectColumns(sqlGenerator, writer);
            WriteFrom(sqlGenerator, writer);
            WriteWhere(sqlGenerator, writer);
            WriteGroupBy(sqlGenerator, writer);
            WriteOrderBy(sqlGenerator, writer);
        }

        protected void WriteOrderBy(SqlGenerator sqlGenerator,
                          SqlWriter writer)
        {
            if (( null == orderBy ) || OrderBy.IsEmpty || ( !IsTopMost && Top == null ))
            {
                return;
            }
            writer.WriteLine();
            writer.Write("ORDER BY ");
            OrderBy.WriteSql(writer, sqlGenerator);
        }

        protected void WriteGroupBy(SqlGenerator sqlGenerator,
                          SqlWriter writer)
        {
            if (( null == groupBy ) || GroupBy.IsEmpty)
            {
                return;
            }
            writer.WriteLine();
            writer.Write("GROUP BY ");
            GroupBy.WriteSql(writer, sqlGenerator);
        }

        protected void WriteWhere(SqlGenerator sqlGenerator,
                        SqlWriter writer)
        {
            if (( null == where ) || Where.IsEmpty)
            {
                return;
            }
            writer.WriteLine();
            writer.Write("WHERE ");
            Where.WriteSql(writer, sqlGenerator);
        }

        /// <summary>
        /// Check if FROM aliases need to be renamed
        /// Column renaming happens in SqlGenerator.AddAliasToColumnIfNeeded() 
        /// and in AliasOrSubquery.WriteSql()
        /// </summary>
        void RenameTableAliasesIfNeeded(SqlGenerator sqlGenerator)
        {
            if(sqlGenerator.ShouldAddAliases() == false)
            {
                return;
            }
            List<AliasOrSubquery> extentList = AllJoinExtents ?? fromExtents;
            if (null == extentList)
            {
                return;
            }
            // Create a list of the aliases used by the outer extents
            // JoinSymbols have to be treated specially.
            List<string> outerExtentAliases = GetOuterExtentAliases();

            // An then rename each of the FromExtents we have
            // If AllJoinExtents is non-null - it has precedence.
            // The new name is derived from the old name - we append an increasing int.
            foreach (AliasOrSubquery fromAlias in extentList)
            {
                if ((null != outerExtentAliases) 
                    && outerExtentAliases.Contains(fromAlias.Name))
                {
                    string newValueForNewName = GetNewValueForNewName(fromAlias, sqlGenerator);
                    fromAlias.NewName = newValueForNewName;

                    // Add extent to list of known names 
                    //(although i is always incrementing, 
                    //"prefix11" can eventually collide with "prefix1" when it is extended)
                    sqlGenerator.AllExtentNames[newValueForNewName] = 0;
                }

                // Add the current alias to the list, so that the extents
                // that follow do not collide with me.
                if (null == outerExtentAliases)
                {
                    outerExtentAliases = new List<string>();
                }
                outerExtentAliases.Add(fromAlias.NewName);
            }
        }

        /// <remarks>
        /// not pure
        /// </remarks>
        string GetNewValueForNewName(AliasOrSubquery fromAlias,
                                     SqlGenerator sqlGenerator)
        {
            int i = sqlGenerator.AllExtentNames[fromAlias.Name];
            string newValueForNewName;
            do
            {
                ++i;
                newValueForNewName = fromAlias.Name + i;
            }
            while (sqlGenerator.AllExtentNames.ContainsKey(newValueForNewName));

            sqlGenerator.AllExtentNames[fromAlias.Name] = i;
            return newValueForNewName;
        }

        List<string> GetOuterExtentAliases()
        {
            List<string> outerExtentAliases = null;
            if ((null != outerExtents) && (0 < outerExtents.Count))
            {
                foreach (AliasOrSubquery outerExtent in outerExtents.Keys)
                {
                    JoinAlias joinAlias = outerExtent as JoinAlias;
                    if (joinAlias != null)
                    {
                        foreach (AliasOrSubquery symbol in joinAlias.FlattenedExtentList)
                        {
                            if (null == outerExtentAliases) { outerExtentAliases = new List<string>(); }
                            outerExtentAliases.Add(symbol.NewName);
                        }
                    }
                    else
                    {
                        if (null == outerExtentAliases) { outerExtentAliases = new List<string>(); }
                        outerExtentAliases.Add(outerExtent.NewName);
                    }
                }
            }
            return outerExtentAliases;
        }

        protected void WriteFrom(SqlGenerator sqlGenerator,
                       SqlWriter writer)
        {
            writer.WriteLine();
            writer.Write("FROM ");
            From.WriteSql(writer, sqlGenerator);
        }

        protected void WriteSelectColumns(SqlGenerator sqlGenerator,
                                          SqlWriter writer)
        {
            if (( null != select ) && !Select.IsEmpty)
            {
                Select.WriteSql(writer, sqlGenerator);
            }
        }

        protected void WriteTop(SqlGenerator sqlGenerator,
                      SqlWriter writer)
        {
            if (Top == null)
            {
                return;
            }
            Top.WriteSql(writer, sqlGenerator);
        }

        protected void WriteDistinct(SqlWriter writer)
        {
            if (!IsDistinct)
            {
                return;
            }
            writer.Write("DISTINCT ");
        }

        #endregion

    }
}
