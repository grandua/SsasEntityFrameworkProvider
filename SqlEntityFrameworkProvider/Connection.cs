//---------------------------------------------------------------------
// <copyright file="Connection.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//---------------------------------------------------------------------

/*////////////////////////////////////////////////////////////////////////
 * Sample ADO.NET Entity Framework Provider
 *
 * This partial Connection class overrides the protected DbProviderFactory
 * property so the Entity Framework's provider agnostic logic can access
 * the ProviderFactory class for a provider given a Connection
 */
////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.Common;

namespace SqlEntityFrameworkProvider
{
    internal partial class EFSqlConnection
    {
        protected override DbProviderFactory DbProviderFactory
        {
            get
            {
                return SqlFactory.Instance;
            }
        }
    }
}
