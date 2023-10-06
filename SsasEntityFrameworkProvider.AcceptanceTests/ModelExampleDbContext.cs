using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using ModelExample;

namespace SsasEntityFrameworkProvider.AcceptanceTests
{
    public class ModelExampleDbContext : DbContext
    {
        public ModelExampleDbContext()
            : base("ModelExampleDbContext")
        {
        }

        public DbSet<Product> Products { get; set; }

        public ObjectContext ObjectContext
        {
            get { return ((IObjectContextAdapter)this).ObjectContext; }
        }
    }
}
