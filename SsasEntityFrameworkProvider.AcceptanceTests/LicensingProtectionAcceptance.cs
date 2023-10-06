using System;
using AgileDesign.SsasEntityFrameworkProvider;
using AgileDesign.SsasEntityFrameworkProvider.Internal;
using AgileDesign.SsasEntityFrameworkProvider.Internal.MdxGeneration;
using Xunit;

namespace SsasEntityFrameworkProvider.AcceptanceTests
{
    public class LicensingProtectionAcceptance
    {
        [Fact (Skip = "Run this test manually because of a popup message")]
        public void ActivateLicenseShowsPopup()
        {
            Console.WriteLine("result = {0}", SsasProvider.ActivateLicense());
        }

        [Fact]
        public void DefaultNamingConventionSetForGoodLicense()
        {
            var mdxGenerator = new MdxGenerator(StoreVersion.Sql10);
            Assert.NotNull(mdxGenerator.NamingConvention);
            Assert.IsType(typeof(AddSpacesToCamelCasingWordsConvention), mdxGenerator.NamingConvention);
        }

        //TODO: Skip this test once done with License Protection testing
        [Fact(Skip = "Manual test, shows messagebox")]
        public void DefaultNamingConventionNotSetForBadLicense()
        {
            string originalLicenceCode = MdxGenerator.License.LicenseCode;
            try
            {
                MdxGenerator.License.LicenseCode = "NotValidLicenseCode";
                var mdxGenerator = new MdxGenerator(StoreVersion.Sql10);
                Assert.Throws(typeof(Exception), () => mdxGenerator.NamingConvention);
            }
            finally
            {
                MdxGenerator.License.LicenseCode = originalLicenceCode;
            }
        }

        [Fact]
        public void UserNameAndCompanyReturnedWhenPresent()
        {
            Assert.Equal("licensed to testUserName#testCompanyName"
                , MdxGenerator.License.GetUserNameAndCompany("testUserName#testEmail#testCompanyName"));
        }

        [Fact]
        public void EmptyStringIsReturnedWhenWrongUserDataFormat()
        {
            Assert.Equal("", MdxGenerator.License.GetUserNameAndCompany(null));
            Assert.Equal("", MdxGenerator.License.GetUserNameAndCompany("testUserName"));
            Assert.Equal("", MdxGenerator.License.GetUserNameAndCompany("u#e"));
        }

    }
}
