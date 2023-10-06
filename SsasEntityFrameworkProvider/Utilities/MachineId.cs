using System.Net.NetworkInformation;

namespace AgileDesign.SsasEntityFrameworkProvider.Utilities
{
    /// <summary>
    /// Centralized exception handling and reporting
    /// </summary>
    internal static class MachineId
    {
        internal static string GetMachineNameWithDomain()
        {
            var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            string domainName = ipProperties.DomainName;
            if(string.IsNullOrWhiteSpace(domainName))
            {
                return ipProperties.HostName;
            }
            return string.Format("{0}.{1}", ipProperties.HostName, domainName);
        }

        internal static string GetDomainName()
        {
            return IPGlobalProperties.GetIPGlobalProperties().DomainName;
        }

        internal static string GetMachineName()
        {
            return IPGlobalProperties.GetIPGlobalProperties().HostName;
        }
    }
}
