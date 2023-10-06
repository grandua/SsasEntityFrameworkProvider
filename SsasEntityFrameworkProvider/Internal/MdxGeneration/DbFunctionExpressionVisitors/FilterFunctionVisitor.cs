using System.Collections.Generic;
using System.Data.Entity.Core.Common.CommandTrees;
using System.Linq;
using AgileDesign.SsasEntityFrameworkProvider.Utilities;
using SqlEntityFrameworkProvider;

namespace AgileDesign.SsasEntityFrameworkProvider.Internal.MdxGeneration.DbFunctionExpressionVisitors
{
    class FilterFunctionVisitor
        : FunctionVisitor
    {
        internal FilterFunctionVisitor(MdxGenerator mdxGenerator)
            : base(mdxGenerator)
        {
        }

        public override ISqlFragment VisitDbFunctionExpression(DbFunctionExpression e)
        {
            if (e.IsCalculatedMemberFunction())
            {
                return VisitCalculatedMemberFunction(e);
            }
            var result = new SqlBuilder();
            result.Append(GetDbFunctionName(e.Function.Name));
            result.Append("(");
            IEnumerable<DbExpression> orderedArgs = e.Arguments;
            if (e.Function.Name == "IndexOf") //TODO: extract to IndexOfVisitor
            {
                orderedArgs = e.Arguments.Reverse();
            }
            AddMdxColumnNamesForArguments(result, orderedArgs);
            result.Append(")");
            return result;
        }

        ISqlFragment VisitCalculatedMemberFunction(DbFunctionExpression e)
        {
            MdxGenerator.Header.AddCalculatedMember(e);
            var calculatedMembersBuilder = new CalculatedMembersBuilder();
            var result = new SqlBuilder();
            result.Append(calculatedMembersBuilder.GetCalculatedMemberName(e));
            return result;
        }

        void AddMdxColumnNamesForArguments(SqlBuilder result,
                                           IEnumerable<DbExpression> orderedArgs)
        {
            string separator = "";
            foreach (var argument in orderedArgs)
            {
                result.Append(separator);
                if (argument is DbPropertyExpression)
                {
                    result.Append(GetMdxColumnNameForFilter(argument));
                }
                else
                {
                    result.Append(argument.Accept(MdxGenerator));
                }
                separator = ", ";
            }
        }

        ISqlFragment GetMdxColumnNameForFilter(DbExpression columnExpression)
        {
            var result = new SqlBuilder();
            if (columnExpression.IsDimension(MdxGenerator))
            {
                GetMdxColumnNameForFilter(columnExpression, result);
            }
            else
            {
                result.AppendLine("FORMAT");
                result.AppendLine("(");
                result.AppendLine(columnExpression.GetMeasureName(MdxGenerator));
                result.Append(")");
            }
            return result;
        }

        void GetMdxColumnNameForFilter(DbExpression columnExpression,
                                       SqlBuilder result)
        {
            result.Append(columnExpression.GetDimensionName(MdxGenerator));
            result.Append(columnExpression.GetHierarchyName(MdxGenerator));
            result.Append(".CurrentMember.Name");
        }
    }
}