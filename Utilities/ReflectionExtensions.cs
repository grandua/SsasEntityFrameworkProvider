//#define needLinqPadDumper
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AgileDesign.SsasEntityFrameworkProvider.Attributes;

#if needLinqPadDumper
    using LINQPad;
    using System.Diagnostics;
#endif

namespace AgileDesign.Utilities
{
    public static class ReflectionExtensions
    {
        #region GetCustomAssemblies

        static Assembly[] customAssemblies;
        /// <summary>
        /// Filters out MS .Net Framework assemblies
        /// </summary>
        /// <returns>
        /// Non- .Net Framework assemblies
        /// </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "<>s__LockTaken0")]
        public static IEnumerable<Assembly> CustomAssemblies
        {
            get
            {
                lock (locker)
                {
                    AppDomain appDomain = AppDomain.CurrentDomain;
                    return customAssemblies 
                        ?? ( customAssemblies = GetCustomAssemblies(appDomain) );
                }
            }
        }

        /// <summary>
        /// Returns assemblies marked with a CustomAssemblyAttribute (or inherited)
        /// or non- .Net Framework assemblies if none is marked
        /// </summary>
        /// <param name="appDomain"></param>
        /// <returns>
        /// Assemblies marked with a CustomAssemblyAttribute or non- .Net Framework assemblies if none is marked
        /// </returns>
        public static Assembly[] GetCustomAssemblies(this AppDomain appDomain)
        {
            Contract.Requires<ArgumentNullException>(appDomain != null);
            var usedAssemblies = appDomain.GetAssemblies();
            var markedCustomAssemblies = usedAssemblies
                .Where(assembly => HasCustomAssemblyAttribute(assembly)).ToArray();
            if (markedCustomAssemblies.Any())
            {
                return markedCustomAssemblies;
            }
            return GetCustomAssemblies(usedAssemblies);
        }

        /// <summary>
        /// Fallback method if no assembly is marked with ModelAssemblyAttribute
        /// </summary>
        static Assembly[] GetCustomAssemblies(IEnumerable<Assembly> usedAssemblies)
        {
            return (
                       from assembly in usedAssemblies
                       where (!excludedAssemblyNames.Contains(assembly.GetName().Name)
                              && !exludedPublicTokens.Contains(
                                  assembly.GetName().GetPublicKeyToken(), new ByteArrayEqualityComparer()))
                       select assembly
                   ).ToArray();
        }

        static bool HasCustomAssemblyAttribute(Assembly assembly)
        {
            return assembly.GetCustomAttributes(typeof(CustomAssemblyAttribute), true).Any();
        }

        static byte[][] exludedPublicTokens = new []
        {
            //AgileDesign.*: 0c609c2d7c233e82
            new byte[] {0x0c, 0x60, 0x9c, 0x2d, 0x7c, 0x23, 0x3e, 0x82}, 
            //LogicNP: PublicKeyToken=4a3c0a4c668b48b4
            new byte[] {0x4a, 0x3c, 0x0a, 0x4c, 0x66, 0x8b, 0x48, 0xb4},
            //MS System.*:
            new byte[] {0xb7, 0x7a, 0x5c, 0x56, 0x19, 0x34, 0xe0, 0x89},
            new byte[] {0x31, 0xbf, 0x38, 0x56, 0xad, 0x36, 0x4e, 0x35},
            new byte[] {0xb0, 0x3f, 0x5f, 0x7f, 0x11, 0xd5, 0x0a, 0x3a},
            //MS Microsoft.Adomd.*:
            new byte[] {0x89, 0x84, 0x5d, 0xcd, 0x80, 0x80, 0xcc, 0x91},
            //MS SQL Sample EF Provider: 095b6aea3a01b8e0
            new byte[] {0x09, 0x5b, 0x6a, 0xea, 0x3a, 0x01, 0xb8, 0xe0},
            //xunit:
            new byte[] {0x8d, 0x05, 0xb1, 0xbb, 0x7a, 0x6f, 0xdb, 0x6c},
            //NUnit: 96d09a1eb7f44a77
            new byte[] {0x96, 0xd0, 0x9a, 0x1e, 0xb7, 0xf4, 0x4a, 0x77},
            //mbUnit: 5e72ecd30bc408d5
            new byte[] {0x5e, 0x72, 0xec, 0xd3, 0x0b, 0xc4, 0x08, 0xd5},
            //LINQPad:
            new byte[] {0x21, 0x35, 0x38, 0x12, 0xcd, 0x2a, 0x2d, 0xb5}, 
            //new byte[] {0x, 0x, 0x, 0x, 0x, 0x, 0x, 0x},
        };

        static string[] excludedAssemblyNames = new[]
        {
            "Anonymously Hosted DynamicMethods Assembly",
            //"SqlEntityFrameworkProvider"
        };

        static object locker = new object();

        #endregion


        #region Has Attribute

        public static string GetNameFromAttribute<T>
            (
                this string typeName,
                string propertyName
            )
            where T : INamed
        {
            return GetNameFromAttribute(typeName, propertyName, typeof(T));
        }

        public static string GetNameFromAttribute
            (
                this string typeName,
                string propertyName,
                Type attributeType
            )
        { //TODO: refactor out duplication between GetNameFromAttribute() and HasPropertyAttribute, cache results for cases when both are being called
            var matchingCustomTypes = GetCustomTypesByName(typeName);
            if (!matchingCustomTypes.Any())
            {
                throw CreateNoCustomTypesFoundException(typeName);
            }
            return
            (
                from type in matchingCustomTypes
                where type.HasPropertyAttribute(propertyName, attributeType)
                from attribute in type.GetProperty(propertyName).GetCustomAttributes(true)
                where attribute.GetType() == attributeType
                select ((INamed)attribute).Name
            ).Single();
        }

        public static bool HasPropertyAttribute
            (
                this Type type, 
                string propertyName,
                Type attributeType
            )
        {
            Contract.Requires<ArgumentNullException>(type != null);

            return type.GetProperties().Any(p => p.Name == propertyName)
                && Attribute.IsDefined(type.GetProperty(propertyName), attributeType, true /*inherit*/);
        }

        public static bool HasPropertyAttribute<T>
            (
                this string typeName, 
                string propertyName
            )
        {
            return HasPropertyAttribute(typeName, propertyName, typeof(T));
        }

        public static bool HasPropertyAttribute
            (
                this string typeName, 
                string propertyName, 
                Type attributeType
            )
        {
            var matchingCustomTypes = GetCustomTypesByName(typeName);
            if ( ! matchingCustomTypes.Any())
            {
                throw CreateNoCustomTypesFoundException(typeName);
            }
            return matchingCustomTypes.Any(t => t.HasPropertyAttribute(propertyName, attributeType));
        }

        public static bool HasTypeLevelAttribute<T>(this string typeName)
        {
            return HasTypeLevelAttribute(typeName, typeof(T));
        }

        public static bool HasTypeLevelAttribute
            (
                this string typeName,
                Type attributeType
            )
        {
            var matchingCustomTypes = GetCustomTypesByName(typeName);
            if ( ! matchingCustomTypes.Any())
            {
                throw CreateNoCustomTypesFoundException(typeName);
            }
            return
            (
                from type in matchingCustomTypes
                where Attribute.IsDefined(type, attributeType, true /*inherit*/)
                select type
            ).Any();
        }

        static ArgumentException CreateNoCustomTypesFoundException(string typeName)
        {
            return new ArgumentException(
                string.Format("No custom types have been found matching type name = '{0}'"
                              , typeName)
                );
        }

        static IEnumerable<Type> GetCustomTypesByName(string typeName)
        { //TODO: profile and optimize this method - it may be slower than an SSAS query with select Measures.Members 
            lock (locker)
            {
                if (!CustomTypes.ContainsKey(typeName))
                {
                    var matchingTypes = GetCustomTypesByNameInternal(typeName);
                    if( ! matchingTypes.Any())
                    {
                        matchingTypes = GetCustomTypesByNameInternal(typeName, true /*refreshCache*/);
                    }
                    //Cache results to avoid multiple reflection queries because reflection is slow
                    CustomTypes[typeName] = matchingTypes;
                }
            }
            return CustomTypes[typeName];
        }

        static Type[] GetCustomTypesByNameInternal
            (
                string typeName, 
                bool shouldRefreshCache = false
            )
        {
            if(shouldRefreshCache)
            {
                customAssemblies = null;
            }
            return (
                       from assembly in CustomAssemblies
                       from type in assembly.GetTypes()
                       where type.Name == typeName
                       select type
                   ).ToArray();
        }

        static Dictionary<string, IEnumerable<Type>> customTypes;
        static Dictionary<string, IEnumerable<Type>> CustomTypes
        {
            get
            {
                return customTypes
                    ?? Init.InitIfNullLocking(ref customTypes, locker);
            }
        }

        #endregion


        #region Type Extensions

        public static bool IsNullable(this Type type)
        {
            Contract.Requires<ArgumentNullException>(type != null);

            return type.IsClass
                   || (type.IsGenericType
                        && type.GetGenericTypeDefinition() == typeof(Nullable<>));
        }


        #endregion


        #region Object Extensions 

        /// <summary>
        /// Get all public instance property values
        /// </summary>
        /// <param name="instance">
        /// Complex object instance who's all properties' values to return
        /// </param>
        /// <returns>
        /// All public instance property values
        /// </returns>
        public static IEnumerable<object> GetPropertyValues(this object instance)
        {
            Contract.Requires<ArgumentNullException>(instance != null);

            return instance.GetType().GetProperties(
                BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.GetValue(instance, null));
        }


        //This part allows to avoid adding temp using-s for LINQPad namespace
        //TODO: remove a reference to LINQPad from RELEASE unless it provides enough value to users

#if needLinqPadDumper //DEBUG //TODO: comment out from Prod code
        [Conditional("DEBUG"), Obsolete("Remove calls to Dump() from production code")]
        public static void Dump
            (
                this object value,
                string dumptLocation = @"c:\temp\Dump.html"
            )
        {
            Console.WriteLine("Dumped '{0}' object at '{1}'", value, dumptLocation);
            //value.Dump(50);
            
            System.IO.File.WriteAllText(dumptLocation, value.DumpString());
        }

        [Obsolete("Remove calls to Dump() from production code")]
        public static string DumpString(this object value)
        {
            var writer = LINQPad.Util.CreateXhtmlWriter();
            writer.Write(value);
            return writer.ToString();
        }
#endif

        #endregion

        public static string GetProductName(this Assembly assembly)
        {
            Contract.Requires<ArgumentNullException>(assembly != null);

            return ((AssemblyProductAttribute)assembly.GetCustomAttributes(
                    typeof(AssemblyProductAttribute), false)[0])
                .Product;
        }

    }
}
