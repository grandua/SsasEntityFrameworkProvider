using System;
using System.Data.Entity.Core.Objects.DataClasses;
using AgileDesign.Utilities;

namespace AgileDesign.SsasEntityFrameworkProvider
{
    public static class Mdx
    {
        internal const string EdmFunctionNamespace = "AgileDesign.SsasEntityFrameworkProvider.AdomdClient";

        static readonly object namingConventionLocker = new object();

        private static IMdxNamingConvention namingConvention;
        /// <summary>
        /// This property is thread safe.
        /// </summary>
        public static IMdxNamingConvention NamingConvention
        {
            get
            {
                lock (namingConventionLocker)
                {
                    return namingConvention;
                }
            }
            set
            {
                lock (namingConventionLocker)
                {
                    namingConvention = value;
                }
            }
        }

        public static string EdmFunctionFullName_Member()
        {
            return string.Format("{0}.{1}"
                                 , EdmFunctionNamespace
                                 , NameOf.Method(() => Member(null, null)));
        }

        /// <param name="mdxSetSpecification">
        /// MDX expression that returns a set that defines granularity of an aggregation
        /// </param>
        [EdmFunction(EdmFunctionNamespace, "Max")]
        public static T Max<T>
            (
                T sourceProperty 
                , string mdxSetSpecification
            )
        {
            throw new NotSupportedException("This method is a stub and it is never executed.");
        }

#if IFiguredOutHowToGetTableNameFromEntityMetadata
        /// <summary>
        /// Get MDX measure name from measure property
        /// or dimension + hierarchy + level name from dimension property. <br/>
        /// DimensionColumnName is not EdmFunction, it is executed in .NET process.
        /// </summary>
        /// <param name="sourceEntity"></param>
        /// <param name="sourcePropertyExpression"></param>
        /// <param name="dbContext"></param>
        /// <param name="sourceProperty">
        /// Entity measure or dimension property that maps to MDX column name</param>
        /// <returns>
        /// Full MDX column name 
        /// (dimension + hierarchy + level name for dimensional properties
        /// and [Measures] + measure name for measure properties).
        /// </returns>
        public static string DimensionColumnName<TEntity>
            (this TEntity sourceEntity,
             Expression<Func<object>> sourcePropertyExpression,
             DbContext dbContext)
        {
            return DimensionColumnName<TEntity>(dbContext, sourcePropertyExpression);
        }

        public static string DimensionColumnName<TEntity>
            (
                this DbContext dbContext
                , Expression<Func<object>> sourcePropertyExpression
            )
        {
            Contract.Requires<ArgumentNullException>(dbContext != null);
            Contract.Requires<ArgumentNullException>(sourcePropertyExpression != null);

            var entityType = GetEntityOMetadata<TEntity>(dbContext);
            string entityName = "TODO"; //(string)(GetEntityCSMetadata<TEntity>(dbContext)).MetadataProperties["Table"].Value;

            string sqlColumnName = entityType
                .Members[NameOf.Member(sourcePropertyExpression)].Name;

            //TODO: give a way to choose naming convention automatically based on configuration or context property value
            var namingConvention = new AddSpacesToCamelCasingWordsConvention();
            return string.Format("{0}.{1}"
                , entityName
                , namingConvention.GetHierarchyAndColumnName(sqlColumnName));
        }

        static EntityType GetEntityOMetadata<TEntity>(DbContext dbContext)
        {
            return ((IObjectContextAdapter)dbContext)
                .ObjectContext.MetadataWorkspace
                .GetItem<EntityType>(typeof(TEntity).FullName, DataSpace.OSpace);
        }
#endif

        /// <param name="mdxSetSpecification">
        /// MDX expression that returns a set that defines granularity of an aggregation
        /// </param>
        [EdmFunction(EdmFunctionNamespace, "Min")]
        public static T Min<T>
            (
                T sourceProperty
                , string mdxSetSpecification
            )
        {
            throw NeverExecutedException();
        }

        [EdmFunction(EdmFunctionNamespace, "Sum")]
        public static T Sum<T>(T sourceProperty)
        {
            throw NeverExecutedException();
        }

#if fixed //TODO: Count() may cause EF to add Count() argument as a dimension into no dimension MDX
        [EdmFunction(EdmFunctionNamespace, "Count")]
        public static int Count(object sourceProperty)
        {
            throw NeverExecutedException();
        }

        [EdmFunction(EdmFunctionNamespace, "LongCount")]
        public static long LongCount(object sourceProperty)
        {
            throw NeverExecutedException();
        }
#endif

        static NotSupportedException NeverExecutedException()
        {
            return new NotSupportedException("This method is a stub and it is never executed.");
        }

        [EdmFunction(EdmFunctionNamespace, "Average")]
        public static T Average<T>
            (
                    T sourceProperty
                    , string mdxSetSpecification
            )
        {
            throw new NotSupportedException("This method is a stub and it is never executed.");
        }

        [EdmFunction(EdmFunctionNamespace, "Member")]
        public static bool Member
            (
                this object memberProperty
                , string shortMemberName
            )
        {
            Contract.Requires<ArgumentNullException>(memberProperty != null);
            Contract.Requires<ArgumentNullException>(shortMemberName != null);
            Contract.Requires<ArgumentException>(string.IsNullOrWhiteSpace(shortMemberName));

            return (memberProperty.ToString() == shortMemberName);
        }

#if RangeIsImplmented
        public static string EdmFunctionFullName_Range()
        {
            return string.Format("{0}.{1}"
                                 , EdmFunctionNamespace
                                 , NameOf.Method(() => Range(null, null, null)));
        }

        [EdmFunction(EdmFunctionNamespace, "Range")]
        public static bool Range
            (
                this object memberProperty
                , string fromShortMemberName
                , string toShortMemberName
            )
        {
            Contract.Requires<ArgumentNullException>(memberProperty != null);
            Contract.Requires<ArgumentNullException>(fromShortMemberName != null);
            Contract.Requires<ArgumentException>(string.IsNullOrWhiteSpace(fromShortMemberName));
            Contract.Requires<ArgumentNullException>(toShortMemberName != null);
            Contract.Requires<ArgumentException>(string.IsNullOrWhiteSpace(toShortMemberName));

            return (memberProperty.ToString() == fromShortMemberName);
        }
#endif

        [EdmFunction(EdmFunctionNamespace, "CalculatedMemberAsString")]
        public static string CalculatedMemberAsString
            (
                string memberName
                , string expression
            )
        {
            return expression;
        }

        [EdmFunction(EdmFunctionNamespace, "CalculatedMemberAsInt")]
        public static int? CalculatedMemberAsInt
            (
                string memberName
                , string expression
            )
        {
            throw new NotSupportedException("This method is a stub and it is never executed.");
        }

        [EdmFunction(EdmFunctionNamespace, "CalculatedMemberAsDouble")]
        public static double? CalculatedMemberAsDouble
            (
                string memberName
                , string expression
            )
        {
            throw new NotSupportedException("This method is a stub and it is never executed.");
        }

        [EdmFunction(EdmFunctionNamespace, "CalculatedMemberAsFloat")]
        public static float? CalculatedMemberAsFloat
            (
                string memberName
                , string expression
            )
        {
            throw new NotSupportedException("This method is a stub and it is never executed.");
        }

        [EdmFunction(EdmFunctionNamespace, "CalculatedMemberAsDateTime")]
        public static DateTime? CalculatedMemberAsDateTime
            (
                string memberName
                , string expression
            )
        {
            throw new NotSupportedException("This method is a stub and it is never executed.");
        }

    }
}
