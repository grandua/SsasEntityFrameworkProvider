using System.Data.Common;
using System.Data.Entity.Infrastructure;
using AgileDesign.SsasEntityFrameworkProvider;
using AgileDesign.SsasEntityFrameworkProvider.AdomdClient;
using SsasEntityFrameworkProvider.AcceptanceTests;
using Xunit;

namespace SsasEntityFrameworkProvider.UnitTests
{
    public class DbContextFacts : DbContextAcceptance
    {
        protected override IDbConnectionFactory CreateConnectionFactory()
        {
            return new DbConnectionFactoryAdapter(new SsasProviderFactoryStub());
        }

        [Fact(Skip="Implement this when I proceed to unit tests")]
        public new void SimpleMdxStoreQuery()
        {
            //base.SimpleMdxStoreQuery();
        }
    }

    /// <summary>
    /// SsasProviderFactoryStub is instantiated based on config file
    /// </summary>
    class SsasProviderFactoryStub : SsasProviderFactory
    {
        /// <summary>
        /// Used by EF
        /// </summary>
        public static new SsasProviderFactoryStub Instance = new SsasProviderFactoryStub();

        public override DbConnection CreateConnection()
        {
            return new DoNothingConnectionStub();
        }

        /// <remarks>
        /// I have to have a class in this assembly to make EF provider detection work
        /// </remarks>
        private class DoNothingConnectionStub : DoNothingConnection
        {
            protected override DbProviderFactory DbProviderFactory
            {
                get { return new SsasProviderFactoryStub(); }
            }
        }
    }

}
