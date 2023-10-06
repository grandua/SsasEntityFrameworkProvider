using System.Data;
using System.Data.Common;
using Microsoft.AnalysisServices.AdomdClient;

namespace AgileDesign.SsasEntityFrameworkProvider.AdomdClient
{
    public class DoNothingCommand : SsasCommand
    {
        DbConnection dbConnection;
        public override string CommandText { get; set; }
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection DbConnection
        {
            get
            {
                return dbConnection
                       ?? ( dbConnection = SsasConnection.DoNothingConnection );
            }
            set { }
        }

        public override bool DesignTimeVisible { get; set; }

        protected override DbParameter CreateDbParameter()
        {
            return new SsasParameter();
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            return new DoNothingDataReader();
        }

        public override void Prepare()
        {
        }

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery()
        {
            return 0;
        }

        public override object ExecuteScalar()
        {
            return null;
        }
    }
}