using System.Data;
using System.Data.Common;

namespace AgileDesign.SsasEntityFrameworkProvider.AdomdClient
{
    public class DoNothingConnection : SsasConnection
    {
        public DoNothingConnection()
        {
            ConnectionString =
                "Provider=MSOLAP.4;Data Source=.;Integrated Security=SSPI;Initial Catalog=ModelExampleCube";
        }

        public override string DataSource
        {
            get { return "."; }
        }

        public override int ConnectionTimeout
        {
            get { return 0; }
        }

        public override void Open()
        {
            state = ConnectionState.Open;
        }

        public override void Close()
        {
            state = ConnectionState.Closed;
        }

        ConnectionState state;
        public override ConnectionState State
        {
            get { return state; }
        }


        public override void ChangeDatabase(string databaseName)
        {
        }

        protected override DbCommand CreateDbCommand()
        {
            return new DoNothingCommand();
        }

        public override string ServerVersion
        { //TODO: allow to configure
            get { return "10.0.2531.0"; }
        }
    }
}