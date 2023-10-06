using System;

namespace AgileDesign.SsasEntityFrameworkProvider.Attributes
{
    /// <summary>
    /// Marker for assemblies that should be used to search for custom types
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public class CustomAssemblyAttribute
        : Attribute
    {
    }
}