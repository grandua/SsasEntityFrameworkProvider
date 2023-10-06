using System;

namespace AgileDesign.SsasEntityFrameworkProvider.Attributes
{
    /// <summary>
    /// Annotate with this attribute dimensional properties of classes mapped to measure groups 
    /// if you would like to use those properties in queries.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class DimensionPropertyAttribute : Attribute, INamed
    {
        string INamed.Name 
        {
            get { return DimensionName; }
        }

        public string DimensionName { get; set; }

        public DimensionPropertyAttribute()
        {
        }

        public DimensionPropertyAttribute(string dimensionName)
            : this()
        { //TODO: Provide Ctor with "Type entityType" parameter and map entityType to a dimension name
            DimensionName = dimensionName;
        }
    }

}