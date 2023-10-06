using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using AgileDesign.SsasEntityFrameworkProvider.Internal;
using AgileDesign.SsasEntityFrameworkProvider.Utilities;
using AgileDesign.Utilities;
using Microsoft.AnalysisServices.AdomdClient;

namespace AgileDesign.SsasEntityFrameworkProvider.AdomdClient
{
    /// <remarks>
    ///   AdomdConnection is a sealed class (as well as SqlConnection)
    ///   So we have to inherit from DbCommand rather than from AdomdConnection and wrap AdomdConnection
    /// </remarks>
    public class SsasCommand
        : DbCommand,
          ICloneable
    {
        public SsasCommand()
        {
        }

        public SsasCommand(AdomdCommand adomdCommand)
        {
            storeCommand = adomdCommand;
        }

        DbConnection dbConnection;
        bool designTimeVisible = true;

        AdomdCommand storeCommand;

        AdomdCommand StoreCommand
        {
            get
            {
                return storeCommand 
                    ?? ( storeCommand = CreateStoreCommand() );
            }
        }

        public override string CommandText
        {
            get { return StoreCommand.CommandText; }
            set { StoreCommand.CommandText = value; }
        }

        public override int CommandTimeout
        {
            get { return StoreCommand.CommandTimeout; }
            set { StoreCommand.CommandTimeout = value; }
        }

        public override CommandType CommandType
        {
            get { return StoreCommand.CommandType; }
            set { StoreCommand.CommandType = value; }
        }
        /// <summary>
        /// Underlying AdomdCommand does not support UpdatedRowSource
        /// </summary>
        public override UpdateRowSource UpdatedRowSource { get; set; }


        /// <remarks>
        ///   AdomdConnection implements IDbConnection but does not inherit from DbConnection
        ///   Because DbCommand exposes DbConnection instead of IDbConnection DbConnection also has to be wrapped
        /// </remarks>
        protected override DbConnection DbConnection
        {
            get { return dbConnection; }
            set
            {
                Contract.Requires(value == null || value is SsasConnection);

                if (storeCommand != null && value != null)
                {
                    storeCommand.Connection = ( (SsasConnection)value ).StoreConnection;
                }
                dbConnection = value;
            }
        }

        DbParameterCollection dbParameterCollection;

        protected override DbParameterCollection DbParameterCollection
        {
            get
            {
                return dbParameterCollection 
                    ?? ( dbParameterCollection = new SsasParameterCollection() );
            }
        }

        protected override DbTransaction DbTransaction
        {
            get { throw CreateTransactionsNotSupportedException(); }
            set
            {
                if (value != null)
                {
                    throw CreateTransactionsNotSupportedException();
                }
            }
        }

        public override bool DesignTimeVisible
        {
            get { return designTimeVisible; }
            set { designTimeVisible = value; }
        }

        internal static DoNothingCommand DoNothingCommand
        {
            get { return new DoNothingCommand(); }
        }

        public IDictionary<int, int> MdxColumnsOrder { get; set; }


        #region ICloneable Members

        public object Clone()
        {
            return new SsasCommand(StoreCommand.Clone())
            {
                Connection = Connection,
                MdxColumnsOrder = MdxColumnsOrder,
                dbParameterCollection = Parameters
            };
        }

        #endregion

        AdomdCommand CreateStoreCommand()
        {
            AdomdConnection storeConnection = null;
            if (Connection != null)
            {
                storeConnection = ( (SsasConnection)Connection ).StoreConnection;
            }
            return new AdomdCommand(null, storeConnection);
        }

        public override void Prepare()
        {
            StoreCommand.Prepare();
        }

        Exception CreateTransactionsNotSupportedException()
        {
            return new NotSupportedException
                (
                string.Format
                    (
                        "Transactions are not supported - '{0}' is readonly data provider"
                        ,
                        GetType().Assembly.GetName().Name));
        }

        public override void Cancel()
        {
            StoreCommand.Cancel();
        }

        protected override DbParameter CreateDbParameter()
        {
            return new SsasParameter(StoreCommand.CreateParameter());
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            if((CommandText ?? "").Contains(SsasProviderManifest.CubeNamePlaceholder))
            {
                ReplaceCubeNamePlaceholderWithConnectionCube();
            }
            ReplaceParameterNamesWithValues();
            Logger.TraceVerbose("Before execution CommandText='\r\n{0}'", CommandText);
            return new SsasDataReader(StoreCommand.ExecuteReader(behavior))
            {
                MdxColumnsOrder = MdxColumnsOrder
            };
        }

        void ReplaceParameterNamesWithValues()
        {
            foreach (var parameter in Parameters)
            {
                ReplaceParameterNameWithValue((IDataParameter)parameter);
                ReplaceNameParameterNameWithValue((IDataParameter)parameter);
            }
        }

        void ReplaceParameterNameWithValue(IDataParameter parameter)
        {
            CommandText = CommandText.Replace(
                string.Format("'<{0}>'", parameter.ParameterName)
                , parameter.Value.Enquote());

            CommandText = CommandText.Replace(
                string.Format("[<{0}>]", parameter.ParameterName)
                , string.Format("[{0}]", parameter.Value));

            CommandText = CommandText.Replace(
                string.Format("@{0}", parameter.ParameterName)
                , parameter.Value.Enquote());

        }

        /// <summary>
        /// Name parameters represent MDX names and should not be in quotes
        /// </summary>
        /// <param name="parameter">
        /// SsasParameter for an MDX name string
        /// </param>
        void ReplaceNameParameterNameWithValue(IDataParameter parameter)
        {
            CommandText = CommandText.Replace(
                string.Format("'{0}{1}>'", SsasParameter.NameParamerterPrefix , parameter.ParameterName)
                , parameter.Value.ToString());
        }

        void ReplaceCubeNamePlaceholderWithConnectionCube()
        {
            CommandText = CommandText.Replace
                (
                    SsasProviderManifest.CubeNamePlaceholder,
                    GetCubeName()
                );
        }

        string GetCubeName()
        {
            string result = ( (SsasConnection)Connection ).CubeName;
            if (string.IsNullOrWhiteSpace(result))
            {
                throw CreateCubeNameNotSpecifiedException();
            }
            result = result.Trim();
            if( ! result.StartsWith("["))
            {
                result = string.Format("[{0}]", result);
            }
            return result;
        }

        InvalidOperationException CreateCubeNameNotSpecifiedException()
        { //TODO: Change this message when other options than connection string 'cube=;' are implemented
            return new InvalidOperationException(
                "Please, specify cube name in connection string (e.g. 'Cube=NorthwindEF;')");
        }

        public override int ExecuteNonQuery()
        {
            return StoreCommand.ExecuteNonQuery();
        }

        public override object ExecuteScalar()
        {
            return StoreCommand.ExecuteScalar();
        }
    }
}