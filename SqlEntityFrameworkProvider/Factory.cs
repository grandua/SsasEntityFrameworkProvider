//---------------------------------------------------------------------
// <copyright file="Factory.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//---------------------------------------------------------------------

/*/////////////////////////////////////////////////////////////////////////////
 * Sample ADO.NET Entity Framework Provider
 *
 * This partial ProviderFactory class implements the IServiceProvider interface
 * and supports returning instances of a ProviderServices class.
 * The Entity Framework uses IServiceProvider to access the ProviderServices
 * class given a ProviderFactory
 */
////////////////////////////////////////////////////////////////////////////

using System;
using System.Data.Entity.Core.Common;

namespace SqlEntityFrameworkProvider
{
    internal partial class SqlFactory : IServiceProvider
    {
        //Implement IServiceProvider
        object IServiceProvider.GetService(Type serviceType)
        {
            if (serviceType == typeof(DbProviderServices))
                return SqlProviderServices.Instance;
            else
                return null;
        }
    }
}
