using System.Reflection;
using System.Text;
using AgileDesign.Utilities;
using LogicNP.CryptoLicensing;

namespace AgileDesign.SsasEntityFrameworkProvider.Utilities
{
    class AgileDesignLicense 
        : CryptoLicense
    {
        public AgileDesignLicense()
            : base
            (
                /*license code:*/
                "FgS+k5wgLlR8ac4BHgBDG1NzYXNFbnRpdHlGcmFtZXdvcmtQcm92aWRlcgIAAAAMYJwtfCM+ggEQAYIKmVCQzN+P48Eb2VDlIwYuKhg2QgiR7S8kd2qUcEjawcY2nYjQ/Lt0H+CqQKTQ4w=="
                /*validation key:*/
                , @"AMAAMACnHx9Ww0VTmTHaAZt2WGZYuio4Ktf7Lm+e1KB3Jz+Co0o+Bz47Oyc4JaVejLRnjbsDAAEAAQ=="
            )
        {
            StorageMode = LicenseStorageMode.ToRegistry;
            LicenseServiceURL = "http://www.agiledesignllc.com/LicService/Service.asmx";
        }

        /// <summary>
        /// May be slow
        /// </summary>
        internal void DisableInLicenseServiceIfAssemblyIsTempered()
        { //notify our license service that our assembly is tempered
            string publicKey = Assembly.GetExecutingAssembly().GetPublicKey();
            if (publicKey.Length != 160 * 2
                || publicKey.Left(18) != "002400000480000094")
            {
                DisableInLicenseService();
            }
        }

        public override byte[] GetLocalMachineCode()
        {
            return Encoding.Unicode.GetBytes(MachineId.GetMachineNameWithDomain());
        }

        public override bool IsMachineCodeValid()
        {
            string embeddedMachineCode = Encoding.Unicode.GetString(MachineCode);
            string localMachineCode = MachineId.GetMachineNameWithDomain();
            //if local machine is not in a domain it is OK to match by machineName only
            return (localMachineCode == embeddedMachineCode.Right(localMachineCode.Length));
        }

        internal string UserNameAndCompany
        {
            get
            {
                return GetUserNameAndCompany(UserData);
            }
        }

        internal string GetUserNameAndCompany(string userData)
        {
            if (string.IsNullOrWhiteSpace(userData)
                || ! userData.Contains("#"))
            {
                return ""; //unrecognized UserData format
            }
            string[] userDataParts = userData.Split('#');
            if (userDataParts.Length < 3)
            {
                return ""; //unrecognized UserData format
            }
            string userName = userDataParts[0];
            string companyName = userDataParts[2];
            return string.Format("licensed to {0}#{1}", userName, companyName);
        }

        internal bool ShowEvaluationInfoDialog()
        {
            bool isRegistered =  ShowEvaluationInfoDialog
                (
                    Assembly.GetExecutingAssembly().GetProductName(),
                    "http://www.agiledesignllc.com/Products"
                );   

            if(isRegistered)
            {
                Save();
            }
            return isRegistered;
        }
    }
}
