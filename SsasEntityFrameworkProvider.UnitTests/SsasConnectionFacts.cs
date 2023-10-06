using System.Collections.Generic;
using System.Linq;
using AgileDesign.SsasEntityFrameworkProvider.AdomdClient;
using Xunit;

namespace SsasEntityFrameworkProvider.UnitTests
{
    public class SsasConnectionFacts
    {
        [Fact]
        public void DataSourcePropertyExtractsItFromConnectionString()
        {
            IEnumerable<string> connections
                = new[]
                      {
                          //Data Source at the end
                          "Provider=MSOLAP.4;Integrated Sity=SSPI;Initial Catalog=ModelExampleCube;Data Source=(local)",
                          //in the middle
                          "Provider=MSOLAP.4;Data Source=(local);Integrated Security=SSPI;Initial Catalog=ModelExampleCube",
                          //at the beginning
                          "Data Source=(local);Provider=MSOLAP.4;Integrated Security=SSPI;Initial Catalog=ModelExampleCube"
                      };
            //make sure it works regardless "Data Source=" or "data source=" case
            connections = connections.Union(connections.Select(c => c.ToLower()));

            foreach (string connectionString in connections)
            {
                var connection = new SsasConnection();
                connection.ConnectionString = connectionString;
                Assert.Equal("(local)", connection.DataSource);
            }
        }
                                      [Fact]
        public void EmptyDataSourceDoesNotBreak()
        {
            IEnumerable<string> connections
                = new[]
                      {
                          "Data Source=  ;Provider=MSOLAP.4;Integrated Security=SSPI;Initial Catalog=ModelExampleCube",
                          "Data Source=;Provider=MSOLAP.4;Integrated Security=SSPI;Initial Catalog=ModelExampleCube",
                          " "
                      };
            foreach (string connectionString in connections)
            {
                var connection = new SsasConnection();
                connection.ConnectionString = connectionString;
                Assert.Equal("", connection.DataSource);
            }
        }
    }
}
