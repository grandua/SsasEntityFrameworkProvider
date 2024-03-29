//---------------------------------------------------------------------
// <copyright file="Command.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//---------------------------------------------------------------------

/*////////////////////////////////////////////////////////////////////////
 * Sample ADO.NET Entity Framework Provider
 *
 * This class represents a thin wrapper over the ADO.NET 2.0 SqlCommand class
 */
////////////////////////////////////////////////////////////////////////

using System.Data;
using System.Data.Common;
using System.Data.SqlClient;

namespace SqlEntityFrameworkProvider
{
    internal partial class EFSqlCommand : DbCommand
    {
        internal DbCommand _WrappedCommand = new SqlCommand();

        public EFSqlCommand()
        {
        }

        public EFSqlCommand(string commandText)
        {
            this.InitializeMe(commandText, null, null);
        }

        public EFSqlCommand(string commandText, EFSqlConnection connection)
        {
            this.InitializeMe(commandText, connection, null);
        }

        public EFSqlCommand(string commandText, EFSqlConnection connection, DbTransaction transaction)
        {
            this.InitializeMe(commandText, connection, transaction);
        }

        private void InitializeMe(string commandText, EFSqlConnection connection, DbTransaction transaction)
        {
            this.CommandText = commandText;
            this.Connection = connection;
            this.Transaction = transaction;
        }

        public override void Cancel()
        {
            this._WrappedCommand.Cancel();
        }

        public override string CommandText
        {
            get
            {
                return this._WrappedCommand.CommandText;
            }
            set
            {
                this._WrappedCommand.CommandText = value;
            }
        }

        public override int CommandTimeout
        {
            get
            {
                return this._WrappedCommand.CommandTimeout;
            }
            set
            {
                this._WrappedCommand.CommandTimeout = value;
            }
        }

        public override CommandType CommandType
        {
            get
            {
                return this._WrappedCommand.CommandType;
            }
            set
            {
                this._WrappedCommand.CommandType = value;
            }
        }

        protected override DbParameter CreateDbParameter()
        {
            return this._WrappedCommand.CreateParameter();
        }

        private EFSqlConnection _Connection = null;
        protected override DbConnection DbConnection
        {
            get
            {
                return this._Connection;
            }
            set
            {
                this._Connection = (EFSqlConnection) value;
                this._WrappedCommand.Connection = this._Connection._WrappedConnection;
            }
        }

        protected override DbParameterCollection DbParameterCollection
        {
            get { return this._WrappedCommand.Parameters; }
        }

        private DbTransaction _Transaction = null;
        protected override DbTransaction DbTransaction
        {
            get
            {
                return this._Transaction;
            }
            set
            {
                this._Transaction = value;
                this._WrappedCommand.Transaction = this._Transaction;
            }
        }

        private bool _DesignTimeVisible = true;
        public override bool DesignTimeVisible
        {
            get
            {
                return this._DesignTimeVisible;
            }
            set
            {
                this._DesignTimeVisible = value;
            }
        }
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            return this._WrappedCommand.ExecuteReader(behavior);
        }

        public override int ExecuteNonQuery()
        {
            return this._WrappedCommand.ExecuteNonQuery();
        }

        public override object ExecuteScalar()
        {
            return this._WrappedCommand.ExecuteScalar();
        }

        public override void Prepare()
        {
            this._WrappedCommand.Prepare();
        }

        public override UpdateRowSource UpdatedRowSource
        {
            get
            {
                return this._WrappedCommand.UpdatedRowSource;
            }
            set
            {
                this._WrappedCommand.UpdatedRowSource = value;
            }
        }
    }
}
