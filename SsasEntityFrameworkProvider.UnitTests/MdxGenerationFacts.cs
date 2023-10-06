using System.Linq;
using AgileDesign.Utilities;
using NorthwindEFModel;
using SsasEntityFrameworkProvider.AcceptanceTests;
using Xunit;

namespace SsasEntityFrameworkProvider.UnitTests
{
    //TODO: uncomment when starting to do pure unit testing
    internal class MdxGenerationFacts 
        : LinqQueryE2EAcceptance
    {
        public MdxGenerationFacts()
        {
            //TODO: Figure out public API which will allow to unit test MDX generation in an isolated from environment way 
            //Note: this API works for unit testing, but it is internal)
            AgileDesign.SsasEntityFrameworkProvider.SsasProvider.SqlServerVersion = "2008";
        }

        [Fact]
        public override void SelectSingleColumnList()
        {
            var context = NorthwindEFContext.CreateForOlap();
            IQueryable<string> customerNamesQuery = context.Customers.Select
                (
                    //Note: if I use anonymous type it confuses column order in result
                    c => c.CompanyName
                );

            Assert.Equal(
                @"
                WITH MEMBER [Measures].[C1]
            	AS 1
                SELECT 
                (
                [Customers].[Company Name].[Company Name]
                )
                ON ROWS,
                {
                [Measures].[C1] 
                }
                ON COLUMNS
                FROM <CubeNamePlaceholder>
                ".NormalizedSpaces()
                , customerNamesQuery.NormalizedMdx());

            IQueryable<string> employeeFNameQuery = context.Employees.Select
                (
                    //Note: if I use anonymous type it confuses column order in result
                e => e.FirstName
                );

            Assert.Contains("( [Employees].[First Name].[First Name] ) ON ROWS"
                            , employeeFNameQuery.NormalizedMdx());
        }

        [Fact]
        public override void SelectMultipleColumns()
        {
            var context = NorthwindEFContext.CreateForOlap();
            var customerColumnsQuery
                = from customer in context.Customers
                  select new
                  {
                      customer.CustomerID,
                      customer.CompanyName
                  };


            Assert.Equal(
                @"
                WITH MEMBER [Measures].[C1]
            	AS 1
                SELECT 
                (
                [Customers].[Customer ID].[Customer ID],
                [Customers].[Company Name].[Company Name]
                )
                ON ROWS,
                {
                [Measures].[C1] 
                }
                ON COLUMNS
                FROM <CubeNamePlaceholder>
                ".NormalizedSpaces()
                , customerColumnsQuery.NormalizedMdx());
        }

        [Fact(Skip = "Not implemented")]
        public override void Select1MeasureColumn()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void SelectMeasureColumnsWith2Keys()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void SelectMeasureColumnsWithAKey()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void SelectMultipleMeasureColumns()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void SelectScalar()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void SelectMeasuresAndSingleDimension()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void SelectMeasuresAndMultipleDimensions()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void SimpleWhere()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void SelectMultipleMeasureGroups()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void OrderBySingleColumn()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void OrderByMultipleColumns()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void ReplaceNonEmptyWithExists()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void TakeTopN()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void TakeTopNOrdered()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void WhereWithMultipleExpressions()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void EverythingInOneQuery()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void FilterByAllComparisonOperators()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void FilterByMeasure()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void OrderByMeasure()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void Pagination()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void  MutlipleMeasureGroupsWithFilterAndSortingMarcoCase()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void TooManyAgrumentsInHeadMdxFunctionBug()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void AssociationPropertyTraversal()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void NoResultsIfTopWithMultipleDimensions()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void ExplicitJoinDoesNotFailButLogsWarning()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void FilterByNonLiteralValueScottsCase()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void FilterWhenMeasuresOnlyInOutput()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void GroupByAsDistinctErrorMessageScottsCase()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void CalculatedMember()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void CalculatedMemberInWhere()
        {
        }

        [Fact(Skip = "Not implemented")]
        public override void ExplicitAggregationIncludingDimension()
        {
        }

    }
}