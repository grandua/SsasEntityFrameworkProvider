using System;

namespace AgileDesign.SsasEntityFrameworkProvider.Attributes
{
    /// <summary>
    /// Marker for assemblies that should be used to search for domain model types
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class ModelAssemblyAttribute 
        : CustomAssemblyAttribute
    {
    }
}
