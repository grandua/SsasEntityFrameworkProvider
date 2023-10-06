//---------------------------------------------------------------------
// <copyright file="JoinSymbol.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//---------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Common.CommandTrees;
using System.Data.Entity.Core.Metadata.Edm;

namespace SqlEntityFrameworkProvider
{
    /// <summary>
    ///Former JoinSymbol
    /// A Join symbol is a special kind of Symbol.
    /// It has to carry additional information
    /// <list type="bullet">
    /// <item>ColumnList for the list of columns in the select clause if this
    /// symbol represents a sql select statement.  This is set by <see cref="SqlGenerator.AddDefaultColumns"/>. </item>
    /// <item>ExtentList is the list of extents in the select clause.</item>
    /// <item>FlattenedExtentList - if the Join has multiple extents flattened at the 
    /// top level, we need this information to ensure that extent aliases are renamed
    /// correctly in <see cref="SqlSelectStatement.WriteSql"/></item>
    /// <item>NameToExtent has all the extents in ExtentList as a dictionary.
    /// This is used by <see cref="SqlGenerator.Visit(DbPropertyExpression)"/> to flatten
    /// record accesses.</item>
    /// <item>IsNestedJoin - is used to determine whether a JoinSymbol is an 
    /// ordinary join symbol, or one that has a corresponding SqlSelectStatement.</item>
    /// </list>
    /// 
    /// All the lists are set exactly once, and then used for lookups/enumerated.
    /// </summary>
    internal sealed class JoinAlias : AliasOrSubquery
    {
        private List<AliasOrSubquery> columnList;
        internal List<AliasOrSubquery> ColumnList
        {
            get
            {
                if (null == columnList)
                {
                    columnList = new List<AliasOrSubquery>();
                }
                return columnList;
            }
            set { columnList = value; }
        }

        private List<AliasOrSubquery> extentList;
        internal List<AliasOrSubquery> ExtentList
        {
            get { return extentList; }
        }

        private List<AliasOrSubquery> flattenedExtentList;
        internal List<AliasOrSubquery> FlattenedExtentList
        {
            get
            {
                if (null == flattenedExtentList)
                {
                    flattenedExtentList = new List<AliasOrSubquery>();
                }
                return flattenedExtentList;
            }
            set { flattenedExtentList = value; }
        }

        private Dictionary<string, AliasOrSubquery> nameToExtent;
        internal Dictionary<string, AliasOrSubquery> NameToExtent
        {
            get { return nameToExtent; }
        }

        private bool isNestedJoin;
        internal bool IsNestedJoin
        {
            get { return isNestedJoin; }
            set { isNestedJoin = value; }
        }

        public JoinAlias(string name, TypeUsage type, List<AliasOrSubquery> extents)
            : base(name, type)
        {
            extentList = new List<AliasOrSubquery>(extents.Count);
            nameToExtent = new Dictionary<string, AliasOrSubquery>(extents.Count, StringComparer.OrdinalIgnoreCase);
            foreach (AliasOrSubquery symbol in extents)
            {
                this.nameToExtent[symbol.Name] = symbol;
                this.ExtentList.Add(symbol);
            }
        }
    }
}
