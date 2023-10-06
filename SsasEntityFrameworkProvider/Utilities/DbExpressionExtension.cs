using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Common.CommandTrees;
using System.Data.Entity.Core.Metadata.Edm;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AgileDesign.SsasEntityFrameworkProvider.AdomdClient;
using AgileDesign.SsasEntityFrameworkProvider.Attributes;
using AgileDesign.SsasEntityFrameworkProvider.Internal.MdxGeneration;
using AgileDesign.Utilities;
using SqlEntityFrameworkProvider;

namespace AgileDesign.SsasEntityFrameworkProvider.Utilities
{
    static class DbExpressionExtension
    {
        static bool IsColumnGroupingExpression(this DbExpression columnExpression)
        {
            return columnExpression is DbPropertyExpression
                && (columnExpression as DbPropertyExpression).Instance is DbVariableReferenceExpression
                && Regex.IsMatch(
                ((DbVariableReferenceExpression)(((DbPropertyExpression)
                    (columnExpression)).Instance)).VariableName, @"GroupBy\d+$");
        }
        
        public static bool IsConstantPropertyExpression(this DbExpression columnExpression)
        {
            return (columnExpression is DbPropertyExpression
                    && IsConstantName(((DbPropertyExpression)columnExpression).Property.Name));
        }
        static bool IsConstantName(string propertyName)
        {
            return Regex.IsMatch(propertyName, @"C\d{1,3}$");
        }

        public static string GetSqlColumnName
            (
                this DbExpression columnExpression
                , MdxGenerator mdxGenerator
            )
        {
            return columnExpression.Accept(mdxGenerator).ToString();
        }

        public static string GetMeasureName
            (
                this DbExpression columnExpression
                , MdxGenerator mdxGenerator
            )
        {
            return ColumnsAxisBuilder.MeasuresPrefix
                + columnExpression.GetMeasureNameWithoutMeasuresPrefix(mdxGenerator);
        }

        public static string GetMeasureNameWithoutMeasuresPrefix
            (
                this DbExpression columnExpression
                , MdxGenerator mdxGenerator
            )
        {
            return mdxGenerator.NamingConvention.GetMdxName(
                columnExpression.Accept(mdxGenerator).ToString());
        }

        public static bool IsEntityADimension
            (
                this DbExpression columnExpression
                , MdxGenerator mdxGenerator
            )
        {
            return !columnExpression.IsEntityAMeasureGroup(mdxGenerator);
        }

        public static bool IsEntityAMeasureGroup
            (
                this DbExpression columnExpression
                , MdxGenerator mdxGenerator
            )
        {
            if (columnExpression is DbFunctionExpression)
            { //TODO: comment out and see if anything breaks
                return false;
            }
            return columnExpression.GetEntityName(mdxGenerator)
                .HasTypeLevelAttribute<MeasureGroupAttribute>();
        }

        static bool IsDimensionColumnOfMeasureGroupEntity
            (
                this DbExpression columnExpression
                , MdxGenerator mdxGenerator
            )
        {
            Contract.Requires(columnExpression is DbPropertyExpression);

            return columnExpression.GetEntityName(mdxGenerator)
                .HasPropertyAttribute<DimensionPropertyAttribute>(
                    ((DbPropertyExpression)columnExpression)
                    .Property.Name);
        }

        public static bool IsDimension
            (
                this DbExpression columnExpression
                , MdxGenerator mdxGenerator
            )
        {
            if (columnExpression.IsColumnGroupingExpression())
            {
                return false;
            }
            return columnExpression is DbFunctionExpression //TODO: should it be NOT is DbFunctionExpression?
                || (columnExpression.IsEntityADimension(mdxGenerator))
                || columnExpression.IsDimensionColumnOfMeasureGroupEntity(mdxGenerator);
        }

        static string GetDimensionFromAttribute
            (
                this DbExpression columnExpression
                , MdxGenerator mdxGenerator
            )
        {
            return columnExpression.GetEntityName(mdxGenerator)
                .GetNameFromAttribute<DimensionPropertyAttribute>(
                    columnExpression.GetPropertyName());
        }

        public static SqlBuilder GetDimensionName
            (
                this DbExpression columnExpression
                , MdxGenerator mdxGenerator
            )
        {
            var result = new SqlBuilder();
            if (columnExpression.IsEntityAMeasureGroup(mdxGenerator))
            {
                AddDimensionName(
                    result, columnExpression.GetDimensionFromAttribute(mdxGenerator), mdxGenerator);
            }
            else
            {
                AddDimensionName(result, columnExpression, mdxGenerator);
            }
            return result;
        }

        static void AddDimensionName
            (
                SqlBuilder result
                , DbExpression columnExpression
                , MdxGenerator mdxGenerator
            )
        {
            result.Append(mdxGenerator.NamingConvention.GetMdxName(
                mdxGenerator.GetTableNameFromDbExpression(columnExpression)));
        }

        static void AddDimensionName
            (
                SqlBuilder result,
                string dimensionName,
                MdxGenerator mdxGenerator
            )
        {
            result.Append(mdxGenerator.NamingConvention.GetMdxName(
                dimensionName).InQuoteIdentifier());
        }

        public static string GetEntityName(this DbExpression columnExpression)
        {
            return ((DbPropertyExpression)columnExpression).Property.DeclaringType.Name;
        }

        public static string GetHierarchyAndLevelName
            (
                this DbExpression columnExpression
                , MdxGenerator mdxGenerator
            )
        {
            return mdxGenerator.NamingConvention.GetHierarchyAndColumnName(
                columnExpression.GetSqlColumnName(mdxGenerator));
        }
        public static string GetHierarchyName
            (
                this DbExpression columnExpression
                , MdxGenerator mdxGenerator
            )
        {
            return mdxGenerator.NamingConvention.GetMdxName(
                columnExpression.GetSqlColumnName(mdxGenerator));
        }

        public static SqlBuilder GetMdxSortKey
            (
                this DbExpression columnExpression
                , MdxGenerator mdxGenerator
            )
        {
            var result = new SqlBuilder();
            if (columnExpression.IsDimension(mdxGenerator))
            {
                result.Append(columnExpression.GetDimensionName(mdxGenerator));
                result.Append(columnExpression.GetHierarchyName(mdxGenerator));
                result.Append(".MemberValue");
            }
            else
            { //TODO: cover with test
                result.Append(columnExpression.GetMeasureName(mdxGenerator));
            }
            return result;
        }

        public static string GetMdxColumnName
            (
                this DbExpression columnExpression
                , MdxGenerator mdxGenerator
            )
        {
            if (!columnExpression.IsDimension(mdxGenerator))
            {
                return columnExpression.GetMeasureName(mdxGenerator);
            }

            SqlBuilder result = columnExpression.GetDimensionName(mdxGenerator);
            result.Append(columnExpression.GetHierarchyAndLevelName(mdxGenerator));
            return result.ToString();
        }

        static string ParameterName(this DbExpression parameterExpression)
        {
            Contract.Requires<ArgumentNullException>(parameterExpression != null);
            Contract.Requires<ArgumentException>(parameterExpression is DbParameterReferenceExpression);

            return ((DbParameterReferenceExpression)parameterExpression).ParameterName;
        }

        public static string GetParameterNamedPlaceholder(this DbExpression parameterExpression)
        {
            return string.Format("<{0}>"
                , parameterExpression.ParameterName());
        }

		/// <summary>
        /// Name parameters represent MDX names and should not be in quotes.<br/>
        /// ..ForMeasures overload uses Cn prefix to allow matching these parameters 
        /// to Cn positions in outer select statement.
        /// </summary>
        public static string GetNameParameterNamedPlaceholderForMeasures(this DbExpression parameterExpression)
		{
            return string.Format("{0}{1}>"
                , SsasParameter.NameParamerterPrefix
                , parameterExpression.ParameterName());
        }
        /// <summary>
        /// Name parameters represent MDX names and should not be in quotes.<br/>
        /// ..ForHeader does not need Cn prefixes because we do not care about positions of calculated members.
        /// </summary>
        public static string GetNameParameterNamedPlaceholderForHeader(this DbExpression parameterExpression)
        {
            return string.Format("{0}{1}>"
                , SsasParameter.NameParamerterPrefix
                , parameterExpression.ParameterName());
        }

        static string GetPropertyName(this DbExpression propertyExpression)
        {
            Contract.Requires(propertyExpression is DbPropertyExpression);

            return ((DbPropertyExpression)propertyExpression).Property.Name;
        }


        public static IEnumerable<DbExpression> ToDbExpressionTreeCollection(
            this DbExpression dbExpression)
        {
            return dbExpression.ToDbExpressionTreeCollectionInternal().ToArray()
                .OfType<DbExpression>();
        }

        static IEnumerable<object> ToDbExpressionTreeCollectionInternal(this object dbExpression)
        {
            var propertyValues = dbExpression.GetPropertyValues();
            if(propertyValues == null)
            {
                return new object[] {};
            }
            var result = GetDbExpressions(propertyValues);
            foreach (var expression in result)
            {
                result = result.Concat(expression.ToDbExpressionTreeCollectionInternal()); //recursion
            }
            return result.Concat(propertyValues.GetCollectionPropertyExpressions());
        }

        static IEnumerable<object> GetCollectionPropertyExpressions(
            this IEnumerable<object> propertyValues)
        {
            return from listProperty in propertyValues.OfType<IEnumerable<object>>()
                   from element in listProperty
                   where (element is DbExpression)
                    || (element is DbExpressionBinding)
                   select element;
        }

        static IEnumerable<object> GetDbExpressions(IEnumerable<object> propertyValues)
        {
            IEnumerable<object> result = propertyValues.OfType<DbExpression>();
            return result.Concat(propertyValues.OfType<DbExpressionBinding>());
        }

        /// <param name="columnExpression">
        /// Must be of DbPropertyExpression type
        /// </param>
        /// <param name="mdxGenerator">
        /// This parameter is needed if the method is used by a query with OrderBy
        /// </param>
        public static string GetEntityName
            (
                this DbExpression columnExpression
                , MdxGenerator mdxGenerator
            )
        {
            Contract.Requires(columnExpression is DbPropertyExpression);

            var property = ((DbPropertyExpression)columnExpression).Property;
            if (property.DeclaringType.GetType() == typeof(RowType))
            { //RowType does not contain entity name, get it from current DbScanExpression
                return GetEntityName(property.Name, mdxGenerator.ExpressionTreeList);
            }
            return property.DeclaringType.Name;
        }

        static string GetEntityName
            (
                string propertyName,
                IEnumerable<DbExpression> expressionTreeList
            )
        {
            //TODO: Figure out how to avoid a bug if different entities have properties with the same name
            var values
                = from propertyExpression in expressionTreeList.OfType<DbPropertyExpression>()
                  where propertyExpression.Property.DeclaringType.GetType() != typeof(RowType)
                        && propertyExpression.Property.Name == propertyName
                  select propertyExpression.Property.DeclaringType.Name;

            return values.First();
        }

        public static string ToOperatorString(this DbExpressionKind expressionKind)
        {
            switch (expressionKind)
            {
                case DbExpressionKind.Equals :
                    return " = ";
                case DbExpressionKind.NotEquals :
                    return " <> ";
                case DbExpressionKind.GreaterThan:
                    return " > ";
                case DbExpressionKind.GreaterThanOrEquals:
                    return " >= ";
                case DbExpressionKind.LessThan:
                    return " < ";
                case DbExpressionKind.LessThanOrEquals:
                    return " <= ";
                default:
                    throw CreateNotSupportedOperatorException(expressionKind);
            }
        }

        static NotSupportedException CreateNotSupportedOperatorException(DbExpressionKind expressionKind)
        {
            return new NotSupportedException(string.Format(
                "Comparison operator '{0}' is not supported", expressionKind));
        }


        #region LicensingProtection

        internal static byte[] GetPublicKeyToken()
        {
            return Assembly.GetExecutingAssembly().GetName().GetPublicKeyToken();
        }

        internal static string GetPublicKey(this Assembly assembly)
        {
            return assembly.GetName().GetPublicKey().ConvertToString();
        }

        #endregion //LicensingProtection

    }
}
