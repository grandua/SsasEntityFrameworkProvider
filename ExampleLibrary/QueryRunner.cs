#define OfflineCubeFile

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AgileDesign.SsasEntityFrameworkProvider;
using AgileDesign.Utilities;
using NorthwindEFModel;

namespace UsageExample
{
    public class QueryRunner
    {
        //static void Main()
        //{
        //    //Comment this line if you would like to see query results without generated MDX:
        //    Logger.AddTraceListener(new ConsoleTraceListener());

        //    (new QueryRunner()).RunQuery();
        //}

        //Note: OLAP DB creation is not supported, 
        //so make sure you include 'Database.SetInitializer<YourContext>(null);' in your DbContext
        //(See static constructor for NorthwindEFContext for an example).
#if OfflineCubeFile
        //Note: offline cube file 'NorthwindEF.cub' has only a subset 
        //of NorthwindEF online cube dimensions and measures
        //and data is filtered by ShipCountry == "Brazil"
        readonly NorthwindEFContext context = NorthwindEFContext.CreateForOfflineOlapCube();
#else
        //Uncomment a next line if you have an access to SQL Server with SSAS, to get all NorthwindEF DB data
        readonly NorthwindEFContext context = NorthwindEFContext.CreateForOnlineOlapServer();
#endif


        public void RunQuery()
        {
            //Note: please, mark assemblies containing entities (DbContext) with [assembly: ModelAssembly]
            //for big performance improvement.

            //First query takes some time to initialize Entity Framework and open a connection
            RunQueryWithWhereJoinAndOrderBy();
            //Subsequent queries are much faster
            //See other important comments within RunQueryWithPaginationAndCalculatedMember()
            RunQueryWithPaginationAndCalculatedMember();
            RunQueryWithStartsWithAndExcept();
        }

        void RunQueryWithWhereJoinAndOrderBy()
        {
            var result
                = (
                      from customer in context.Customers
                      from order in context.OrderDetails
                      where ("Brazil" == customer.Country)
                            && order.Quantity >= 100
                      orderby customer.Country,
                          customer.Region
                      select new Result1
                      {
                          CustomerID = customer.CustomerID,
                          CompanyName = customer.CompanyName,
                          Quantity = order.Quantity,
                      }
                  ).Take(8).ToArray();
            
            //Note: This is almost like a regular LINQ but there are no aggregation functions and "group by".
            //But the results are aggregated and according to measure definitions (Quantity, Discount) 
            //in a cube (multidimensional OLAP database)
            //and grouped by dimensional (identifying) properties included (CustomerID, CustomerName).
            //Commonly you can omit "orderby" too, 
            //in such a case the results will be sorted by each dimensional (identifying) property 
            //included into select clause in the sequence of dimensions inclusion. 
            //Default dimension sorting defined in a cube will be always applied 
            //if not overridden by an explicit "orderby".
            //And the most importantly there are no joins in this query, 
            //but the result is not a Cartesian product 
            //- Customer, Order and OrderDetails entities are joined 
            //according to relationships defined in a cube.
            //Simple, is not it? - It is all you need to know to write most of the queries.

            PrintResult1(result);
        }

        void RunQueryWithPaginationAndCalculatedMember()
        {
            var result2
                = (
                      from orderHeader in context.Orders
                      //implicit join
                      from order in context.OrderDetails
                      orderby orderHeader.OrderDate descending
                      select new Result2
                      {
                          CustomerID = orderHeader.CustomerID,
                          //You can also use MDX functions and member properties here 
                          //(with some restrictions for local offline cubes)
                          ServerSideCalculation = Mdx.CalculatedMemberAsInt
                          (
                              "[Measures].[ServerSideCalculation]",
                              "2 * [Measures].[Quantity]"
                          ),
                          OrderDate = orderHeader.OrderDate,
                          Quantity = order.Quantity
                      }
                  )
                    .Skip(80) //Pagination is supported
                    .Take(20)
                    .ToArray();

            //Print result2
            PrintResult2(result2);
        }

        void RunQueryWithStartsWithAndExcept()
        {
            var result = context.Orders
                .Select
                (
                    o => new Result3
                    {
                        OrderID = o.OrderID,
                        RequiredDate = o.RequiredDate,
                        ShipCountry = o.ShipCountry
                    }
                )
                .Except //Union(), Concat(), Intersect() and Except() set operations are supported
                (
                    context.Orders
                //StartsWith(), Contains(), IndexOf(), ToUpper() and ToLower() are supported on string in WHERE
                        .Where(o => o.ShipCountry.StartsWith("U")) 
                        .Select
                        (
                            o => new Result3
                            {
                                OrderID = o.OrderID,
                                RequiredDate = o.RequiredDate,
                                ShipCountry = o.ShipCountry
                            }
                        )
                )
                .Take(10)
                .ToArray();

            PrintResult3(result);
        }


        #region Output

        readonly ConsoleFormatter consoleFormatter = new ConsoleFormatter();

        readonly int[] result1ColumnWidths = { 8, 31, 8 };

        void PrintResult1(IEnumerable<Result1> result)
        {
            Console.WriteLine();
            Console.WriteLine(
                "First query takes some time to initialize Entity Framework and open a connection");

            consoleFormatter.WriteColumnValue("CustomerID", result1ColumnWidths[0]);
            consoleFormatter.WriteColumnValue("CompanyName", result1ColumnWidths[1]);
            consoleFormatter.WriteColumnValue("Quantity", result1ColumnWidths[2]);
            Console.WriteLine();

            foreach (var row in result)
            {
                WriteLine(row);
            }
            Console.WriteLine();
        }

        void WriteLine(Result1 lineObject)
        {
            if (lineObject == null)
            {
                return;
            }
            consoleFormatter.WriteColumnValue(lineObject.CustomerID, result1ColumnWidths[0]);
            consoleFormatter.WriteColumnValue(lineObject.CompanyName, result1ColumnWidths[1]);
            consoleFormatter.WriteColumnValue(lineObject.Quantity, result1ColumnWidths[2]);
            Console.WriteLine();
        }

        class Result1
        {
            public string CustomerID { get; set; }
            public string CompanyName { get; set; }
            public int Quantity { get; set; }
        }

        void WriteLine(Result2 lineObject)
        {
            if (lineObject == null)
            {
                return;
            }
            consoleFormatter.WriteColumnValue(lineObject.OrderID, result2ColumnWidths[0]);
            consoleFormatter.WriteColumnValue(lineObject.CustomerID, result2ColumnWidths[1]);
            consoleFormatter.WriteColumnValue(lineObject.OrderDate, result2ColumnWidths[2]);
            consoleFormatter.WriteColumnValue(lineObject.Quantity, result2ColumnWidths[3]);
            consoleFormatter.WriteColumnValue(lineObject.ServerSideCalculation, result2ColumnWidths[4]);
            Console.WriteLine();
        }

        readonly int[] result2ColumnWidths = { 7, 9, 9, 8, 13 };

        void PrintResult2(IEnumerable<Result2> result2)
        {
            Console.WriteLine();
            Console.WriteLine("Subsequent queries are fast");
            Console.WriteLine();

            consoleFormatter.WriteColumnValue("OrderID", result2ColumnWidths[0]);
            consoleFormatter.WriteColumnValue("CustomerID", result2ColumnWidths[1]);
            consoleFormatter.WriteColumnValue("OrderDate", result2ColumnWidths[2]);
            consoleFormatter.WriteColumnValue("Quantity", result2ColumnWidths[3]);
            consoleFormatter.WriteColumnValue("ServerSideCalculation", result2ColumnWidths[4]);

            Console.WriteLine();

            foreach (var line in result2)
            {
                WriteLine(line);
            }
            Console.WriteLine();
        }

        class Result2
        {
            public int OrderID { get; set; }
            public string CustomerID { get; set; }
            public int? ServerSideCalculation { get; set; }
            public DateTime? OrderDate { get; set; }
            public int Quantity { get; set; }
        }

        class Result3
        {
            public int OrderID { get; set; }
            public DateTime? RequiredDate { get; set; }
            public string ShipCountry { get; set; }
        }

        int[] result3ColumnWidths = {7, 12, 11};

        void PrintResult3(IEnumerable<Result3> result3)
        {
            consoleFormatter.WriteColumnValue("OrderID", result3ColumnWidths[0]);
            consoleFormatter.WriteColumnValue("RequiredDate", result3ColumnWidths[1]);
            consoleFormatter.WriteColumnValue("ShipCountry", result3ColumnWidths[2]);

            Console.WriteLine();

            foreach (var line in result3)
            {
                WriteLine(line);
            }
            Console.WriteLine();
        }

        void WriteLine(Result3 lineObject)
        {
            consoleFormatter.WriteColumnValue(lineObject.OrderID, result3ColumnWidths[0]);
            consoleFormatter.WriteColumnValue(lineObject.RequiredDate, result3ColumnWidths[1]);
            consoleFormatter.WriteColumnValue(lineObject.ShipCountry, result3ColumnWidths[2]);
            Console.WriteLine();
        }

        #endregion

    }
}
