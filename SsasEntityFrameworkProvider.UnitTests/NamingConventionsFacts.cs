using System.Linq;
using System.Threading;
using AgileDesign.SsasEntityFrameworkProvider;
using AgileDesign.SsasEntityFrameworkProvider.Internal.MdxGeneration;
using AgileDesign.Utilities;
using NorthwindEFModel;
using Xunit;

namespace SsasEntityFrameworkProvider.UnitTests
{
    public class NamingConventionsFacts
    {
        NorthwindEFContext context = NorthwindEFContext.CreateForOlap();

        public NamingConventionsFacts()
        {
            SsasProvider.SqlServerVersion = "2008";
        }

        [Fact]
        public void CamelCaseIsConvertedToSpaceSeparatedWordsByDefualt()
        {
            IQueryable<string> employeeFNameQuery = context.Employees.Select
                (
                    e => e.FirstName
                );

            Assert.Contains("( [Employees].[First Name].[First Name] ) ON ROWS"
                            , employeeFNameQuery.NormalizedMdx());
        }

        [Fact]
        public void CamelCaseIsConvertedToSpaceSeparatedWordsByARespectiveConvention()
        {
            lock (locker)
            {
                Mdx.NamingConvention = new AddSpacesToCamelCasingWordsConvention();
                CamelCaseIsConvertedToSpaceSeparatedWordsByDefualt();
                //Mdx.NamingConvention = null;
            }
        }

        static object locker = new object();

        [Fact]
        public void OriginalNamesArePreservedByARespectiveConvention()
        {
            lock (locker)
            {
                Mdx.NamingConvention = new PreserveSpecifiedNameConvention();
                try
                {
                    IQueryable<string> employeeFNameQuery = context.Employees.Select
                        (
                            e => e.FirstName
                        );

                    Assert.Contains("( [Employees].[FirstName].[FirstName] ) ON ROWS"
                        , employeeFNameQuery.NormalizedMdx());
                }
                finally
                {
                    Mdx.NamingConvention = null;
                }
            }
        }

    }
}
