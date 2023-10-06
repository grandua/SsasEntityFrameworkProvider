using System.Data.Common;
using System.Data.Entity.Infrastructure;

namespace AgileDesign.SsasEntityFrameworkProvider
{
    /// <summary>
    /// Use this class when you need to set Database.DefaultConnectionFactory property 
    /// for use with SsasProviderFactory. <br/>
    /// This class allows to avoid dependency on EF 4.1 and use EF 4.0 if you have to.
    /// </summary>
    public class DbConnectionFactoryAdapter
        : IDbConnectionFactory
    {
        readonly SsasProviderFactory adaptee;

        public DbConnectionFactoryAdapter(SsasProviderFactory adaptee)
        {
            this.adaptee = adaptee;
        }

        public DbConnection CreateConnection(string nameOrConnectionString)
        {
            return adaptee.CreateConnection(nameOrConnectionString);
        }
    }
}