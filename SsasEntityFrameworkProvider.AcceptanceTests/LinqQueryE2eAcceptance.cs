using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Core;
using System.Linq;
using AgileDesign.SsasEntityFrameworkProvider;
using NorthwindEFModel;
using Xunit;
using Xunit.Sdk;


//using System.Data.Entity;
//using System.Data.Entity.Infrastructure;

    //public class MyConfiguration : DbConfiguration
    //{
    //    public MyConfiguration()
    //    {
    //        //SetExecutionStrategy("System.Data.SqlClient", () => new SqlAzureExecutionStrategy());
    //        //SetDefaultConnectionFactory(new LocalDbConnectionFactory("v11.0"));

    //        SetProviderFactory("AgileDesign.SsasEntityFrameworkProvider", new SsasProviderFactory());
    //        SetProviderServices("AgileDesign.SsasEntityFrameworkProvider", new SsasProvider());
    //    }
    //}

namespace SsasEntityFrameworkProvider.AcceptanceTests
{
    public class LinqQueryE2EAcceptance
    {

#if false //DEBUG //Another way to test invalid license is by disabling it in LicenseingService.DisabledLicense
        public LinqQueryE2EAcceptance()
        {
            AgileDesign.SsasEntityFrameworkProvider.Internal.MdxGeneration
                .MdxGenerator.license.LicenseCode = "NotWorkingCode";
        }
#endif

        [Fact]
        public void ParanthysisInWhere()
        {
            var result = context.Orders
                .Where
                (
                    o =>
                        (o.OrderID == 10248 || o.OrderID == 10249 || o.OrderID == 10250 || o.OrderID == 10251)
                        && (o.OrderID == 10250 || o.OrderID == 10251 || o.OrderID == 10252)
                )
                .Select
                (
                    o => new
                    {
                        o.OrderID,
                        o.OrderDate,
                        o.RequiredDate,
                    }
                )
                .ToArray();

            Assert.Equal(2, result.Length);
        }
    

        [Fact]
        public void FilterByDateRangeBasic()
        {
            var pMinDate = new DateTime(1998, 5, 5);
            var result = context.Orders
                .Where
                (
                    o =>
                        o.OrderDate >= pMinDate
                    && o.OrderDate < DateTime.MaxValue
                )
                .Select
                (
                    o => new
                    {
                        o.OrderID,
                        o.OrderDate,
                        o.RequiredDate,
                    }
                )
                .ToArray();

            Assert.Equal(8, result.Length);
        }

        [Fact(Skip = "Not ready")]
        public void FilterByDateRangeComplex()
        {
            //var result
            //    = from order in context.OrderDetails
            //      //TODO: where context.Orders.Any(h => h.RequiredDate >= DateTime.MinValue
            //      where context.Orders.Any(h => h.RequiredDate >= new DateTime(1996, 01, 01)
            //        && h.RequiredDate <= new DateTime(2010, 01, 01))
            //      select new { order.OrderID, order.Quantity };

            var result
                = from order in context.OrderDetails
                  from orderHeader in context.Orders
                  where orderHeader.OrderDate >= DateTime.MinValue
                  //&& orderHeader.RequiredDate <= new DateTime(2010, 01, 01)
                  select new { order.OrderID, order.Quantity };

            Assert.NotNull(result);
            Assert.NotEqual(0, result.Count());
        }

        [Fact(Skip = "Stack overflow exception")]
        public void SubqueryWithAny()
        {
            var result
                = from customer in context.Customers
                  from order in context.OrderDetails
                  where context.Orders.Any(h => h.RequiredDate > DateTime.MinValue 
                      && h.CustomerID == customer.CustomerID)
                  select new { order.OrderID, order.Quantity };

            Assert.NotNull(result);
            Assert.True(result.Any());
        }
        
        [Fact]
        public void WebExample()
        {
            var result
                = (
                    from customer in context.Customers
                    from order in context.OrderDetails //implicit join based on cube metadata
                    where ( customer.Country == "Italy"
                            || customer.Country.Contains( "US" ) )
                        && order.Product.ProductName.ToUpper().StartsWith( "P" )
                        && order.Discount != 0
                        && order.Quantity >= 100
                    orderby order.Discount descending
                    select new
                    {
                        customer.CustomerID,
                        customer.CompanyName,
                        order.Quantity, //Quantity is aggregated using implicit Sum() aggregation function
                        order.Discount, //Discount is aggregated using implicit Avg() aggregation function
                        //Sometimes you will specify explicit aggregation functions in MDX style
                        MaxDiscount = Mdx.Max(order.Discount, "[Customers].[Customer ID].[Customer ID]" /*granularity dimension(s)*/)
                    }
                )
                .Skip(2) //pagination
                .Take(3)
                .ToArray();

            Assert.NotNull(result);
            Assert.Equal(3, result.Length);
            float prevDiscount = int.MaxValue;
            foreach(var row in result)
            {
                Assert.True(row.Discount <= prevDiscount);
                prevDiscount = row.Discount;
            }
        }

        [Fact]
        public void FilterByDateTime()
        {
            var result = context.Orders
                .Where
                (
                    o => o.RequiredDate < DateTime.Now
                        && o.OrderDate < new DateTime(1997, 1, 1)
                )
                .Select
                (
                    o => new
                    {
                        o.OrderID,
                        o.RequiredDate,
                    }
                )
                .Take(3)
                .ToArray();

            Assert.Equal(3, result.Length);
        }

        [Fact]
        public void UsageExamplePaginationAndCalculatedMember()
        {
            var result
                = (
                      from orderHeader in context.Orders
                      //implicit join
                      from order in context.OrderDetails
                      where orderHeader.ShipCity.ToUpper().StartsWith("A")
                      orderby orderHeader.OrderDate descending 
                      select new
                      {
                          orderHeader.OrderID,
                          orderHeader.CustomerID,
                          //You can also use MDX functions and member properties here 
                          //(with some restrictions for local offline cubes)
                          ServerSideCalculation = Mdx.CalculatedMemberAsInt
                          (
                              "[Measures].[ServerSideCalculation]",
                              "2 * [Measures].[Quantity]"
                          ),
                          orderHeader.OrderDate,
                          order.Quantity,
                      }
                  )
                    .Skip(30) //Pagination
                    .Take(20)
                    .ToArray();

            Assert.NotNull(result);
            Assert.Equal(4, result.Length);
            Assert.Equal(254, result.Sum(o => o.Quantity));
        }

        /// <summary>
        /// Bug found when preparing UsageExample
        /// </summary>
        [Fact]
        public void CalculatedMemberPosition()
        {
            //ON ROWS column reordering conditions are enumerated (EF does column reordering):
            var result2
                = (
                      from orderHeader in context.Orders
                      from order in context.OrderDetails
                      //implicit join
                      //orderby orderHeader.OrderDate descending 
                      select new
                      {
                          //uncommenting this line removes column reordering condition 
                          //orderHeader.OrderID,
                          //1.
                          order.OrderID,
                          orderHeader.CustomerID,
                          //You can also use MDX functions and member properties here 
                          //(with some restrictions for local offline cubes)
                          //2.
                          ServerSideCalculation = Mdx.CalculatedMemberAsInt
                          (
                              "[Measures].[ServerSideCalculation]",
                              "2 * [Measures].[Quantity]"
                          ),
                          order.Quantity,
                      }
                  )
                    .Take(20)
                    .ToArray();

            Assert.NotNull(result2);
        }

        [Fact]
        public void Distinct()
        {
            var actual = context.Customers
                .Select(c => c.City)
                .Distinct()
                .ToArray();

            Assert.Equal(70, actual.Length);
        }

        [Fact]
        public void Except()
        {
            var actual = context.Customers
                .Select(c => c.CustomerID)
                .Except(
                    context.Customers
                    .Select(c => c.CustomerID)
                    .Take(10))
                .Take(3)
                .ToArray();

            var expected = context.Customers
                .OrderBy(c => c.CustomerID)
                .Select(c => c.CustomerID)
                .Skip(10)
                .Take(3)
                .ToArray();

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Intersect()
        {
            var actual = context.Customers
                .Select(c => c.CustomerID)
                .Take(10)
                .Intersect(
                    context.Customers
                    .OrderBy(c => c.CustomerID)
                    .Select(c => c.CustomerID)
                    .Skip(5)
                    .Take(10))
                .ToArray();

            var expected = context.Customers
                .OrderBy(c => c.CustomerID)
                .Select(c => c.CustomerID)
                .Skip(5)
                .Take(5)
                .ToArray();

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Concat()
        {
            var actual = context.Customers
                .Select(c => c.CustomerID)
                .Concat(
                    context.Customers
                    .OrderBy(c => c.CustomerID)
                    .Select(c => c.CustomerID)
                    .Skip(4)
                    .Take(3))
                .ToArray();

            var expected1stPart = context.Customers
                .Select(c => c.CustomerID)
                .ToArray();

            var expected2ndPart = context.Customers
                .OrderBy(c => c.CustomerID)
                .Select(c => c.CustomerID)
                .Skip(4)
                .Take(3)
                .ToArray();

            var expected = expected1stPart.Concat(expected2ndPart).ToArray();
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Union()
        {
            var actual = context.Customers
                .Select(c => c.CustomerID)
                .Union
                (
                    context.Customers
                    .OrderBy(c => c.CustomerID)
                    .Select(c => c.CustomerID)
                    .Skip(4)
                    .Take(3)
                )
                .ToArray();

            var expected = context.Customers
                .Select(c => c.CustomerID)
                .ToArray(); //duplicates should be removed

            Assert.Equal(92, actual.Length);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public virtual void First()
        {
            var result = context.Customers.Select(c => c.CustomerID).First();

            Assert.NotNull(result);
            Assert.Equal("ALFKI", result);

            var result2 = context.Customers.Select(c => c.CustomerID).FirstOrDefault();

            Assert.NotNull(result2);
            Assert.Equal("ALFKI", result2);
        }

        [Fact]
        public virtual void ToLowerStartsWithInFilter()
        {
            var result =
                (
                    from customer in context.Customers
                    select new
                    {
                        customer.CustomerID
                    }
                )
                .Where(c => c.CustomerID.ToLower().StartsWith("b"))
                .ToArray();

            Assert.NotNull(result);
            Assert.Equal(7, result.Count());
            Assert.True(result.All(r => r.CustomerID.StartsWith("B")));
        }

        [Fact]
        public virtual void ToLowerInProjection()
        {
            var result =
                (
                    from customer in context.Customers
                    select customer.CustomerID.ToLower()
                )
                .Take(5)
                .ToArray();

            Assert.NotNull(result);
            Assert.Equal(5, result.Count());
            Assert.True(result.All(r => char.IsLower(r[0])));
        }

        [Fact]
        public virtual void ToUpperContainsInFilter()
        {
            var result =
                (
                    from customer in context.Customers
                    select new
                    {
                        customer.ContactName
                    }
                )
                .Where(c => c.ContactName.ToUpper().Contains("U"))
                .ToArray();

            Assert.NotNull(result);
            Assert.Equal(29, result.Count());
            Assert.True(result.All(r => r.ContactName.ToUpper().Contains("U")));
        }

        [Fact]
        public virtual void IndexOfInFilter()
        {
            var result =
                (
                    from customer in context.Customers
                    select new
                    {
                        customer.CustomerID,
                    }
                )
                .Where(c => c.CustomerID.IndexOf("B") == 4)
                .ToArray();

            Assert.NotNull(result);
            Assert.Equal(3, result.Count());
            Assert.True(result.All(r => r.CustomerID.Substring(4, 1) == "B"));
        }

        [Fact]
        public virtual void ContainsInFilter()
        {
            var result =
                (
                    from customer in context.Customers
                    select new
                    {
                        customer.CustomerID,
                    }
                )
                .Where(c => c.CustomerID.Contains("B"))
                .ToArray();

            Assert.NotNull(result);
            Assert.Equal(11, result.Count());
            Assert.True(result.All(r => r.CustomerID.Contains("B")));
        }

        [Fact]
        public virtual void StartsWithInFilter()
        {
            var result =
                (
                    from customer in context.Customers
                    select new
                    {
                        customer.CustomerID,
                    }
                )
                .Where(c => c.CustomerID.StartsWith("B"))
                .ToArray();

            Assert.NotNull(result);
            Assert.Equal(7, result.Count());
            Assert.True(result.All(r => r.CustomerID.StartsWith("B")));
        }

        [Fact]
        public virtual void CalculatedMemberInWhere()
        {
            var result =
                (
                    from customer in context.Customers
                    select new
                    {
                        customer.CustomerID,
                        customer.ContactName,
                        customer.ContactTitle
                    }
                )
                .Where
                (
                    c => Mdx.CalculatedMemberAsInt
                                (
                                    "[Measures].[CustomerIDStartsWithB]",
                                    "Instr([Customers].[Customer ID].CurrentMember.Name, 'B')"
                                ) > 0
                ).ToArray();

            Assert.NotNull(result);
        }

        readonly NorthwindEFContext context = NorthwindEFContext.CreateForOlap();

        const string firstCompanyName = "Alfreds Futterkiste";
        const string unknown = "Unknown";


#if ReadyToExtractSqlNameFromPropertyName
        public static class Mdx
        {
            public static string HierarchyName(System.Linq.Expressions.Expression<Func<object>> property)
            {
                //( (IObjectContextAdapter)NorthwindEFContext
                //    .CreateForOlap() ).ObjectContext.MetadataWorkspace.GetItem
                //    <EntityType>("", DataSpace.OSpace).Members["Name"].Name;

                return new AddSpacesToCamelCasingWordsConvention()
                    .GetHierarchyAndColumnName(
                        NameOf.Member(property));
            }
        }
#endif

        [Fact(Skip = "See summery for ExplicitAggregationWithNoGroupBy()")]
        public virtual void ExplicitAggregationIncludingDimension()
        {
            var result
                = (
                      from order in context.OrderDetails
                      group order by order.ProductID into productGroup
                      select new
                      {
                          ProductID = productGroup.Key,
                          QuontityMax = productGroup.Max(g => g.Quantity)
                          //Max = context.OrderDetails.Max(o => o.Quantity)
                      }
                  ).ToArray();

            Assert.Equal(666, result.Count());
            Assert.Equal(666, result.First().QuontityMax); //TODO: can I exclude all All levels somehow?
        }

        /// <summary>
        /// Target MDX:
        ///WITH MEMBER [Measures].[CustomerID Caption]
        ///    AS [Customers].[Customer ID].Member_Caption
        ///SELECT 
        ///NON EMPTY
        ///HEAD
        ///(
        ///(
        ///[Customers].[Customer ID].[Customer ID]
        ///)
        ///,
        ///3
        ///)
        ///ON ROWS,
        ///{
        ///[Measures].[Quantity], 
        ///[Measures].[CustomerID Caption]
        ///}
        ///ON COLUMNS
        ///FROM [NorthwindEF]
        /// </summary>
        [Fact]
        public virtual void CalculatedMember()
        {
            //TODO: in future implement type safe alternative for MDX expressions, e.g.: 
            //string expression = Mdx.HierarchyName(() => (new Customer()).CustomerID) + MdxMember.Caption;

            string captionMemberName = "[Measures].[CustomerID Caption]";
            string captionExpression = "[Customers].[Customer ID].Member_Caption";
            string keyMemberName = "[Measures].[CustomerID Key]";
            string keyNameExpression = "[Customers].[Customer ID].CurrentMember.Member_Key";
            var result
                = (
                      from customer in context.Customers
                      from order in context.OrderDetails
                      select new
                      {
                          Caption = Mdx.CalculatedMemberAsString(
                                captionMemberName, captionExpression),

                          UniqueName = Mdx.CalculatedMemberAsString(
                                "[Measures].[CustomerID UniqueName]"
                                , "[Customers].[Customer ID].CurrentMember.UniqueName"),

                          Calculation = Mdx.CalculatedMemberAsInt(
                                "[Measures].[CustomerID Calculation]"
                                , "3 * 4"),

                          Key = Mdx.CalculatedMemberAsString(
                          keyMemberName, keyNameExpression),

                          //CustomerID property is needed to return all individual customer captions 
                          //rather than [All] level only
                          customer.CustomerID, 
                          order.Quantity
                      }
                  ).Take(3).ToArray();
    
            Assert.Equal(3, result.Length);
            Assert.Equal("ALFKI", result[0].Caption);
            Assert.Equal("[Customers].[Customer ID].&[ALFKI]", result[0].UniqueName);
            Assert.Equal("ALFKI", result[0].Key);
            Assert.Equal(12, result[0].Calculation);
        }

        /// <summary>
        /// Thanks to Marco Russo for finding this bug.
        /// The bug happened in LINQ queries with a filter and multiple measure groups.
        /// (Sorting on multiple measure groups did work all right).
        /// </summary>
        [Fact]
        public virtual void MutlipleMeasureGroupsWithFilterAndSortingMarcoCase()
        {
            var result = ( from c in context.Customers
                           from o in context.Orders
                           from d in context.OrderDetails
                           where d.Discount > 0 && o.ShipCountry == "Italy"
                           orderby d.Quantity descending
                           select new
                           {
                               c.City,
                               o.ShipCountry,
                               d.Discount,
                               d.Quantity
                           } ).Take(8).ToArray();

            Assert.NotNull(result);
            Assert.True(result.Length > 0);
        }

        /// <summary>
        /// Filter by a variable or parameter <br/>
        /// e.g.: <br/>
        /// var country = "Italy"; <br/>
        /// where (customer.Country == country) <br/>
        /// <br/>
        /// Thanks to Scott Weinstein for finding this bug in Prod.
        /// </summary>
        [Fact]
        public virtual void FilterByNonLiteralValueScottsCase()
        {
            string countrySingleQuoteRequired = "Italy";
            int orderIdNoQuoteRequired = 10404;
            var result
                = (
                      from orderHeader in context.Orders
                      from order in context.OrderDetails
                      where orderHeader.ShipCountry == countrySingleQuoteRequired
                        && orderHeader.OrderID == orderIdNoQuoteRequired
                      select new
                      {
                          orderHeader.ShipCountry,
                          order.Quantity
                      }
                  ).ToArray();

            Assert.NotNull(result);
            Assert.True(result.Length == 1);
            Assert.Equal(100, result[0].Quantity);
        }


        /// <summary>
        /// It failed initially because there were no dimensions in ON ROWS 
        /// and FILTER( , conditions) did not work. <br/>
        /// It will still fail if .Member() is replaced with == comparison.<br/>
        /// <br/>
        /// The issue was solved by using separate WhereAxis when comparison is .Member(), 
        /// there are no OR comparisons and the filter dimension is not present on other axes  
        /// </summary>
        [Fact]
        public virtual void FilterWhenMeasuresOnlyInOutput()
        {
            var result
                = (
                      from orderHeader in context.Orders
                      from order in context.OrderDetails
                      where orderHeader.ShipCountry.Member("Italy")
                      select new
                      {
                          order.Quantity
                      }
                  ).ToArray();

            Assert.NotNull(result);
            Assert.True(result.Length == 1);
            Assert.Equal(800, result[0].Quantity);
        }

        [Fact]
        public virtual void ParametrizedWhereMemberFilter()
        {
            string memberName = "Italy";
            var result
                = (
                      from orderHeader in context.Orders
                      from order in context.OrderDetails
                      where orderHeader.ShipCountry.Member(memberName)
                      select new
                      {
                          order.Quantity
                      }
                  ).ToArray();

            Assert.NotNull(result);
            Assert.True(result.Length == 1);
            Assert.Equal(800, result[0].Quantity);
        }

#if RangeIsImplmented
        [Fact(Skip = "Not Implmented Yet")]
        public virtual void WhereRangeFilter()
        {
            string fromMemberName = "01/01/1990";
            var result
                = (
                      from orderHeader in context.Orders
                      from order in context.OrderDetails
                      where orderHeader.OrderDate.Range(fromMemberName, "01/01/2013")
                      select new
                      {
                          order.Quantity
                      }
                  ).ToArray();

            Assert.NotNull(result);
            Assert.True(result.Length == 1);
            Assert.Equal(800, result[0].Quantity);
        }
#endif

        [Fact]
        public virtual void TwoMembersOnWhereAxis()
        {
            var result
                = (
                      from orderHeader in context.Orders
                      from order in context.OrderDetails
                      where orderHeader.ShipCountry.Member("Italy")
                        || orderHeader.ShipCountry.Member("USA")
                      select new
                      {
                          order.Quantity
                      }
                  ).ToArray();

            Assert.NotNull(result);
            Assert.True(result.Length == 1);
            Assert.Equal(800 + 9223, result[0].Quantity);
        }

        [Fact]
        public virtual void MemberOnWhereAxisAndRegularComparisonTogether()
        {
            var result
                = (
                      from orderHeader in context.Orders
                      from order in context.OrderDetails
                      where orderHeader.ShipCountry.Member("Italy")
                        && orderHeader.OrderID >= 10300
                      select new
                      {
                          orderHeader.OrderDate,
                          order.Quantity
                      }
                  ).ToArray();

            Assert.NotNull(result);
            Assert.Equal(25, result.Length);
            Assert.Equal(50, result[0].Quantity);
            Assert.Equal(769, result.Sum(i => i.Quantity));
        }

        /// <summary>
        /// It must be Member() OR Member()
        /// </summary>
        [Fact]
        public virtual void MemberAndMemberOnWhereAxisThrow()
        {
            Assert.Throws
                (
                typeof(EntityCommandCompilationException)
                , () =>
                    (
                        from orderHeader in context.Orders
                        from order in context.OrderDetails
                        where orderHeader.ShipCountry.Member("Italy")
                        && orderHeader.ShipCountry.Member("USA")
                        select new
                        {
                            orderHeader.OrderDate,
                            order.Quantity
                        }
                    ).ToArray()
                );

        }
        /// <summary>
        /// It must be Member() AND NotMemberEdmFunction
        /// </summary>
        [Fact]
        public virtual void MemberOrNotMemberOnWhereAxisThrow()
        {
            Assert.Throws
                (
                typeof(EntityCommandCompilationException)
                , () =>
                    (
                        from orderHeader in context.Orders
                        from order in context.OrderDetails
                        where orderHeader.ShipCountry.Member("Italy")
                            || orderHeader.OrderID >= 10300
                        select new
                        {
                            orderHeader.OrderDate,
                            order.Quantity
                        }
                    ).ToArray()
                );

        }

        [Fact]
        public void FilterByMeasureMemberThrows()
        {
            Assert.Throws
            (
                typeof(EntityCommandCompilationException)
                , () => 
                    (
                      from order in context.OrderDetails
                      where order.Quantity.Member("50")
                      select new
                      {
                          order.Quantity
                      }
                    ).ToArray()
            );
        }

        /// <summary>
        /// Production bug found by S.G. <br/>
        /// Error message fixed: 
        /// Too many arguments were passed to the HEAD MDX function. No more than 2 arguments are allowed. <br/>
        /// This bug was caused by HEAD() not enclosing its argument into parenthesis 
        /// when the argument is not enclosed already
        /// </summary>
        [Fact]
        public virtual void TooManyAgrumentsInHeadMdxFunctionBug()
        {
            var result
            = (
                  from orderHeader in context.Orders
                  from order in context.OrderDetails
                  select new
                  {
                      orderHeader.CustomerID,
                      orderHeader.OrderID,
                      orderHeader.OrderDate,
                      order.Quantity
                  }
              ).Take(8).ToArray();

            Assert.NotNull(result);
            Assert.Equal(8, result.Length);
        }

        /// <summary>
        /// Production bug found by S.G. <br/>
        /// It looks like HEAD() takes top n members from each dimension independently 
        /// regardless if they exist in combination with each other or not. 
        /// So top n from DimA and top n from DimB do not exist with each other 
        /// NON EMPTY will filter them out and less than n members will ber returned.
        /// </summary>
        [Fact(Skip = "This bug has a solution to use EXISTS with 3 parameters, which is not trivial"
            + " and is not supported yet")]
        public virtual void NoResultsIfTopWithMultipleDimensions()
        {
            var result
            = (
                  from customer in context.Customers
                  from order in context.OrderDetails
                  from orderHeader in context.Orders
                  select new
                  {
                      customer.CustomerID,
                      customer.CompanyName,
                      order.Quantity,
                      orderHeader.OrderID
                  }
              ).Take(8).ToArray();

            Assert.NotNull(result);
            Assert.Equal(8, result.Length);
        }

        /// <summary>
        /// Failed in Prod with this error message:
        /// System.NotSupportedException: Do not use explicit join clauses, results are joined automatically 
        /// according to relationships defined in a cube
        /// </summary>
        [Fact]
        public virtual void AssociationPropertyTraversal()
        {
            var result
            = (
                  from order in context.OrderDetails
                  from orderHeader in context.Orders
                  where orderHeader.Customer.Country == "Italy"
                  && orderHeader.ShipCity == "Bergamo"
                  select new
                  {
                      order.Quantity,
                      orderHeader.OrderID,
                      orderHeader.ShipCity
                  }
              )
              .ToArray();

            Assert.NotNull(result);
            Assert.Equal(10, result.Length);
        }

        /// <summary>
        /// This query initially resulted an error "Distinct() is not supported", 
        /// rather than saying GroupBy() is not supported.
        /// Reported by Scott.
        /// Now it fails because EF created CASE WHEN THEN internally, 
        /// and DbCaseExpression is not supported yet.
        /// </summary>
        [Fact(Skip="TODO: Implement this case, priority # 2")]
        public virtual void GroupByAsDistinctErrorMessageScottsCase()
        {
            try
            {
                RunGroupByAsDistinctQuery();
            }
            catch (EntityCommandCompilationException ex)
            {
                if ( ! ex.GetBaseException().Message.StartsWith("Distinct() and GroupBy are not supported yet."))
                    throw;

                return;
            }
            throw new AssertException("Expected NotSupportedException was not thrown");
        }
        void RunGroupByAsDistinctQuery()
        {
            (
                from customer in context.Customers
                from order in context.OrderDetails
                select new
                {
                    customer.CustomerID,
                    customer.CompanyName,
                    order.Quantity,
                    order.Discount,
                }
            ).GroupBy(a => a.CompanyName)
            .ToArray();
        }


        [Fact]
        public virtual void ExplicitJoinDoesNotFailButLogsWarning()
        {
            Assert.Throws(typeof(EntityCommandCompilationException), () => RunExplicitJoinQeury());
        }

        void RunExplicitJoinQeury()
        { //TODO: make properties PureSdx and LinqSdxMix styles
            (
                from order in context.OrderDetails
                join orderHeader in context.Orders
                    on order.OrderID equals orderHeader.OrderID
                where orderHeader.Customer.Country == "Italy"
                select new
                {
                    order.Quantity,
                    orderHeader.OrderID
                }
            ).Take(8).ToArray();
        }

        [Fact]
        public virtual void SelectScalar()
        {
            var company = context.Customers.Select
                ( //Note: if I use anonymous type it confuses column order in result
                    c => c.CompanyName
                ).First();

            Assert.NotNull(company);
            Assert.Equal(firstCompanyName, company);
        }

        [Fact]
        public virtual void SelectSingleColumnList()
        {
            var comapnies = context.Customers.Select
                ( //Note: if I use anonymous type it confuses column order in result
                    c => c.CompanyName
                )
                .ToArray();

            Assert.Equal(92, comapnies.Length);
            Assert.Equal(firstCompanyName, comapnies[0]);
            Assert.Equal(unknown, comapnies.Last());
        }

        [Fact]
        public virtual void SelectMultipleColumns()
        {
            var customerColumnsQuery
                = from customer in context.Customers
                  select new
                  {
                      customer.CustomerID,
                      customer.CompanyName,
                      customer.Country
                  };
            var result = customerColumnsQuery.ToArray();

            Assert.Equal(92, result.Length);
            
            Assert.Equal(firstCompanyName, result.First().CompanyName);
            Assert.Equal(unknown, result.Last().CompanyName);
            
            Assert.Equal("ALFKI", result.First().CustomerID);
            Assert.Equal(unknown, result.Last().CustomerID);

            Assert.Equal("Germany", result.First().Country);
            Assert.Equal(unknown, result.Last().Country);
        }

        [Fact]
        public virtual void Select1MeasureColumn()
        {
            var quantity
                = from orderDetails in context.OrderDetails
                  select orderDetails.Quantity;

            Assert.Equal(50119, quantity.Single());
        }

        [Fact]
        public virtual void SelectMultipleMeasureColumns()
        {
            var measures
                = (
                    from orderDetails in context.OrderDetails
                    select new
                    {
                        orderDetails.Quantity,
                        orderDetails.Discount,
                        orderDetails.UnitPrice
                    }
                  ).Single();

            Assert.Equal(50119, measures.Quantity);
            Assert.Equal(0.1445, measures.Discount, 4);
            Assert.Equal(67.89m, measures.UnitPrice, 2);
        }

        [Fact]
        public virtual void SelectMultipleMeasureGroups()
        {
            var measures
                = (
                    from orderDetails in context.OrderDetails
                    from product in context.Products
                    select new
                    {
                        orderDetails.Quantity,
                        product.UnitsInStock,
                        product.UnitsOnOrder
                    }
                  ).Single();

            Assert.Equal(50119, measures.Quantity);
            Assert.Equal<short?>(3119, measures.UnitsInStock);
            Assert.Equal<short?>(780, measures.UnitsOnOrder);
        }

        [Fact]
        public virtual void SelectMeasureColumnsWithAKey()
        {
            var result
                = (
                    from orderDetails in context.OrderDetails
                    select new
                    {
                        orderDetails.ProductID,
                        orderDetails.Quantity,
                        orderDetails.Discount,
                        orderDetails.UnitPrice
                    }
                  ).ToArray();

            Assert.Equal(77, result.Length);

            Assert.Equal(50119, result.Sum(od => od.Quantity));
            //Assert.Equal(0.1445, result.Average(od => od.Discount), 4);
            //Assert.Equal(67.89m, result.Average(od => od.UnitPrice), 2);

            Assert.Equal(1, result.First().ProductID);
            Assert.Equal(77, result.Last().ProductID);
            Assert.Equal(788, result.First().Quantity);
            Assert.Equal(0.05, result.Last().Discount, 2);

            Assert.Equal(11, result.ElementAt(10).ProductID);
            Assert.Equal(696, result.ElementAt(10).Quantity);
            Assert.Equal(19.56m, result.ElementAt(10).UnitPrice, 2);
        }
        
        [Fact]
        public virtual void SelectMeasureColumnsWith2Keys()
        { 
            var result
                = (
                    from orderDetails in context.OrderDetails
                    select new
                    {
                        orderDetails.ProductID,
                        orderDetails.OrderID,
                        orderDetails.Quantity,
                        orderDetails.Discount,
                        orderDetails.UnitPrice
                    }
                  ).ToArray();

            Assert.Equal(2082, result.Length);

            Assert.Equal(50119, result.Sum(od => od.Quantity));

            Assert.Equal(11, result.First().ProductID);
            Assert.Equal(39, result.Last().ProductID);
            Assert.Equal(12, result.First().Quantity);
            Assert.Equal(0.00, result.Last().Discount, 2);

            Assert.Equal(65, result.ElementAt(10).ProductID);
            Assert.Equal(20, result.ElementAt(10).Quantity);
            Assert.Equal(16.80m, result.ElementAt(10).UnitPrice, 2);
        }

        [Fact]
        public virtual void SelectMeasuresAndSingleDimension()
        {
            var result
                = (
                    from orderDetails in context.OrderDetails
                    from product in context.Products
                    select new
                    {
                        product.ProductID,
                        //Causing string to int cast exception without LinqToMdxColumnOrder feature
                        product.ProductName, 
                        orderDetails.Quantity,
                        orderDetails.Discount,
                        orderDetails.UnitPrice
                    }
                  ).ToArray();

            Assert.Equal(77, result.Length);

            Assert.Equal(50119, result.Sum(od => od.Quantity));

            Assert.Equal(1, result.First().ProductID);
            Assert.Equal(77, result.Last().ProductID);
            Assert.Equal("Original Frankfurter grüne Soße", result.Last().ProductName);

            Assert.Equal(11, result.Skip(10).First().ProductID);
            Assert.Equal(696, result.Skip(10).First().Quantity);
            Assert.Equal(19.56m, result.Skip(10).First().UnitPrice, 2);
        }

        [Fact]
        public virtual void SelectMeasuresAndMultipleDimensions()
        {
            var result
                = ( //Order of "from" clauses does not effect result order of columns and rows
                    from orderDetails in context.OrderDetails
                    from customer in context.Customers
                    from product in context.Products
                    from employee in context.Employees
                    select new
                    { //Order in "select" effects shape and order of columns and rows, 
                        //but measures will be always put in a separate ON COLUMNS axis 
                        //regardless of their position
                        orderDetails.Quantity,
                        orderDetails.Discount,
                        orderDetails.UnitPrice,
                        customer.CustomerID,
                        customer.CompanyName,
                        customer.ContactName,
                        customer.Country,
                        product.ProductName,
                        employee.EmployeeID,
                        employee.FirstName,
                        employee.LastName,
                    }
                  ).ToArray();

            Assert.Equal(2020, result.Length);
            Assert.Equal(50119, result.Sum(od => od.Quantity));
            Assert.Contains("Original Frankfurter grüne Soße", result.Select(r => r.ProductName));
            Assert.Contains("BERGS", result.Select(r => r.CustomerID));
        }

        [Fact]
        public virtual void SimpleWhere()
        {
            var result
                = (
                      from customer in context.Customers
                      where customer.Country == "Italy"
                      select new
                      {
                          customer.CustomerID,
                          customer.CompanyName,
                      }
                  ).ToArray();

            Assert.Equal(3, result.Length);

            Assert.Equal("Franchi S.p.A.", result.First().CompanyName);
            Assert.Equal("Reggiani Caseifici", result.Last().CompanyName);

            Assert.Equal("FRANS", result.First().CustomerID);
            Assert.Equal("REGGC", result.Last().CustomerID);

            //Assert.Equal("Italy", result.First().Country);
            //Assert.Equal(Italy, result.Last().Country);
        }

        [Fact]
        public virtual void WhereWithMultipleExpressions()
        {
            var result
                = (
                      from customer in context.Customers
                      where "Italy" == customer.Country
                      && (customer.CustomerID == "FRANS"
                        || customer.CustomerID == "REGGC"
                      )
                      select new
                      {
                          customer.CustomerID,
                          customer.CompanyName
                      }
                  ).ToArray();

            Assert.Equal(2, result.Length);

            Assert.Equal("Franchi S.p.A.", result.First().CompanyName);
            Assert.Equal("Reggiani Caseifici", result.Last().CompanyName);

            Assert.Equal("FRANS", result.First().CustomerID);
            Assert.Equal("REGGC", result.Last().CustomerID);
        }

        [Fact]
        public virtual void Pagination()
        {
            #region Target MDX Example
            /* Generate this kind of MDX:
            SELECT 
            {} ON COLUMNS, 
            SubSet([Customers].[Customer ID].[Customer ID], 2, 3) ON ROWS
            FROM [NorthwindEF]
            
             -- where 2 - RowsToSkip (START), 3 - RowLen (COUNT)
             --http://www.sqlmonster.com/Uwe/Forum.aspx/sql-server-olap/1004/MDX-Paging
             --(Mosha Pasumansky [MS] - 15 Oct 2003 06:08 GMT)
             */
            #endregion

            var actual = context.Customers
                .Select(c => c.CustomerID)
                .OrderBy(id => id)
                .Skip(2)
                .Take(3).ToArray();

            Assert.Equal(3, actual.Length);
            var expected = new[]
            {
                "ANTON",
                "AROUT",
                "BERGS"
            };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public virtual void OrderByMeasure()
        {
            var actual
                = (
                      from customer in context.Customers
                      from order in context.OrderDetails
                      orderby order.Discount descending
                      select new
                      {
                          customer.CustomerID,
                          customer.CompanyName,
                          order.Discount
                      }
                  ).Take(5).ToArray();

            Assert.Equal(5, actual.Length);
            Assert.Equal("SIMOB", actual.First().CustomerID);
            Assert.Equal("LINOD", actual.Last().CustomerID);
            Assert.Equal(0.3093, actual.Average(r => r.Discount), 4);
        }

        [Fact]
        public virtual void FilterByMeasure()
        {
            var result
                = (
                      from customer in context.Customers
                      from order in context.OrderDetails
                      where order.Discount == 0
                      select new
                      {
                          customer.CustomerID,
                          customer.CompanyName,
                          order.Discount
                      }
                  ).Take(3).ToArray();

            Assert.Equal(3, result.Length);
            Assert.True(result.All(r => r.Discount == 0));
        }

        [Fact]
        public virtual void FilterByAllComparisonOperators()
        {
            var result
                = (
                      from customer in context.Customers
                      from order in context.OrderDetails
                      where customer.Country == "Italy"
                      || (
                            order.Discount > 0 
                            && order.Discount < 0.2
                         )
                         && order.Quantity >= 184
                         && order.Quantity <= 433
                         && order.Quantity != 174
                      select new
                      {
                          customer.CustomerID,
                          customer.CompanyName,
                          order.Discount,
                          order.Quantity
                      }
                  ).ToArray();

            Assert.NotNull(result);
            Assert.Equal(13, result.Length);
            Assert.Equal(3952, result.Sum(r => r.Quantity));
            Assert.Equal(0.1371, result.Average(r => r.Discount), 4);
            Assert.Equal("ANTON", result.First().CustomerID);
            Assert.Equal("WELLI", result.Last().CustomerID);
        }

        [Fact]
        public void MultipleDimensionsInFilterViaAnd()
        {
            var result
                = (
                      from customer in context.Customers
                      from orderHeader in context.Orders
                      where customer.Country == "USA"
                        && orderHeader.Customer.ContactName.StartsWith("A")
                      orderby customer.Country
                      select new 
                      {
                        customer.CustomerID,
                        customer.ContactName,
                        customer.Country
                      }
                  ).Take(8).ToArray();

            Assert.NotNull(result);
            Assert.True
                (
                    result.All
                    (
                        r => r.Country == "USA"
                            && r.ContactName.StartsWith("A") 
                    )
                );
        }

        [Fact]
        public void MultipleDimensionsInFilterViaOr()
        {
            var result
                = (
                      from customer in context.Customers
                      from orderHeader in context.Orders
                      where customer.Country == "USA"
                        || orderHeader.Customer.ContactName.StartsWith("A")
                      orderby customer.Country
                      select new
                      {
                          customer.CustomerID,
                          customer.ContactName,
                          customer.Country
                      }
                  ).Take(8).ToArray();

            Assert.NotNull(result);
            Assert.NotEqual("USA", result.First().Country);
            Assert.True(result.First().ContactName.StartsWith("A"));
            Assert.True
                (
                    result.All
                    (
                        r => r.Country == "USA"
                            || r.ContactName.StartsWith("A")
                    )
                );
        }

        [Fact]
        public virtual void EverythingInOneQuery()
        {
            var result
                = (
                      from customer in context.Customers
                      from order in context.OrderDetails
                      from orderHeader in context.Orders
                      where ("Italy" == customer.Country
                            || customer.Country == "USA")
                        && order.Discount != 0
                        && order.Quantity >= 100
                        && DateTime.Now > orderHeader.OrderDate
#if fixed //TODO: fix calculated member in a WHERE clause with multiple dimensions (try to use base.Visit())
                        && Mdx.CalculatedMemberAsString(
                              "[Measures].[Exp3]"
                              , "[Customers].[Customer ID].MemberValue") != "BONAP"
#endif
                        && 
                        (
                            orderHeader.Customer.ContactName.ToLower().StartsWith("a")
                                || orderHeader.Customer.ContactName.ToLower().StartsWith("b")
#if fixed //TODO: uncomment when fixed parenthesis position for different scan filters
                            || orderHeader.ShipCity.ToUpper().StartsWith("A") //OR within parenthesis
                            )
#else
                        )
                        || orderHeader.ShipCity.ToUpper().Contains("A") //OR outside of parenthesis
#endif
                      orderby customer.Country, 
                        customer.Region ascending,
                        order.Discount descending
                      select new
                      {
                          customer.CustomerID,
                          Calc1 = Mdx.CalculatedMemberAsInt(
                            "[Measures].[Exp1]"
                            , "[Measures].[Quantity] * 1"),
                          Calc2 = Mdx.CalculatedMemberAsInt(
                            "[Measures].[Exp2]"
                            , "[Measures].[Quantity] * 2"),
                          customer.CompanyName,
                          order.Quantity,
                          order.Discount,
                          MaxDiscount = Mdx.Max(order.Discount, "[Customers].[Customer ID].[Customer ID]"),
                          MinDiscount = Mdx.Min(order.Discount, "[Customers].[Customer ID].[Customer ID]")
                      }
                  )
#if fixed //TODO: fix calculated member in a WHERE clause with multiple dimensions
                  .Where(r => Mdx.CalculatedMemberAsString(
                      "[Measures].[Exp3]"
                      , "[Customers].[Customer ID].MemberValue") != "BONAP")
#endif
                  .Skip(2)
                  .Take(8)
                  .ToArray();

            Assert.Equal(8, result.Length);
            Assert.Equal(87, result.First().Quantity);
            Assert.Equal(result.First().Quantity, result.First().Calc1);
            Assert.Equal(result.First().Calc1 * 2, result.First().Calc2);
            Assert.Equal(0, result.First().MinDiscount);
            Assert.Equal(0.3417, result.First().MaxDiscount, 4);
            Assert.Equal(583, result.Last().Quantity);
            Assert.Equal(0.2446, result.ElementAt(1).Discount, 4);
            Assert.Equal("PICCO", result.ElementAt(2).CustomerID);
            Assert.Equal( "Piccolo und mehr", result.ElementAt( 2 ).CompanyName );
        }

        [Fact(Skip = "Feature not implemented yet - postponed")]
        public virtual void ReplaceNonEmptyWithExists()
        {
            /*
SELECT 
EXISTS
(
	{
		(
		[Products].[Product ID].[Product ID], 
		[Products].[Product Name].[Product Name], 
		[Customers].[Customer ID].[Customer ID], 
		[Customers].[Company Name].[Company Name], 
		[Customers].[Contact Name].[Contact Name], 
		[Customers].[Country].[Country],
		[Employees].[Employee ID].[Employee ID], 
		[Employees].[First Name].[First Name], 
		[Employees].[Last Name].[Last Name]
		)
	},
	{
	[Measures].[Quantity], 
	[Measures].[Discount], 
	[Measures].[Unit Price]
	},
   'Order Details'
)
ON ROWS,
{
[Measures].[Quantity], 
[Measures].[Discount], 
[Measures].[Unit Price]
}
ON COLUMNS
FROM [NorthwindEF]
             
             */
            throw new NotImplementedException();
        }


        #region Sorting

        [Fact]
        public virtual void OrderBySingleColumn()
        {
            var result
                = (
                      from customer in context.Customers
                      orderby customer.Country
                      select new
                      {
                          customer.CustomerID,
                          customer.CompanyName,
                          customer.Country
                      }
                  ).ToArray();

            Assert.Equal(92, result.Length);

            Assert.Equal("Argentina", result.First().Country);
            Assert.Equal("CACTU", result.First().CustomerID);

            Assert.Equal("Venezuela", result.Last().Country);
            Assert.Equal("LINOD", result.Last().CustomerID);
        }

        [Fact]
        public virtual void OrderByMultipleColumns()
        {
            var result
                = (
                      from customer in context.Customers
                      from product in context.Products
                      from order in context.OrderDetails
                      orderby customer.Country, 
                        product.CategoryID, 
                        customer.CompanyName
                      select new OrderByMultipleColumnsRecord()
                      {
                          CustomerID = customer.CustomerID,
                          CompanyName = customer.CompanyName,
                          Country = customer.Country,
                          CategoryID = product.CategoryID,
                          Quantity = order.Quantity
                      }
                  ).ToArray();

            Assert.Equal(595, result.Length);

            AssertSortedByCountry(result);
            AssertSortedByCategoryId(result);
        }

        void AssertSortedByCategoryId(IEnumerable<OrderByMultipleColumnsRecord> result)
        {
            result.Take(3).All(r => r.CategoryID == 1);
            result.Skip(3).Take(2).All(r => r.CategoryID == 2);
            result.Skip(3 + 2).Take(3).All(r => r.CategoryID == 3);
        }

        void AssertSortedByCountry(IEnumerable<OrderByMultipleColumnsRecord> result)
        {
            Assert.Equal("Argentina", result.First().Country);
            Assert.Equal("CACTU", result.First().CustomerID);

            Assert.Equal("Venezuela", result.Last().Country);
            Assert.Equal("LINOD", result.Last().CustomerID);
        }

        private class OrderByMultipleColumnsRecord
        {
            public string CustomerID { get; set; }
            public string CompanyName { get; set; }
            public string Country { get; set; }
            public int? CategoryID { get; set; }
            public int Quantity { get; set; }
        }

        #endregion Sorting


        [Fact]
        public virtual void TakeTopN()
        {
            Assert.Equal(5, context.Products.Select(p => p.ProductID)
                .Take(5).ToArray().Length);

            Assert.Equal(10, context.Products.Select(p => p.ProductID)
                .Take(10).ToArray().Length);
        }

        [Fact]
        public virtual void TakeTopNOrdered()
        {
            var result
                = (
                      from customer in context.Customers
                      orderby customer.Country, 
                        customer.CompanyName
                      select new OrderByMultipleColumnsRecord()
                      {
                          CustomerID = customer.CustomerID,
                          CompanyName = customer.CompanyName,
                          Country = customer.Country
                      }
                  ).Take(1)
                  .ToArray();

            Assert.Equal(1, result.Length);

            Assert.Equal("Argentina", result.First().Country);
            Assert.Equal("CACTU", result.First().CustomerID);
        }

    }
}
