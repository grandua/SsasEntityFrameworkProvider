﻿//---------------------------------------------------------------------
// <copyright file="StoreVersion.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//---------------------------------------------------------------------

using System;

namespace SqlEntityFrameworkProvider
{
    /// <summary>
    /// This enum describes the current server version
    /// </summary>
    internal enum StoreVersion
    {
        /// <summary>
        /// Sql Server 9
        /// </summary>
        Sql9 = 90,

        /// <summary>
        /// Sql Server 10
        /// </summary>
        Sql10 = 100,

        // higher versions go here
    }

    /// <summary>
    /// This class is a simple utility class that determines the sql version from the 
    /// connection
    /// </summary>
    internal static class StoreVersionUtils
    {
        /// <summary>
        /// Get the StoreVersion from the connection. Returns one of Sql8, Sql9, Sql10
        /// </summary>
        /// <param name="connection">current sql connection</param>
        /// <returns>Sql Version for the current connection</returns>
        internal static StoreVersion GetStoreVersion(EFSqlConnection connection)
        {
            if (connection.ServerVersion.StartsWith("10.", StringComparison.Ordinal))
            {
                return StoreVersion.Sql10;
            }
            else if (connection.ServerVersion.StartsWith("09.", StringComparison.Ordinal))
            {
                return StoreVersion.Sql9;
            }
            else
            {
                throw new ArgumentException("The version of SQL Server is not supported via sample provider.");
            }
        }

        internal static StoreVersion GetStoreVersion(string providerManifestToken)
        {
            switch (providerManifestToken)
            {
                case SqlProviderManifest.TokenSql9:
                    return StoreVersion.Sql9;

                case SqlProviderManifest.TokenSql10:
                    return StoreVersion.Sql10;

                default:
                    throw new ArgumentException("Could not determine storage version; a valid provider manifest token is required.");
            }
        }

        internal static string GetVersionHint(StoreVersion version)
        {
            switch (version)
            {
                case StoreVersion.Sql9:
                    return SqlProviderManifest.TokenSql9;

                case StoreVersion.Sql10:
                    return SqlProviderManifest.TokenSql10;
            }

            throw new ArgumentException("Could not determine storage version; a valid storage connection or a version hint is required.");
        }
    }
}
