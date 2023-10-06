using System;
using System.Data.Entity;
using System.Data.Objects;
using NorthwindEFModel;
using System.Linq;
using Xunit;

namespace SqlEntityFrameworkProvider.AcceptanceTests
{
    public class LinqToSqlEntitiesAcceptance
    {
        public LinqToSqlEntitiesAcceptance()
        {
            Database.SetInitializer<NorthwindEFContext>(null);
        }


        NorthwindEFContext context = NorthwindEFContext.CreateForSql();

        [Fact]
        public void GetSingleAggregate()
        {
            string result = context.Customers.Max(c => c.CustomerID);
            Assert.Equal("WOLZA", result);
        }

        [Fact]
        public void GroupBy()
        {
            var result
                = (
                      from customer in context.Customers
                      group customer by customer.City
                      into customerCity
                      select new
                      {
                          customerCity.Key,
                          MaxCustomerId = customerCity.Max(c => c.CustomerID)
                      }
                  ).ToArray();

            Assert.Equal(69, result.Count());
            Assert.Equal("Aachen", result.First().Key);
            Assert.Equal("DRACD", result.First().MaxCustomerId);
        }

        /// <summary>
        /// Group By with subquery
        /// </summary>
        [Fact]
        public void GroupByWithCompositeArgument()
        {
            var result
                = (
                      from customer in context.Customers
                      group customer by customer.Country + customer.City
                          into customerCity
                          select new
                          {
                              customerCity.Key,
                              MaxCustomerId = customerCity.Max(cs => cs.City + cs.CustomerID)
                          }
                  ).ToArray();

            Assert.Equal(69, result.Count());
            Assert.Equal("ArgentinaBuenos Aires", result.First().Key);
            Assert.Equal("Buenos AiresRANCH", result.First().MaxCustomerId);
        }

        [Fact]
        public void OrderBySingleColumn()
        {
            var context = NorthwindEFContext.CreateForSql();
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

            Assert.Equal(91, result.Length);

            Assert.Equal("Argentina", result.First().Country);
            Assert.Equal("CACTU", result.First().CustomerID);

            Assert.Equal("Venezuela", result.Last().Country);
            Assert.Equal("GROSR", result.Last().CustomerID);
        }

        [Fact]
        public void CrossJoin()
        {
            var context = NorthwindEFContext.CreateForSql();
            var result
                = (
                      from orderDetails in context.OrderDetails
                      from product in context.Products
                      select new
                      {
                          product.ProductID,
                          product.ProductName,
                          orderDetails.Quantity,
                          orderDetails.Discount,
                          orderDetails.UnitPrice
                      }
                  );
            Console.WriteLine(result);

            Assert.NotEqual(0, result.ToString().Length);
        }

        [Fact]
        public void SimpleParameterizedLinq()
        {
            Console.WriteLine("SimpleParameterizedLinq");

            using (var context = NorthwindEFContext.CreateForSql())
            {
                string parameterVariable = "ALFKI";
                var query 
                    = from c in context.Customers 
                      where c.CustomerID == parameterVariable 
                      select c;

                Console.WriteLine("  Query Results");
                foreach (Customer c in query)
                {
                    Console.WriteLine("    {0}", c.CompanyName);
                    Assert.False(string.IsNullOrWhiteSpace(c.CompanyName));
                    Assert.Equal("Alfreds Futterkiste", c.CompanyName);
                }
            }

            Console.WriteLine();
        }

        [Fact]
        public void ComplexParameterizedLinq()
        {
            using (var context = NorthwindEFContext.CreateForSql())
            {
                PreloadCustomers(context);
                var query
                    = from c in context.Customers
                      from o in c.Orders
                      from d in o.OrderDetails
                      where c.CustomerID == "ALFKI"
                      select new
                      {
                          Order = o,
                          d.ProductID,
                          d.Product.Category.CategoryName
                      };

                Console.WriteLine(query.ToString());

                var resultList = query.ToList();
                Assert.Equal(12, resultList.Count());
                Assert.Equal(28, resultList.First().ProductID);
                Assert.Equal(71, resultList.Last().ProductID);
                Assert.Equal("Dairy Products", resultList.Last().CategoryName);
                foreach (var result in resultList)
                {
                    Assert.Equal("Obere Str. 57", result.Order.Customer.Address);
                    Assert.Equal("Alfreds Futterkiste", result.Order.Customer.CompanyName);
                    Assert.InRange(result.Order.OrderDate.Value.Year, 1997, 1998);
                }

                var groupQuery = from item in query.Distinct()
                                 group item by item.ProductID
                                 into groupedByProduct
                                 orderby groupedByProduct.Key
                                 select groupedByProduct;

                var groupDistinctTopQuery = groupQuery.Take(2);
                Console.WriteLine(groupDistinctTopQuery.ToString());
                var groupedProduct = groupDistinctTopQuery.ToList();

                Assert.Equal(2, groupedProduct.Count());
                Assert.Equal(3, groupedProduct.First().Key);
                Assert.Equal(6, groupedProduct.Last().Key);
            }

            Console.WriteLine();
        }

        void PreloadCustomers(NorthwindEFContext context)
        {
            var loadCustomerQuery
                = from c in context.Customers
                  where c.CustomerID == "ALFKI"
                  select c;

            Console.WriteLine(context.Database.Connection.ConnectionString);
            Console.WriteLine(loadCustomerQuery.ToString());

            Assert.NotNull(loadCustomerQuery.First().CompanyName);
        }

        #region Support for translating to Like
        static void QueryWithStartsWith()
        {
            Console.WriteLine("QueryWithStartsWith");
            using (var context = NorthwindEFContext.CreateForSql())
            {
                var query =
                    from c in context.Customers
                    where c.CompanyName.StartsWith("La")
                    select c;
                ExecuteQuery(query);
            }
        }
        #endregion

        #region Helper Methods
        private static void ExecuteQuery(IQueryable<Customer> query)
        {
            Console.WriteLine("-- generated SQL");
            Console.WriteLine(((ObjectQuery)query).ToTraceString());

            Console.WriteLine();
            Console.WriteLine("-- query results");
            foreach (Customer c in query)
                Console.WriteLine("    {0}", c.CompanyName);
            Console.WriteLine();
        }

        #endregion

    }
}