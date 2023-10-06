using System.Linq;
using NorthwindEFModel;
using Xunit;
using AgileDesign.SsasEntityFrameworkProvider;

namespace SsasEntityFrameworkProvider.AcceptanceTests
{
    public class MultiValueExplicitAggregationFunctions
    {
        NorthwindEFContext context = NorthwindEFContext.CreateForOlap();
        const string granularityDimensionLevel = "[Customers].[Customer ID].[Customer ID]";

        [Fact]
        public void ScalarValueAggregatesWithSubquery()
        {
            //TODO: match outer SELECT Cn DbPropertyExpression-s by Cn alias 
            //which should be passed into inner SELECT
            var result
                = context.OrderDetails.Select
                    (
                        od => new
                        {
                            //-this adds OrderID into ON ROWS, 
                            //EF likely does that by using Quantity or OrderID instead of C1:
#if fixed //TODO: fix Mdx.Count()
                            Count = Mdx.Count(od.OrderID), 
#endif
                            MaxDiscount = Mdx.Max(od.Discount, granularityDimensionLevel),
                            MinDiscount = Mdx.Min(od.Discount, granularityDimensionLevel),
                            AvgQuantity = Mdx.Average(od.Quantity, granularityDimensionLevel),
                            SumQuantity = Mdx.Sum(od.Quantity),
                            MaxQuantity = Mdx.Max(od.Quantity, granularityDimensionLevel),
                            MinQuantity = Mdx.Min(od.Quantity, granularityDimensionLevel)
                        }
                    ).Single(); 
            //If .ToArray().Single() is not used Single() causes a nested Limit1 
            //NewInstanceExpression or DbProjectExpression to be generated in my input command tree
            //and I have to match outer Cn by alias names to internal measure names
            Assert.Equal(0, result.MinDiscount);
            Assert.Equal(0.3417, result.MaxDiscount, 4);
            Assert.Equal(563, result.AvgQuantity);
            Assert.Equal(50119, result.SumQuantity);
            Assert.Equal(4958, result.MaxQuantity);
            Assert.Equal(11, result.MinQuantity);
        }

        [Fact]
        public void ScalarValueAggregatesWithoutSubquery()
        {
            var result
                = context.OrderDetails.Select
                    (
                        od => new
                        {
#if fixed //TODO: fix Count() which adds its argument as an output column
                            Count = Mdx.Count(od.OrderID),
#endif
                            MaxDiscount = Mdx.Max((float?)od.Discount, granularityDimensionLevel),
                            MinDiscount = Mdx.Min(od.Discount, granularityDimensionLevel),
                            AvgQuantity = Mdx.Average(od.Quantity, granularityDimensionLevel),
                            SumQuantity = Mdx.Sum(od.Quantity),
                            MaxQuantity = Mdx.Max(od.Quantity, granularityDimensionLevel),
                            MinQuantity = Mdx.Min(od.Quantity, granularityDimensionLevel)
                        }
                    ).ToArray().Single();

            //Assert.Equal(666, result.Count);
            Assert.Equal(0, result.MinDiscount);
            Assert.Equal(0.3417, (double)result.MaxDiscount, 4);
            Assert.Equal(563, result.AvgQuantity);
            Assert.Equal(50119, result.SumQuantity);
            Assert.Equal(4958, result.MaxQuantity);
            Assert.Equal(11, result.MinQuantity);
        }

        [Fact]
        public void GroupedAggregates()
        {
            var result
                = (
                      from orderDetail in context.OrderDetails
                      select new
                      {
                          orderDetail.ProductID,
                          Min = Mdx.Min(orderDetail.Discount, granularityDimensionLevel),
                          Max = Mdx.Max(orderDetail.Discount, granularityDimensionLevel),
                          Avg = Mdx.Average(orderDetail.Discount, granularityDimensionLevel),
                          Sum = Mdx.Sum(orderDetail.Discount),
                          ImplicitSum = orderDetail.Discount
                      }
                  ).ToArray();

            Assert.Equal(77, result.Length);
            
            var actual = result[74];
            Assert.Equal(75, actual.ProductID);
            Assert.Equal(0, actual.Min);
            Assert.Equal(0.2167, actual.Max, 4);
            Assert.Equal(0.0462, actual.Avg, 4);
            Assert.Equal(0.0533, actual.Sum, 4);
            Assert.Equal(0.0533, actual.ImplicitSum, 4);
        }
    }
}
