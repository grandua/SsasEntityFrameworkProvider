//---------------------------------------------------------------------
// <copyright file="Symbol.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//---------------------------------------------------------------------


using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;

namespace SqlEntityFrameworkProvider
{
    /// <summary>
    /// <see cref="NamingScopes"/>
    ///Former Symbol
    /// This class represents an extent/nested select statement, or a column.
    ///
    /// The important fields are Name, Type and NewName.
    /// NewName starts off the same as Name, and is then modified as necessary.
    ///
    ///
    /// The rest are used by special symbols.
    /// e.g. NeedsRenaming is used by columns to indicate that a new name must
    /// be picked for the column in the second phase of translation.
    ///
    /// IsUnnest is used by symbols for a collection expression used as a from clause.
    /// This allows <see cref="SqlGenerator.AddFromSymbol(SqlSelectStatement, string, AliasOrSubquery, bool)"/> to add the column list
    /// after the alias.
    ///
    /// </summary>
    internal class AliasOrSubquery 
        : ISqlFragment
    {
        private Dictionary<string, AliasOrSubquery> columns = new Dictionary<string, AliasOrSubquery>(StringComparer.CurrentCultureIgnoreCase);
        internal Dictionary<string, AliasOrSubquery> Columns
        {
            get { return columns; }
        }

        private bool needsRenaming = false;
        internal bool NeedsRenaming
        {
            get { return needsRenaming; }
            set { needsRenaming = value; }
        }

        bool isUnnest = false;
        internal bool IsUnnest
        {
            get { return isUnnest; }
            set { isUnnest = value; }
        }

        string name;
        public string Name
        {
            get { return name; }
        }

        string newName;
        public string NewName
        {
            get { return newName; }
            set { newName = value; }
        }

        private TypeUsage type;
        internal TypeUsage Type
        {
            get { return type; }
            set { type = value; }
        }

        public AliasOrSubquery(string name, TypeUsage type)
        {
            this.name = name;
            newName = name;
            Type = type;
        }

        #region ISqlFragment Members

        /// <summary>
        /// We rename columns here if necessary.
        /// Table rename happens in SqlSelectStatement.RenameTableAliasesIfNeeded()
        /// 
        /// Write this symbol out as a string for sql.  This is just
        /// the new name of the symbol (which could be the same as the old name).
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="sqlGenerator"></param>
        public void WriteSql(SqlWriter writer, SqlGenerator sqlGenerator)
        {
            if (!NeedsRenaming)
            {
                writer.Write(SqlGenerator.QuoteIdentifier(NewName));
                return;
            }
            string newValueForNewName;
            int i = sqlGenerator.AllColumnNames[NewName];
            do
            {
                ++i;
                newValueForNewName = Name + i;
            }
            while (sqlGenerator.AllColumnNames.ContainsKey(newValueForNewName));
        
            sqlGenerator.AllColumnNames[NewName] = i;

            // Prevent it from being renamed repeatedly.
            NeedsRenaming = false;
            NewName = newValueForNewName;

            // Add this column name to list of known names so that there are no subsequent
            // collisions
            sqlGenerator.AllColumnNames[newValueForNewName] = 0;
            writer.Write(SqlGenerator.QuoteIdentifier(NewName));
        }

        #endregion

    }
}
