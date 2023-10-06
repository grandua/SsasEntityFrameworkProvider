using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using AgileDesign.SsasEntityFrameworkProvider;
using ModelExample;
using Xunit;

namespace SsasEntityFrameworkProvider.AcceptanceTests
{
    public class DbContextAcceptance
    {
        public DbContextAcceptance()
        {
            Database.SetInitializer<DbContext>(null);
            Database.SetInitializer<ModelExampleDbContext>(null);
            Database.DefaultConnectionFactory = CreateConnectionFactory();
            Database.SetInitializer<DataTypeTestContext>(null);
        }

        [Fact(Skip = "Run if you need to create ReportLineAllPrimitiveDataTypesTest table with data")]
        public void PersistReportLineAllPrimitiveDataTypesTest()
        {
            Database.SetInitializer(new DataTypeTestInitializer());
            using (var context = DataTypeTestContext.CreateForSql())
            {
                context.Database.Initialize(true);
            }
        }

        const string loadReportLineAllPrimitiveDataTypesTestMdx
            = @"
            select 
            (
                [Report Line].[Id].[Id]
                ,[Report Line].[Date].[Date]
                ,[Report Line].[W Char Item Name].[W Char Item Name]
                ,[Report Line].[Guid Code].[Guid Code]
            ) on rows,
            {
                Measures.[Big Int Sum]
                , Measures.[Currency Sum]
	            , Measures.[Decimal Sum]
                , Measures.[Double Sum]
                , Measures.[Int Sum]
                , Measures.[Single Sum]
                , Measures.[Small Int Sum]
                , Measures.[Unsigned Tiny Int Does Not Exist In Ssas Sum]
                , Measures.[Big Int Nullable Sum]
                , Measures.[Currency Nullable Sum]
	            , Measures.[Decimal Nullable Sum]
                , Measures.[Double Nullable Sum]
                , Measures.[Int Nullable Sum]
                , Measures.[Single Nullable Sum]
                , Measures.[Small Int Nullable Sum]
                , Measures.[Unsigned Tiny Int Nullable Does Not Exist In Ssas Sum]
                , Measures.[Bool Fact]
                , Measures.[Bool Nullable Fact]
                , Measures.[Tiny Int Sum]
                , Measures.[Tiny Int Nullable Sum]
            } on columns
            from [DataTypeTest]
            ";

        [Fact]
        public void LoadAllDataTypesFromOlap()
        {
            ReportLineAllPrimitiveDataTypesTest result;
            using (var context = DataTypeTestContext.CreateForMdx())
            {
                result = context.Database.SqlQuery<ReportLineAllPrimitiveDataTypesTest>(
                    loadReportLineAllPrimitiveDataTypesTestMdx).Single();
            }
            var etalon = new ReportLineAllPrimitiveDataTypesTest();
            etalon.SetTestValues();
            //unsigned types on SSAS side are supported, 
            //but on EF signed types must be used for all mapped properties
            Assert.Equal(etalon.BigIntNullableSum, result.BigIntNullableSum);
            Assert.Equal(etalon.BigIntSum, result.BigIntSum);
            Assert.Equal(etalon.BoolFact, result.BoolFact);
            Assert.Equal(etalon.BoolNullableFact, result.BoolNullableFact);
            //by default SSAS maps SQL Server 'money' to SSAS 'Double' loosing precision
            Assert.Equal(etalon.CurrencyNullableSum ?? 0, result.CurrencyNullableSum ?? 0, 0);
            //CurrencySum data type is set to Currency, so it preserves its precision
            Assert.Equal(etalon.CurrencySum, result.CurrencySum);
            Assert.Equal(etalon.Date, result.Date);
            //by default SSAS maps SQL Server 'decimal' to SSAS 'Double' loosing precision
            Assert.Equal(
                etalon.DecimalNullableSum / 100 ?? 0
                , result.DecimalNullableSum / 100 ?? 0, 0);
            Assert.Equal(etalon.DecimalSum / 100, result.DecimalSum / 100, 0);
            Assert.Equal(etalon.DoubleNullableSum, result.DoubleNullableSum);
            Assert.Equal(etalon.DoubleSum, result.DoubleSum);
            Assert.Equal(etalon.Id, result.Id);
            Assert.Equal(etalon.IntNullableSum, result.IntNullableSum);
            Assert.Equal(etalon.IntSum, result.IntSum);
            Assert.Equal(etalon.SingleNullableSum, result.SingleNullableSum);
            Assert.Equal(etalon.SingleSum, result.SingleSum);
            Assert.Equal(etalon.SmallIntNullableSum, result.SmallIntNullableSum);
            Assert.Equal(etalon.SmallIntSum, result.SmallIntSum);
            Assert.Equal(etalon.TinyIntNullableSum, result.TinyIntNullableSum);
            Assert.Equal(etalon.TinyIntSum, result.TinyIntSum);
            Assert.Equal(etalon.UnsignedTinyIntDoesNotExistInSsasSum
                , result.UnsignedTinyIntDoesNotExistInSsasSum);
            Assert.Equal(etalon.UnsignedTinyIntNullableDoesNotExistInSsasSum
                , result.UnsignedTinyIntNullableDoesNotExistInSsasSum);
            Assert.Equal(etalon.WCharItemName, result.WCharItemName);            
            Assert.Equal(etalon.GuidCode, result.GuidCode);
        }

        [Fact(Skip = "Do not go this way as I will have to use a mapping designer which probably does not support SSAS")]
        public void CanUseObjectContextForMdxQueryAsIs()
        {
#if SupportObjectContext
            const string connectionString 
                = "Provider=MSOLAP.4;Data Source=.;Integrated Security=SSPI;Initial Catalog=ModelExampleCube";
            var connection = new SsasConnection(connectionString);
            var workspace = new MetadataWorkspace
                (
                    new [] {"res://*/"},
                    new[] { Assembly.GetAssembly(typeof(NorthwindEntities)) }  //TODO: change to: Assembly.GetAssembly(typeof(NorthwindEntities))
                );

            var entityConnection = new EntityConnection(workspace, connection);
            var context = new NorthwindEntities(entityConnection);
            //var context = new ObjectContext(entityConnection);
            Assert.Null(context.ExecuteStoreQuery<NorthwindEFModel.Product>("select from ModelExampleCube"));

            //Do I need EntityFrameworkRererence? This is from System.Data.Entity:
            //var context = new ObjectContext("Provider=MSOLAP.4;Data Source=.;Integrated Security=SSPI;Initial Catalog=ModelExampleCube");
#endif
        }

        const string explicitConnectionString
            = "Provider=MSOLAP.4;Data Source=.;Integrated Security=SSPI;Initial Catalog=ModelExampleCube";

        /// <summary>
        /// Uses EF DbContext as is, special derived context class is not required
        /// </summary>
        [Fact]
        public void SimpleMdxStoreQuery()
        {
            //using context with explicit connection string:
            AssertSimpleMdxQueryReturnsNull(new DbContext(explicitConnectionString));
            //getting connection from config by name:
            AssertSimpleMdxQueryReturnsNull(new DbContext("ModelExampleDbContext"));
            //using preexisting connection instance:
            const bool contextOwnesConnection = true;
            AssertSimpleMdxQueryReturnsNull(
                new DbContext(
                    Database.DefaultConnectionFactory.CreateConnection(explicitConnectionString)
                    , contextOwnesConnection));
            //using derived DbContext
            AssertSimpleMdxQueryReturnsNull(new ModelExampleDbContext());

            //TODO: can I also make this work:
            #region Commented Out
            //this connection string also works:
            //    = @"metadata=..\..\..\ModelExampleDbContext;provider=AgileDesign.SsasEntityFrameworkProvider;provider connection string=&quot;Provider=MSOLAP.4;Data Source=.;Integrated Security=SSPI;Initial Catalog=ModelExampleCube&quot;";
            //var connection = new SsasConnection(connectionString);
            //var context = new DbContext(connection, true);
            #endregion
        }

        [Fact]
        public void SingleEntityMdxQueryPopulatesComplexType()
        {
            var context = new ModelExampleDbContext();
            //var product = context.Products.SqlQuery(singleProductMdx).Single();
            var product = context.Database.SqlQuery<Product>(singleProductMdx).Single();

            Assert.NotNull(product);
            Assert.Equal("Test Product", product.ProductName);
            Assert.Equal(42, product.Id);
            Assert.Equal("Test Code", product.Code);
            Assert.Equal("Test Unit", product.UnitOfMeasure);

            Assert.NotNull(product.AuditTrail);
            //Database.SqlQuery() / ObjectContext.ExucuteStoreQuery() do not support ComplexType
            Assert.Null(product.AuditTrail.ModifiedTime);

            #region Uncomment when I can use complex type
            //Assert.Equal("01/01/2010", product.AuditTrail.ModifiedTime);
            //ObjectContext.ExucuteStoreQuery() does not support complex types.
            //ObjectContext.Translate<Product>(reader) does not work with complex types neither
            //Try: How to: Execute a Query that Returns StructuralType Results: http://msdn.microsoft.com/en-us/library/cc716720.aspx
            #endregion
        }

        protected virtual IDbConnectionFactory CreateConnectionFactory()
        {
                return new DbConnectionFactoryAdapter(new SsasProviderFactory());
        }

        void AssertSimpleMdxQueryReturnsNull(DbContext context)
        {
            //TODO: implement MdxQuery() extension method on Database and IDbSet 
            //(maybe on ObjectContext and ObjectQuery too)
            var query = context.Database.SqlQuery<object>("select from ModelExampleCube");

            var result = query.First();

#if ! DEBUG //TODO: debug and figure out what is returned and that result is correct
            Assert.Null(result);
#else
            Assert.NotNull(result);
#endif
        }

        const string singleProductMdx
    = @"
                with 
                member [Product].[Product Name].[All].[Test Product]
                    as ""product""
                member [Product].[Id].[All].[42]
	                as ""id""
                member [Product].[Code].[All].[Test Code]
	                as ""code""
                member [Product].[Unit Of Measure].[All].[Test Unit]
	                as ""UnitOfMeasure""
                member [Product].[Modified Time].[All].[01/01/2010]
	                as ""01/01/2010""
                select 
                {
	                [Measures].[Total]
	                , [Measures].[Tax]
                } on columns
                ,
                (
	                [Product].[Id].[All].[42]
	                , [Product].[Modified Time].[All].[01/01/2010]
	                , [Product].[Code].[All].[Test Code]
	                , [Product].[Product Name].[All].[Test Product]
	                , [Product].[Unit Of Measure].[All].[Test Unit]
                ) on rows
                from ModelExampleCube";

    }
}
