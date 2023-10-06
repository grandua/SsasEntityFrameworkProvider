using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using AgileDesign.Utilities;

namespace AgileDesign.AdomdExtensions
{
    /// <summary>
    ///   It is a default mapping scheme and a base class for any other mapping strategies <br />
    ///   Uses exact column to property name match only <br />
    ///   Complex types are not supported <br />
    ///   Use EntityFrameworkMdxToEntityMapper for more complex scenarios <br />
    ///   Or create your own derived class inherited from MdxToEntityNameMapper
    /// </summary>
#if ! DEBUG
    [DebuggerStepThrough]
#endif
    public class MdxToEntityNameMapper
    {
        public MdxToEntityNameMapper()
        {
        }
        public MdxToEntityNameMapper(IDataRecord reader)
        {
            Reader = reader;
        }

        IDataRecord Reader { get; set; }

        IDictionary<string, string> columnNames;
        IDictionary<string, string> MdxColumnNames
        {
            get 
            { 
                return columnNames 
                    ?? ( columnNames = GetColumnNames(Reader) ); 
            }
        }


        public TEntity MapToEntity<TEntity>(IDataRecord reader)
            where TEntity : new()
        {
            var result = new TEntity();
            Reader = reader;
            foreach (var propertyInfo in GetPropertiesForMapping(typeof(TEntity)))
            {
                propertyInfo.SetValue(result, GetValue(propertyInfo), null);
            }
            columnNames = null;
            return result;
        }


        /// <remarks>
        ///   TODO: http://www.toodledo.com/views/search.php?x=1025983;id=65049867
        /// </remarks>
        public string GetMdxColumnName(string propertyName)
        {
            string propertyNormalizedName = GetPureColumnName(propertyName);
            if (! MdxColumnNames.ContainsKey(propertyNormalizedName))
            {
                LogMappingFailure(propertyName);
                return null;
            }
            return MdxColumnNames[propertyNormalizedName];
        }

        void LogMappingFailure(string propertyName)
        {
            Logger.Instance.TraceEvent(
                TraceEventType.Verbose, "Could not map property '{0}'", propertyName);
        }

        object GetValue(PropertyInfo property)
        {
            Contract.Requires(property != null);

            string mdxColumnName = GetMdxColumnName(property.Name);
            if (mdxColumnName == null)
            {
                LogWarningIfComplexType(property);
                return null;
            }
            return Reader[mdxColumnName].ConvertTo(property.PropertyType);
        }

        static void LogWarningIfComplexType(PropertyInfo property)
        {
            if (!IsComplexType(property))
            {
                return;
            }

            Logger.Instance.TraceEvent
                (
                    TraceEventType.Warning,
                    "Could not map a complex type for property '{0}'",
                    property.Name
                );
            Logger.Instance.TraceEvent
                (
                    TraceEventType.Warning,
                    "Set Mapper property to an instance of EntityFrameworkMdxToEntityMapper"
                );
        }

        static bool IsComplexType(PropertyInfo property)
        {
            return ( ! property.PropertyType.IsPrimitive )
                   && property.PropertyType != typeof(string);
        }

        protected virtual IEnumerable<PropertyInfo> GetPropertiesForMapping(Type type)
        {
            //TODO: allow end user configuration of visibility and property/field selection
            Contract.Requires(type != null);

            return type.GetProperties().Where(p => p.CanWrite);
        }

        IDictionary<string, string> GetColumnNames(IDataRecord reader)
        {
            var result = new Dictionary<string, string>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                string[] nameParts = reader.GetName(i).Split('.');
                string levelName = ( nameParts.Length <= 3 )
                                       ? nameParts.Last()
                                       : nameParts[2];

                result[GetPureColumnName(levelName)]
                    = reader.GetName(i);
            }
            return result;
        }

        string GetPureColumnName(string inputColumnName)
        {
            return inputColumnName.Replace("[", "").Replace("]", "")
                .Replace(" ", "").Replace(".", "").ToLower();
        }
    }
}