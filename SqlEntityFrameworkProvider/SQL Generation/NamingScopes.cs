//---------------------------------------------------------------------
// <copyright file="SymbolTable.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//---------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Common.CommandTrees;

namespace SqlEntityFrameworkProvider
{
    /// <summary>
    ///Former SymbolTable
    /// it is a stack with a new entry for each scope (dictionary of dictionaries).
    /// Lookups search from the top of the stack to the bottom, until
    /// an entry is found.
    /// 
    /// The symbols are of the following kinds
    /// <list type="bullet">
    /// <item><see cref="AliasOrSubquery"/> represents tables (extents/nested selects/unnests)</item>
    /// <item><see cref="JoinAlias"/> represents Join nodes</item>
    /// <item><see cref="AliasOrSubquery"/> columns.</item>
    /// </list>
    /// 
    /// Symbols represent names <see cref="SqlGenerator.Visit(DbVariableReferenceExpression)"/> to be resolved, 
    /// or things to be renamed.
    /// </summary>
    internal sealed class NamingScopes
    {
        private List<Dictionary<string, AliasOrSubquery>> symbols = new List<Dictionary<string, AliasOrSubquery>>();

        internal void EnterScope()
        {
            symbols.Add(new Dictionary<string, AliasOrSubquery>(StringComparer.OrdinalIgnoreCase));
        }

        internal void ExitScope()
        {
            symbols.RemoveAt(symbols.Count - 1);
        }

        internal void Add(string name, AliasOrSubquery value)
        {
            symbols[symbols.Count - 1][name] = value;
        }

        internal AliasOrSubquery Lookup(string name)
        {
            for (int i = symbols.Count - 1; i >= 0; --i)
            {
                if (symbols[i].ContainsKey(name))
                {
                    return symbols[i][name];
                }
            }

            return null;
        }
    }
}
