using System.Data;
using System.Data.Entity.Core;
using System.Linq;
using AgileDesign.SsasEntityFrameworkProvider;
using NorthwindEFModel;
using Xunit;

namespace SsasEntityFrameworkProvider.AcceptanceTests
{
    /// <summary>
    /// All aggregation functions return the same value of All member 
    /// if they are used with a single argument 
    /// and are not being cross-joined with some dimension leaf level.
    /// 
    /// To solve this problem I can use my canonical functions with 2 arguments 
    /// or Max(singleArgument).Over(p => product.ProductId)
    /// and pass a dimension level as a second argument.
    /// E.g.:
    /// WITH MEMBER [Measures].[A1]
    ///    AS Max([Products].[Product ID].[Product ID], [Measures].[Quantity])
    ///SELECT 
    ///{
    ///[Measures].[A1]
    ///}
    ///ON COLUMNS
    ///FROM [NorthwindEF]
    /// 
    /// Another solution more inline with LINQ semantics will be to cross-join 
    /// all available dimensions having All level in a first argument (set) of an aggregate function.
    /// E.g.: http://social.msdn.microsoft.com/Forums/en/sqlanalysisservices/thread/75c3601c-81de-40b2-b5d3-5f0993a043e4 and http://sqlblog.com/blogs/mosha/archive/2006/10/11/querying-dimensions-in-mdx.aspx :
    /// SELECT
    ///[DIMENSION_UNIQUE_NAME]
    ///FROM
    ///$SYSTEM.MDSCHEMA_DIMENSIONS
    ///WHERE
    ///[CUBE_NAME]='NorthwindEF'
    /// </summary>
    /// <remarks>
    /// This cannot be done as a real aggregation function because 
    /// EF throws "Aggregate Functions should take exactly one input parameter."
    /// </remarks>
    public class SingleValueExplicitAggregationFunctions
    {
        const string GranularityDimensionHeirarchyAndLevel = "[Products].[Product ID].[Product ID]";
        readonly NorthwindEFContext context = NorthwindEFContext.CreateForOlap();

        [Fact]
        public void Max()
        {
            var maxQuantity = context.OrderDetails
                .Select(o => Mdx.Max(o.Quantity, GranularityDimensionHeirarchyAndLevel)).Single();

            Assert.Equal(1504, maxQuantity);

            var maxUnitPrice = context.OrderDetails
                .Select(o => Mdx.Max(o.UnitPrice, GranularityDimensionHeirarchyAndLevel)).Single();

            Assert.Equal(245.93m, maxUnitPrice, 2);
        }

        [Fact]
        public void SumRegularLinqSyntax()
        {
            double sum = context.OrderDetails.Sum(o => o.Quantity);
            Assert.Equal(50119, sum);
        }

        [Fact]
        public void Average()
        {
            int average = context.OrderDetails
                .Select(o => Mdx.Average(o.Quantity, GranularityDimensionHeirarchyAndLevel)).Single();

            Assert.Equal(651, average);
        }

        [Fact]
        public void Min()
        {
            int min = context.OrderDetails
                .Select(o => Mdx.Min(o.Quantity, GranularityDimensionHeirarchyAndLevel)).Single();

            Assert.Equal(95, min);
        }

        [Fact]
        public void SumSdxSyntax()
        {
            int sumNonStandard = context.OrderDetails
                .Select(o => Mdx.Sum(o.Quantity)).Single();

            Assert.Equal(50119, sumNonStandard);
        }

#if fixed //TODO: Count() may cause EF to add Count() argument as a dimension into no dimension MDX
        [Fact]
        public void CountDimensionSdxSyntax()
        {
            int countNonStandard = context.OrderDetails
                .Select(o => Mdx.Count(o.ProductID)).Single();

            Assert.Equal(77, countNonStandard);
        }

        [Fact(Skip = "Implement support for Count() on measure groups by using an appropriate count measure")]
        public void CountMeasureSdxSyntax()
        {
            int countNonStandard = context.OrderDetails
                .Select(o => Mdx.Count(o.Quantity)).Single();

            Assert.Equal(77, countNonStandard);
        }

        [Fact]
        public void LongCountMeasureSdxSyntax()
        {
            long countNonStandard = context.OrderDetails
                .Select(o => Mdx.LongCount(o.OrderID)).Single();

            Assert.Equal(831, countNonStandard);
        }
#endif

        [Fact(Skip="It is quite complex to support because it is not that simple to use count measures")]
        public void CountRegularLinqSyntaxIsNotSupportedYet()
        {
            //I would need either extract and use entity key dimension level or AppropriateMeasureCount measure
            //but it is hard to find table / dimension name or redirect to a count measure
            Assert.Throws(typeof(EntityCommandCompilationException), () => context.OrderDetails.Count());
        }

    }
}