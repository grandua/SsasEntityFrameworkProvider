//---------------------------------------------------------------------
// <copyright file="ProviderServices.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//---------------------------------------------------------------------

/*/////////////////////////////////////////////////////////////////////////////
 * Sample ADO.NET Entity Frameworke Provider for Visual Studio 2010 notes
 *
 * The ProviderServices class is the starting point for accessing
 * the SQL generation layer to convert CommandTrees into DbCommands
 * and additional data store information such as the provider manifest, 
 * which describes type store specific mappings and functions, and the
 * store specific mapping files to generate queries for table and column
 * information. ProviderServices class also includes entry points for 
 * creating databases by executing DDL scripts, as well as support for 
 * checking if a database exists and deleting databases
 */
////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Entity.Core.Common;
using System.Data.Entity.Core.Common.CommandTrees;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;

namespace SqlEntityFrameworkProvider
{
    internal class SqlProviderServices : DbProviderServices
    {
        internal static readonly SqlProviderServices Instance = new SqlProviderServices();

        protected override DbCommandDefinition CreateDbCommandDefinition(DbProviderManifest manifest, DbCommandTree commandTree)
        {
            DbCommand prototype = CreateCommand(manifest, commandTree);
            DbCommandDefinition result = this.CreateCommandDefinition(prototype);
            return result;
        }

        /// <summary>
        /// Create a EFSqlCommand object, given the provider manifest and command tree
        /// </summary>
        private DbCommand CreateCommand(DbProviderManifest manifest, DbCommandTree commandTree)
        {
            if (manifest == null)
                throw new ArgumentNullException("manifest");

            if (commandTree == null)
                throw new ArgumentNullException("commandTree");

            SqlProviderManifest sqlManifest = (manifest as SqlProviderManifest);
            if (sqlManifest == null)
            {
                throw new ArgumentException("The provider manifest given is not of type 'SqlProviderManifest'.");
            }

            StoreVersion version = sqlManifest.Version;

            EFSqlCommand command = new EFSqlCommand();

            List<DbParameter> parameters;
            CommandType commandType;

            command.CommandText = SqlGenerator.GenerateSql(commandTree, version, out parameters, out commandType);
            command.CommandType = commandType;

            if (command.CommandType == CommandType.Text)
            {
                command.CommandText += Environment.NewLine + Environment.NewLine + "-- provider: " + this.GetType().Assembly.FullName;
            }

            // Get the function (if any) implemented by the command tree since this influences our interpretation of parameters
            EdmFunction function = null;
            if (commandTree is DbFunctionCommandTree)
            {
                function = ((DbFunctionCommandTree)commandTree).EdmFunction;
            }

            // Now make sure we populate the command's parameters from the CQT's parameters:
            foreach (KeyValuePair<string, TypeUsage> queryParameter in commandTree.Parameters)
            {
                SqlParameter parameter;

                // Use the corresponding function parameter TypeUsage where available (currently, the SSDL facets and 
                // type trump user-defined facets and type in the EntityCommand).
                FunctionParameter functionParameter;
                if (null != function && function.Parameters.TryGetValue(queryParameter.Key, false, out functionParameter))
                {
                    parameter = CreateSqlParameter(functionParameter.Name, functionParameter.TypeUsage, functionParameter.Mode, DBNull.Value);
                }
                else
                {
                    parameter = CreateSqlParameter(queryParameter.Key, queryParameter.Value, ParameterMode.In, DBNull.Value);
                }

                command.Parameters.Add(parameter);
            }

            // Now add parameters added as part of SQL gen (note: this feature is only safe for DML SQL gen which
            // does not support user parameters, where there is no risk of name collision)
            if (null != parameters && 0 < parameters.Count)
            {
                if (!(commandTree is DbInsertCommandTree) &&
                    !(commandTree is DbUpdateCommandTree) &&
                    !(commandTree is DbDeleteCommandTree))
                {
                    throw new InvalidOperationException("SqlGenParametersNotPermitted");
                }

                foreach (DbParameter parameter in parameters)
                {
                    command.Parameters.Add(parameter);
                }
            }

            return command;
        }

        protected override string GetDbProviderManifestToken(DbConnection connection)
        {
            if (connection == null)
                throw new ArgumentException("connection");

            EFSqlConnection efSqlConnection = connection as EFSqlConnection;
            if (efSqlConnection == null)
            {
                throw new ArgumentException("The connection is not of type 'EFSqlConnection'.");
            }

            if (string.IsNullOrEmpty(efSqlConnection.ConnectionString))
            {
                throw new ArgumentException("Could not determine storage version; a valid storage connection or a version hint is required.");
            }

            bool closeConnection = false;
            try
            {
                if (efSqlConnection.State != ConnectionState.Open)
                {
                    efSqlConnection.Open();
                    closeConnection = true;
                }

                StoreVersion version = StoreVersionUtils.GetStoreVersion(efSqlConnection);
                if (version == StoreVersion.Sql9)
                    return SqlProviderManifest.TokenSql9;
                else

                return StoreVersionUtils.GetVersionHint(version);
            }
            finally
            {
                if (closeConnection)
                {
                    efSqlConnection.Close();
                }
            }
        }

        protected override DbProviderManifest GetDbProviderManifest(string versionHint)
        {
            if (string.IsNullOrEmpty(versionHint))
            {
                throw new ArgumentException("Could not determine store version; a valid store connection or a version hint is required.");
            }

            return new SqlProviderManifest(versionHint);
        }

        protected override string DbCreateDatabaseScript(string providerManifestToken, StoreItemCollection storeItemCollection)
        {
            if (providerManifestToken == null)
                throw new ArgumentNullException("providerManifestToken must not be null");

            if( storeItemCollection == null)
                throw new ArgumentNullException("storeItemCollection must not be null");
            
            return DdlBuilder.CreateObjectsScript(storeItemCollection);            
        }

        protected override void DbCreateDatabase(DbConnection connection, int? commandTimeout, StoreItemCollection storeItemCollection)
        {
            if (connection == null)
                throw new ArgumentNullException("connection must not be null");

            if (storeItemCollection == null)
                throw new ArgumentNullException("storeItemCollection must not be null");

            EFSqlConnection efSqlConnection = connection as EFSqlConnection;
            if (efSqlConnection == null)
            {
                throw new ArgumentException("The connection is not of type 'EFSqlConnection'.");
            }

            string databaseName = GetDatabaseName(efSqlConnection);
            if (string.IsNullOrEmpty(databaseName))
            {
                throw new InvalidOperationException("Initial Catalog is missing from the connection string");
            }

            string dataFileName, logFileName;
            GetDatabaseFileNames(efSqlConnection, out dataFileName, out logFileName);

            string createDatabaseScript = DdlBuilder.CreateDatabaseScript(databaseName, dataFileName, logFileName);
            string createObjectsScript = DdlBuilder.CreateObjectsScript(storeItemCollection);

            UsingMasterConnection(efSqlConnection, conn =>
            {
                // create database
                CreateCommand(conn, createDatabaseScript, commandTimeout).ExecuteNonQuery();
            });

            // Clear connection pool for the database connection since after the 'create database' call, a previously
            // invalid connection may now be valid.                
            efSqlConnection.ClearPool();

            UsingConnection(efSqlConnection, conn =>
            {
                // create database objects
                CreateCommand(conn, createObjectsScript, commandTimeout).ExecuteNonQuery();
            });

        }

        private static string GetDatabaseName(EFSqlConnection efSqlConnection)
        {
            string databaseName = efSqlConnection.Database;
            if (string.IsNullOrEmpty(databaseName))
                throw new InvalidOperationException("Connection String did not specify an Initial Catalog");

            return databaseName;
        }

        private static void GetDatabaseFileNames(EFSqlConnection connection, out string dataFileName, out string logFileName)
        {
            if (connection == null)
                throw new ArgumentNullException("connection must not be null");


            var connectionStringBuilder = new SqlConnectionStringBuilder(connection.ConnectionString);
            string attachDBFile = connectionStringBuilder.AttachDBFilename;
            if (string.IsNullOrEmpty(attachDBFile))
            {
                dataFileName = null;
                logFileName = null;
            }
            else
            {                
                //Handle the case when attachDBFilename starts with |DataDirectory|                
                dataFileName = ExpandDataDirectory(attachDBFile);

                //Handle the other cases
                dataFileName = dataFileName ?? attachDBFile;
                logFileName = Path.ChangeExtension(dataFileName, "ldf");
            }
        }        

        private static string ExpandDataDirectory(string filenameWithMacro)
        {            
            string dataDir = null;
            const string DataDirectory = "|DataDirectory|";

            if (filenameWithMacro == null || filenameWithMacro.Length <= DataDirectory.Length)
                return null;            

            if (!filenameWithMacro.StartsWith(DataDirectory, StringComparison.OrdinalIgnoreCase))
                return null;

            dataDir = AppDomain.CurrentDomain.GetData("DataDirectory") as string;
            if (string.IsNullOrEmpty(dataDir))            
                dataDir = AppDomain.CurrentDomain.BaseDirectory;            

            string dbFilename = filenameWithMacro.Substring(DataDirectory.Length, filenameWithMacro.Length - DataDirectory.Length);

            // See if dataDir ends with a '\'
            bool dataDirEndsWith = (0 < dataDir.Length) && (dataDir[dataDir.Length - 1] == '\\');
            if (dataDirEndsWith)
            {
                // remove the trailing '\'
                dataDir = dataDir.Substring(0, dataDir.Length - 1);
            }

            // see if dbFilename starts with a '\'
            bool dbFilenameStartsWith = (0 < dbFilename.Length) && (dbFilename[0] == '\\');
            if (!dbFilenameStartsWith)
            {
                // add a leading '\'
                dbFilename = string.Concat("\\", dbFilename);
            }

            string expandedPath = string.Concat(dataDir, dbFilename);            
            return expandedPath;
        }
        
        protected override bool DbDatabaseExists(DbConnection connection, int? commandTimeout, StoreItemCollection storeItemCollection)
        {
            if (connection == null)
                throw new ArgumentNullException("connection must not be null");

            if (storeItemCollection == null)
                throw new ArgumentNullException("storeItemCollection must not be null");

            EFSqlConnection efSqlConnection = connection as EFSqlConnection;
            if (efSqlConnection == null)
                throw new ArgumentException("connection must be a valid EFSqlConnection");

            string databaseName = GetDatabaseName(efSqlConnection);

            bool exists = false;
            UsingMasterConnection(efSqlConnection, conn =>
            {
                StoreVersion storeVersion = StoreVersionUtils.GetStoreVersion(conn);
                string databaseExistsScript = DdlBuilder.CreateDatabaseExistsScript(databaseName);

                int result = (int)CreateCommand(conn, databaseExistsScript, commandTimeout).ExecuteScalar();
                exists = (result == 1);
            });

            return exists;
        }       

        protected override void DbDeleteDatabase(DbConnection connection, int? commandTimeout, StoreItemCollection storeItemCollection)
        {
            if (connection == null)
                throw new ArgumentNullException("connection must not be null");

            if (storeItemCollection == null)
                throw new ArgumentNullException("storeItemCollection must not be null");

            EFSqlConnection efSqlConnection = connection as EFSqlConnection;
            if (efSqlConnection == null)
                throw new ArgumentException("connection must be a valid EFSqlConnection");

            string databaseName = GetDatabaseName(efSqlConnection);
            string dropDatabaseScript = DdlBuilder.DropDatabaseScript(databaseName);

            // clear the connection pool in case someone is holding on to the database
            efSqlConnection.ClearPool();

            UsingMasterConnection(efSqlConnection, (conn) =>
            {
                CreateCommand(conn, dropDatabaseScript, commandTimeout).ExecuteNonQuery();
            });
        }

        private static EFSqlCommand CreateCommand(EFSqlConnection connection, string commandText, int? commandTimeout)
        {
            Debug.Assert(connection != null);
            if (string.IsNullOrEmpty(commandText))
            {
                // SqlCommand will complain if the command text is empty
                commandText = Environment.NewLine;
            }
            var command = new EFSqlCommand(commandText, connection);
            if (commandTimeout.HasValue)
            {
                command.CommandTimeout = commandTimeout.Value;
            }
            return command;
        }

        private static void UsingConnection(EFSqlConnection connection, Action<EFSqlConnection> act)
        {
            // remember the connection string so that we can reset it if credentials are wiped
            string holdConnectionString = connection.ConnectionString;
            bool openingConnection = connection.State == ConnectionState.Closed;
            if (openingConnection)
            {
                connection.Open();
            }
            try
            {
                act(connection);
            }
            finally
            {
                if (openingConnection && connection.State == ConnectionState.Open)
                {
                    // if we opened the connection, we should close it
                    connection.Close();
                }
                if (connection.ConnectionString != holdConnectionString)
                {
                    connection.ConnectionString = holdConnectionString;
                }
            }
        }

        private static void UsingMasterConnection(EFSqlConnection connection, Action<EFSqlConnection> act)
        {
            var connectionBuilder = new SqlConnectionStringBuilder(connection.ConnectionString)
            {
                InitialCatalog = "master",
                AttachDBFilename = string.Empty, // any AttachDB path specified is not relevant to master
            };

            try
            {
                using (var masterConnection = new EFSqlConnection(connectionBuilder.ConnectionString))
                {
                    UsingConnection(masterConnection, act);
                }
            }
            catch (SqlException)
            {
                // if it appears that the credentials have been removed from the connection string, use an alternate explanation
                if (!connectionBuilder.IntegratedSecurity &&
                    (string.IsNullOrEmpty(connectionBuilder.UserID) || string.IsNullOrEmpty(connectionBuilder.Password)))
                {
                    throw new InvalidOperationException("Credentials are missing from the connection string");
                }
                throw;
            }
        }

        /// <summary>
        /// Creates a SqlParameter given a name, type, and direction
        /// </summary>
        internal static SqlParameter CreateSqlParameter(string name, TypeUsage type, ParameterMode mode, object value)
        {
            int? size;

            SqlParameter result = new SqlParameter(name, value);

            // .Direction
            ParameterDirection direction = MetadataHelpers.ParameterModeToParameterDirection(mode);
            if (result.Direction != direction)
            {
                result.Direction = direction;
            }

            // .Size and .SqlDbType
            // output parameters are handled differently (we need to ensure there is space for return
            // values where the user has not given a specific Size/MaxLength)
            bool isOutParam = mode != ParameterMode.In;
            SqlDbType sqlDbType = GetSqlDbType(type, isOutParam, out size);

            if (result.SqlDbType != sqlDbType)
            {
                result.SqlDbType = sqlDbType;
            }

            // Note that we overwrite 'facet' parameters where either the value is different or
            // there is an output parameter.
            if (size.HasValue && (isOutParam || result.Size != size.Value))
            {
                result.Size = size.Value;
            }

            // .IsNullable
            bool isNullable = MetadataHelpers.IsNullable(type);
            if (isOutParam || isNullable != result.IsNullable)
            {
                result.IsNullable = isNullable;
            }

            return result;
        }


        /// <summary>
        /// Determines SqlDbType for the given primitive type. Extracts facet
        /// information as well.
        /// </summary>
        private static SqlDbType GetSqlDbType(TypeUsage type, bool isOutParam, out int? size)
        {
            // only supported for primitive type
            PrimitiveTypeKind primitiveTypeKind = MetadataHelpers.GetPrimitiveTypeKind(type);

            size = default(int?);


            // TODO add logic for Xml here
            switch (primitiveTypeKind)
            {
                case PrimitiveTypeKind.Binary:
                    // for output parameters, ensure there is space...
                    size = GetParameterSize(type, isOutParam);
                    return GetBinaryDbType(type);

                case PrimitiveTypeKind.Boolean:
                    return SqlDbType.Bit;

                case PrimitiveTypeKind.Byte:
                    return SqlDbType.TinyInt;

                case PrimitiveTypeKind.Time:
                    return SqlDbType.Time;

                case PrimitiveTypeKind.DateTimeOffset:
                    return SqlDbType.DateTimeOffset;

                case PrimitiveTypeKind.DateTime:
                    return SqlDbType.DateTime;

                case PrimitiveTypeKind.Decimal:
                    return SqlDbType.Decimal;

                case PrimitiveTypeKind.Double:
                    return SqlDbType.Float;

                case PrimitiveTypeKind.Guid:
                    return SqlDbType.UniqueIdentifier;

                case PrimitiveTypeKind.Int16:
                    return SqlDbType.SmallInt;

                case PrimitiveTypeKind.Int32:
                    return SqlDbType.Int;

                case PrimitiveTypeKind.Int64:
                    return SqlDbType.BigInt;

                case PrimitiveTypeKind.SByte:
                    return SqlDbType.SmallInt;

                case PrimitiveTypeKind.Single:
                    return SqlDbType.Real;

                case PrimitiveTypeKind.String:
                    size = GetParameterSize(type, isOutParam);
                    return GetStringDbType(type);

                default:
                    Debug.Fail("unknown PrimitiveTypeKind " + primitiveTypeKind);
                    return SqlDbType.Variant;
            }
        }

        /// <summary>
        /// Determines preferred value for SqlParameter.Size. Returns null
        /// where there is no preference.
        /// </summary>
        private static int? GetParameterSize(TypeUsage type, bool isOutParam)
        {
            int maxLength;
            if (MetadataHelpers.TryGetMaxLength(type, out maxLength))
            {
                // if the MaxLength facet has a specific value use it
                return maxLength;
            }
            else if (isOutParam)
            {
                // if the parameter is a return/out/inout parameter, ensure there 
                // is space for any value
                return int.MaxValue;
            }
            else
            {
                // no value
                return default(int?);
            }
        }

        /// <summary>
        /// Chooses the appropriate SqlDbType for the given string type.
        /// </summary>
        private static SqlDbType GetStringDbType(TypeUsage type)
        {
            Debug.Assert(type.EdmType.BuiltInTypeKind == BuiltInTypeKind.PrimitiveType &&
                PrimitiveTypeKind.String == ((PrimitiveType)type.EdmType).PrimitiveTypeKind, "only valid for string type");

            SqlDbType dbType;
            if (type.EdmType.Name.ToLowerInvariant() == "xml")
            {
                dbType = SqlDbType.Xml;
            }
            else
            {
                // Specific type depends on whether the string is a unicode string and whether it is a fixed length string.
                // By default, assume widest type (unicode) and most common type (variable length)
                bool unicode;
                bool fixedLength;
                if (!MetadataHelpers.TryGetIsFixedLength(type, out fixedLength))
                {
                    fixedLength = false;
                }

                if (!MetadataHelpers.TryGetIsUnicode(type, out unicode))
                {
                    unicode = true;
                }

                if (fixedLength)
                {
                    dbType = (unicode ? SqlDbType.NChar : SqlDbType.Char);
                }
                else
                {
                    dbType = (unicode ? SqlDbType.NVarChar : SqlDbType.VarChar);
                }
            }
            return dbType;
        }

        /// <summary>
        /// Chooses the appropriate SqlDbType for the given binary type.
        /// </summary>
        private static SqlDbType GetBinaryDbType(TypeUsage type)
        {
            Debug.Assert(type.EdmType.BuiltInTypeKind == BuiltInTypeKind.PrimitiveType &&
                PrimitiveTypeKind.Binary == ((PrimitiveType)type.EdmType).PrimitiveTypeKind, "only valid for binary type");

            // Specific type depends on whether the binary value is fixed length. By default, assume variable length.
            bool fixedLength;
            if (!MetadataHelpers.TryGetIsFixedLength(type, out fixedLength))
            {
                fixedLength = false;
            }

            return fixedLength ? SqlDbType.Binary : SqlDbType.VarBinary;
        }
    }
}

