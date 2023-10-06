using System.Data.Entity;
using ModelExample;

namespace SsasEntityFrameworkProvider.AcceptanceTests
{
    public class DataTypeTestContext 
        : DbContext
    {
        DataTypeTestContext(string nameOrConnectionString)
            : base(nameOrConnectionString)
        {
        }

        public static DataTypeTestContext CreateForSql()
        {
            return new DataTypeTestContext("Name=DataTypeTestDb");
        }

        public static DataTypeTestContext CreateForMdx()
        {
            return new DataTypeTestContext("Name=DataTypeTestOlap");
        }

        public DbSet<ReportLineAllPrimitiveDataTypesTest> ReportLineAllTypesTest { get; set; }

    }

    public class DataTypeTestInitializer 
        : DropCreateDatabaseAlways<DataTypeTestContext>
    {
        protected override void Seed(DataTypeTestContext context)
        {
            var reportLine = new ReportLineAllPrimitiveDataTypesTest();
            reportLine.SetTestValues();
            context.ReportLineAllTypesTest.Add(reportLine);
            base.Seed(context);
        }
    }
}