using System;
using System.Configuration;
using System.Linq;
using AgileDesign.AdomdExtensions;
using Microsoft.AnalysisServices.AdomdClient;
using ModelExample;
using Xunit;

namespace LinqToOlap.AcceptanceTests
{
    public class AdomdConnectionExtensionsAcceptance
    {
        private AdomdConnection connection = new AdomdConnection(GetConnectionString());

        public AdomdConnectionExtensionsAcceptance()
        {
            //AdomdConnectionExtensions.Mapper = new System.Data.Entity.ModelConfiguration.EntityTypeConfiguration<T>();
        }

        [Fact]
        public void NoRowsExecuteMdxCommand()
        {
            var products = connection.ExecuteMdxCollection<Product>(
                @"
                select [Product].[Product Name].[Product Name]
                on columns
                from ModelExampleCube
                ");

            Assert.Equal(0, products.Count());
        }

        [Fact]
        public void NoRowsExecuteScalarNonNullableMdxCommandThrowsError()
        {
            Assert.Throws
            (
                typeof (InvalidCastException)
                , () => connection.ExecuteMdxScalar<int>
                          (
                              @"
                                select [Product].[Product Name].[Product Name]
                                on columns
                                from ModelExampleCube
                                "
                          )
            );
        }

        [Fact]
        public void NoRowsExecuteScalarComplexTypeMdxCommand()
        {
            var product = connection.ExecuteMdxScalar<Product>(
                @"
                select [Product].[Product Name].[Product Name]
                on columns
                from ModelExampleCube
                ");

            Assert.Null(product);
        }

        [Fact]
        public void NullFromExecuteMdxCommand()
        {
            var products = connection.ExecuteMdxCollection<Product>(
                @"
                select
                from ModelExampleCube
                where [Product].[Product Name].[Product Name].[Does not exist]
                ");

            Assert.Equal(0, products.Count());
        }

        [Fact]
        public void ScalarStringNoCaptionMdxQueryResult()
        {
            string value = connection.ExecuteMdxScalar<string>(
                @"
                with member [Product].[Product Name].[All].[Test Product]
	            as ""test""
                select
                from ModelExampleCube
                where [Product].[Product Name].[All].[Test Product]
                ");

            Assert.Equal("test", value);
        }

        [Fact]
        public void ScalarWithCaptionMdxQueryResult()
        {
            ScalarTMdxQueryResult<int>("42");
            ScalarTMdxQueryResult<int?>("42");
            ScalarTMdxQueryResult<int?>("null");
            ScalarTMdxQueryResult<double>("42.42");
            ScalarTMdxQueryResult<double?>("null");
            ScalarTMdxQueryResult<decimal>("42.42");
            ScalarTMdxQueryResult<decimal?>("null");
            ScalarTMdxQueryResult<string>("test");
            ScalarTMdxQueryResult<string>("null");
            ScalarTMdxQueryResult<DateTime>(DateTime.Now.ToString());
            ScalarTMdxQueryResult<DateTime?>(DateTime.Now.ToString());
            ScalarTMdxQueryResult<DateTime?>("null");
            ScalarTMdxQueryResult<Product>("null");
        }

        [Fact]
        public void NonNullubleScalarThrowsErrorWhenQueryReturnsNull()
        {
            NonNullubleThrowsErrorWhenQueryReturnsNull<int>();
            NonNullubleThrowsErrorWhenQueryReturnsNull<double>();
            NonNullubleThrowsErrorWhenQueryReturnsNull<decimal>();
            NonNullubleThrowsErrorWhenQueryReturnsNull<DateTime>();
        }

        private void NonNullubleThrowsErrorWhenQueryReturnsNull<T>()
        {
            Assert.Throws(typeof(InvalidCastException)
                          , () => ScalarTMdxQueryResult<T>("null"));
        }

        /// <typeparam name="T">
        /// T is a real type of both input value and member value result
        /// </typeparam>
        private void ScalarTMdxQueryResult<T>(string stringMemberTestValue)
        {
            string memberValue = stringMemberTestValue;
            memberValue = WrapInQuotesIfNeeded<T>(memberValue);
            T result = connection.ExecuteMdxScalar<T>(
                string.Format(
                    @"
                    with member [Product].[Product Name].[All].[Test Product]
	                    as {0}
                    select [Product].[Product Name].[Product Name].AllMembers
                    on columns
                    from ModelExampleCube
                    "
                    , memberValue)
                );
            if (stringMemberTestValue.ToLower() == "null")
            {
                Assert.Null(result);
            }
            else
            {
                Assert.Equal(stringMemberTestValue, result.ToString());
            }
        }

        private string WrapInQuotesIfNeeded<T>(string memberValue)
        { //Note: I may need to move this method into TestBase
            if(memberValue.ToLower() != "null"
                && IsTypeValueRequiringQuotes<T>())
            {
                return string.Format("\"{0}\"", memberValue);
            }
            return memberValue;
        }

        private bool IsTypeValueRequiringQuotes<T>()
        {
            return (new[] { typeof(string), typeof(DateTime), typeof(DateTime?) }).Contains(typeof(T));
        }

        private static string GetConnectionString()
        {
            return ConfigurationManager.ConnectionStrings["ModelExampleCube"].ConnectionString;
        }

    }
}
