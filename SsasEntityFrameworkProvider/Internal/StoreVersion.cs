using System;
using System.Data.Common;

namespace AgileDesign.SsasEntityFrameworkProvider.Internal
{
    enum StoreVersion
    {
        /// <summary>
        ///   Sql Server 9 / 2005
        /// </summary>
        Sql9 = 90,

        /// <summary>
        ///   Sql Server 10 / 2008
        /// </summary>
        Sql10 = 100,
        // higher versions go here
    }

    /// <summary>
    ///   This class is a simple utility class that determines the sql version from the 
    ///   connection
    /// </summary>
    static class StoreVersionUtils
    {
        //TODO: move out
        /// <summary>
        ///   Get the StoreVersion from the connection. Returns one of Sql8, Sql9, Sql10
        /// </summary>
        /// <param name = "connection">current sql connection</param>
        /// <returns>Sql Version for the current connection</returns>
        internal static StoreVersion GetStoreVersion(DbConnection connection)
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
                case SsasProviderManifest.TokenSql9 :
                    return StoreVersion.Sql9;

                case SsasProviderManifest.TokenSql10 :
                    return StoreVersion.Sql10;

                default :
                    //throw new ArgumentException
                    //    ("Could not determine storage version; a valid provider manifest token is required.");

                    //Support all future MS SQL Server / SSAS versions by default
                    return StoreVersion.Sql10;

            }
        }

        internal static string GetVersionHint(StoreVersion version)
        {
            switch (version)
            {
                case StoreVersion.Sql9 :
                    return SsasProviderManifest.TokenSql9;

                case StoreVersion.Sql10 :
                    return SsasProviderManifest.TokenSql10;
            }
            //throw new ArgumentException
            //    ("Could not determine storage version; a valid storage connection or a version hint is required.");

            //Support all future MS SQL Server / SSAS versions by default
            return SsasProviderManifest.TokenSql10;
        }
    }
}