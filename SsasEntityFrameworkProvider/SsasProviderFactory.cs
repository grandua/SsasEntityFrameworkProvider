using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity.Core.Common;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Infrastructure.DependencyResolution;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using AgileDesign.SsasEntityFrameworkProvider.AdomdClient;
using Microsoft.AnalysisServices.AdomdClient;

namespace AgileDesign.SsasEntityFrameworkProvider
{
#if ! DEBUG
    [DebuggerStepThrough]
#endif

    //public class SsasProviderDependencyResolver
    //    : IDbDependencyResolver
    //{
    //    public object GetService(Type type, object key)
    //    {
    //        //if (type != typeof(DbProviderServices))
    //        //{
    //        //    return null;
    //        //}
    //        //return SsasProvider.Instance;

    //        if (type == typeof(IDbConnectionFactory))
    //        {
    //            return new SsasProviderFactory();
    //        }
    //        return null;
    //    }

    //    public IEnumerable<object> GetServices(Type type, object key)
    //    {
    //        //if (type != typeof(DbProviderServices))
    //        //{
    //        //    return null;
    //        //}
    //        //return new [] {SsasProvider.Instance};

    //        var service = GetService(type, key);
    //        return service == null ? Enumerable.Empty<object>() : new[] { service };
    //    }
 
    //}

    public class SsasProviderFactory
        : DbProviderFactory
        , IServiceProvider
        , IDbConnectionFactory
    {
        /// <reamrks> //TODO: verify this
        ///   comment is removed from obfuscated version
        ///   This field must be a subtype of DbProviderFactory, it cannot be of DbProviderFactory type directly <br />
        ///   due to design of .Net Framework DbProviderFactories class. <br />
        ///   It must be a public static field (not property)
        /// </reamrks>
        public static SsasProviderFactory Instance = new SsasProviderFactory();

        public override bool CanCreateDataSourceEnumerator
        {
            get { return true; }
        }

        #region IDbConnectionFactory Members

        private class SqlConnectionFactoryLocal
        {
            //TODO: try to replace "Data Source=.\SQLEXPRESS" with "Data Source=."
            string baseConnectionString
                = @"Data Source=.; Integrated Security=True; MultipleActiveResultSets=True";
            string BaseConnectionString
            {
                get { return baseConnectionString; }
            }

            public DbConnection CreateConnection(string nameOrConnectionString)
            {
                string connectionString = nameOrConnectionString;
                if (!IsConnectionString(nameOrConnectionString))
                {
                    bool ignoreCase = true;
                    if (nameOrConnectionString.EndsWith(".mdf", ignoreCase, null))
                    {
                        throw new NotSupportedException(string.Format(
                            "Connection strings '{0}' for MDF databases are not supported!",
                            nameOrConnectionString));
                    }
                    SqlConnectionStringBuilder builder
                        = new SqlConnectionStringBuilder(BaseConnectionString)
                    {
                        InitialCatalog = nameOrConnectionString
                    };
                    connectionString = builder.ConnectionString;
                }
                return new SqlConnection(connectionString);
            }

            bool IsConnectionString(string nameOrConnectionString)
            {
                return (nameOrConnectionString.IndexOf('=') >= 0);
            }
        }

        public DbConnection CreateConnection(string nameOrConnectionString)
        {
            var result = CreateConnection();
            var sqlConnectionFactory = new SqlConnectionFactoryLocal();
            var prototypeConnection = sqlConnectionFactory.CreateConnection
                (
                    (nameOrConnectionString ?? "").Replace("Provider=MSOLAP.4;", ""));
            result.ConnectionString = "Provider=MSOLAP.4;"
                                      + prototypeConnection.ConnectionString;
            return result;
        }

        #endregion


        #region IServiceProvider Members

        /// <summary>
        ///   Gets the service object of the specified type.
        /// </summary>
        /// <returns>
        ///   A service object of type <paramref name = "serviceType" />.-or- null if there is no service object of type <paramref name = "serviceType" />.
        /// </returns>
        /// <param name = "serviceType">An object that specifies the type of service object to get. </param>
        /// <filterpriority>2</filterpriority>
        public virtual object GetService(Type serviceType)
        {
            if (serviceType == typeof(DbProviderServices))
            {
                return SsasProvider.Instance;
            }
            if (serviceType == typeof(DbProviderFactory))
            {
                return this;
            }
            if (serviceType == typeof(IDbConnectionFactory))
            {
                return this;
            }
            else
            {
                return null;
            }
        }

        #endregion


        public override DbCommand CreateCommand()
        {
            return new SsasCommand();
        }

        public override DbCommandBuilder CreateCommandBuilder()
        {
            //TODO: does SqlCommandBuilder work for SSAS?
            return new SqlCommandBuilder();
        }

        public override DbConnection CreateConnection()
        {
            return new SsasConnection();
        }

        public override DbConnectionStringBuilder CreateConnectionStringBuilder()
        {
            //TODO: does SqlConnectionStringBuilder work for SSAS?
            return new SqlConnectionStringBuilder();
        }

        public override DbDataAdapter CreateDataAdapter()
        {
            return new AdomdDataAdapter();
        }

        public override DbDataSourceEnumerator CreateDataSourceEnumerator()
        {
            //TODO: does it work for SSAS?
            return SqlDataSourceEnumerator.Instance;
        }

        public override DbParameter CreateParameter()
        {
            return new SsasParameter();
        }

        public override System.Security.CodeAccessPermission CreatePermission(
            System.Security.Permissions.PermissionState state)
        {
            return new SqlClientPermission(state);
        }

    }
}