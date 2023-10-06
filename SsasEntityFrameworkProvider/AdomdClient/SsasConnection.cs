using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.AnalysisServices.AdomdClient;

namespace AgileDesign.SsasEntityFrameworkProvider.AdomdClient
{
    public class SsasConnection : DbConnection
    {
        public SsasConnection()
        {
        }

        [SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public SsasConnection(string connectionString)
        {
            ConnectionString = connectionString;
        }

        string cubeName;
        public string CubeName
        {
            get
            {
                if(cubeName == null)
                {
                    return GetCubeNameFromConnectionString();
                }
                return cubeName;
            }
            set { cubeName = value; }
        }

        string GetCubeNameFromConnectionString()
        { //Note: this is not a good option for cases when users have very many cubes in a single DB
            //TODO: Implement per query and per DbContext CubeName setter (see ToodleDo)
            return GetConnectionStringPart(connectionString, "Cube");
        }

        string connectionString;
        AdomdConnection storeConnection;

        public AdomdConnection StoreConnection
        {
            get
            {
                return storeConnection
                    ?? (
                            storeConnection = GetStoreConnection(
                                GetDbConnectionString(ConnectionString))
                       );
            }
        }

        /// <summary>
        /// Gets AdoMD.Net connection from custom connection pool 
        /// or creates a new one if the pool is empty. <br />
        /// (AdoMD.Net does not have its own out of the box connection pool)
        /// </summary>
        /// <returns>AdoMD.Net connection</returns>
        static AdomdConnection GetStoreConnection(string connectionString)
        {
            return new AdomdConnection(connectionString);
        }

        protected override DbProviderFactory DbProviderFactory
        {
            get { return SsasProviderFactory.Instance; }
        }

        public override string ConnectionString
        {
            get { return connectionString; }
            set
            {
                if (storeConnection != null)
                {
                    storeConnection.ConnectionString = GetDbConnectionString(value);
                }
                connectionString = value;
            }
        }

        public override string Database
        {
            get { return StoreConnection.Database; }
        }

        public override ConnectionState State
        {
            get { return StoreConnection.State; }
        }

        /// <remarks>
        ///   IDbConnection and AdomdCommand do not have DataSource property, but it is used by EF <br />
        ///   so it has to be extracted from a connection string. <br />
        ///   This is another reason why we cannot use AdomdConnection directly.
        /// </remarks>
        public override string DataSource
        {
            get { return GetServerInstanceNameFromConnection(ConnectionString); }
        }

        public override string ServerVersion
        {
            get { return StoreConnection.ServerVersion; }
        }

        public static SsasConnection DoNothingConnection
        {
            get { return new DoNothingConnection(); }
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            throw new NotSupportedException("No need to use transactions in readonly DB provider");
        }

        public override void ChangeDatabase(string databaseName)
        {
            StoreConnection.ChangeDatabase(databaseName);
        }

        private StateChangeEventHandler _stateChangeEventHandler;

        private void OnStateChange(StateChangeEventArgs e)
        {
            if (_stateChangeEventHandler == null)
            {
                return;
            }
            _stateChangeEventHandler(this, e);
        }

        public override event StateChangeEventHandler StateChange
        {
            add { _stateChangeEventHandler += value; }
            remove { _stateChangeEventHandler -= value; }
        }

        public override void Close()
        {
            var e = new StateChangeEventArgs(State, ConnectionState.Closed);
            StoreConnection.Close();
            OnStateChange(e);
        }

        public override void Open()
        {
            var e = new StateChangeEventArgs(State, ConnectionState.Open);
            StoreConnection.Open();
            OnStateChange(e);
        }

        string GetDbConnectionString(string connectionString)
        {
            if (connectionString == null)
            {
                return null;
            }
            if (! IsConnectionWithEFMetadata(connectionString))
            {
                return connectionString;
            }
            string quote = "&quot;|[\",']";
            string connectionWithMetadata = connectionString;
            string lookBack = string.Format("(?<={0}{1})", "provider connection string=", quote);
            string lookForward = string.Format("(?={0})", quote);
            string pattern = string.Format("{0}.*?{1}", lookBack, lookForward);
            return Regex.Match
                (
                    connectionWithMetadata,
                    pattern,
                    RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace
                )
                .Value;
        }

        bool IsConnectionWithEFMetadata(string connectionStringWithEfMetadata)
        {
            return Regex.Match
                (
                     connectionStringWithEfMetadata,
                     "provider connection string=",
                     RegexOptions.IgnoreCase
                )
                .Success;
        }

        /// <summary>
        ///   Connection string like "...;Data Source=(local);..." should return "(local)" as DataSource
        /// </summary>
        string GetServerInstanceNameFromConnection(string connectionString)
        {
            return GetConnectionStringPart(connectionString, "Data Source");
        }

        string GetConnectionStringPart
            (
                string connectionString,
                string connectionPart
            )
        {
            string connectionPartName = connectionPart.ToLower();
            //with look back
            string tillEndOfStringPattern = string.Format("(?<={0}=).*", connectionPartName); 
            string tillSemicolumnPattern = tillEndOfStringPattern + @"?(?=;)";
            //with lazy look forward for a first ";" occurrence
            string result = Regex.Match(connectionString, tillSemicolumnPattern, RegexOptions.IgnoreCase)
                .Value;
            if (( ! string.IsNullOrEmpty(result) )
                || connectionString.ToLower().Contains(string.Format("{0}=;", connectionPartName)))
            {
                return result.Trim();
            }
            //else take all remainder of a string starting from "Data Source="
            return ( Regex.Match(connectionString, tillEndOfStringPattern, RegexOptions.IgnoreCase)
                .Value ).Trim() ?? "";
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        protected override DbCommand CreateDbCommand()
        {
            return new SsasCommand
            {
                Connection = this
            };
        }
    }
}