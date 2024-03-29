//---------------------------------------------------------------------
// <copyright file="Connection.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//---------------------------------------------------------------------

/*////////////////////////////////////////////////////////////////////////
 * Sample ADO.NET Entity Framework Provider
 *
 * This class represents a thin wrapper over the ADO.NET 2.0 SqlConnection class
 */
////////////////////////////////////////////////////////////////////////

using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;

namespace SqlEntityFrameworkProvider
{
    internal partial class EFSqlConnection : DbConnection, ICloneable
    {
        internal DbConnection _WrappedConnection = new SqlConnection();

        public EFSqlConnection()
        {
        }

        public EFSqlConnection(string connectionString)
        {
            this.ConnectionString = connectionString;
        }

        public void ClearPool()
        {
            SqlConnection.ClearPool((SqlConnection)_WrappedConnection);
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            return this._WrappedConnection.BeginTransaction(isolationLevel);
        }

        public override void ChangeDatabase(string databaseName)
        {
            this._WrappedConnection.ChangeDatabase(databaseName);
        }

        public override void Close()
        {
            this._WrappedConnection.Close();
        }

        public override string ConnectionString
        {
            get
            {
                return this._WrappedConnection.ConnectionString;
            }
            set
            {
                this._WrappedConnection.ConnectionString = value;
            }
        }

        public override int ConnectionTimeout
        {
            get
            {
                return this._WrappedConnection.ConnectionTimeout;
            }
        }

        protected override DbCommand CreateDbCommand()
        {
            DbCommand command = SqlFactory.Instance.CreateCommand();
            command.Connection = this;
            return command;
        }

        public override string Database
        {
            get { return this._WrappedConnection.Database;}
        }

        public override string DataSource
        {
            get { return this._WrappedConnection.DataSource; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                this._WrappedConnection.Dispose();
            base.Dispose(disposing);
        }

        public override void EnlistTransaction(System.Transactions.Transaction transaction)
        {
            this._WrappedConnection.EnlistTransaction(transaction);
        }

        public override DataTable GetSchema(string collectionName)
        {
            return this._WrappedConnection.GetSchema(collectionName);
        }

        public override DataTable GetSchema()
        {
            return this._WrappedConnection.GetSchema();
        }

        public override DataTable GetSchema(string collectionName, string[] restrictionValues)
        {
            return this._WrappedConnection.GetSchema(collectionName, restrictionValues);
        }

        public override void Open()
        {
            this._WrappedConnection.Open();
        }

        public override string ServerVersion
        {
            get { return this._WrappedConnection.ServerVersion; }
        }

        public override System.ComponentModel.ISite Site
        {
            get
            {
                return this._WrappedConnection.Site;
            }
            set
            {
                this._WrappedConnection.Site = value;
            }
        }

        public override ConnectionState State
        {
            get { return this._WrappedConnection.State; }
        }

        object ICloneable.Clone()
        {
            EFSqlConnection clone = new EFSqlConnection();
            clone._WrappedConnection = (DbConnection) ((ICloneable) this._WrappedConnection).Clone();
            return clone;
        }
    }
}
