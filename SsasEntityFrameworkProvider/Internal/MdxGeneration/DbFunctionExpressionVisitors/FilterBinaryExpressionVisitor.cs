using System;
using System.Data.Entity.Core.Common.CommandTrees;
using AgileDesign.SsasEntityFrameworkProvider.Utilities;
using SqlEntityFrameworkProvider;

namespace AgileDesign.SsasEntityFrameworkProvider.Internal.MdxGeneration.DbFunctionExpressionVisitors
{
    class FilterBinaryExpressionVisitor
    {
        readonly MdxGenerator mdxGenerator;

        public FilterBinaryExpressionVisitor(MdxGenerator mdxGenerator)
        {
            this.mdxGenerator = mdxGenerator;
        }

        public ISqlFragment CreateFilterExpression(DbBinaryExpression comparisonExpression)
        {
            return CreateFilterExpression(comparisonExpression, comparisonExpression.ExpressionKind);
        }

        public ISqlFragment CreateFilterExpression
            (
            DbBinaryExpression comparisonExpression,
            DbExpressionKind expressionKind
            )
        {
            var result = new SqlBuilder();
            if (comparisonExpression.HasDbFunctionExpression())
            {
                result.Append(CreateFilterExpressionFromFunction(comparisonExpression));
                AddComparisonAndValue(result, expressionKind, comparisonExpression);
            }
            else if (comparisonExpression.HasDbPropertyExpression())
            {
                result.Append(comparisonExpression.GetMdxSortKey(mdxGenerator));
                AddComparisonAndValue(result, expressionKind, comparisonExpression);
            }
            else
            {
                result.Append(comparisonExpression.Left.Accept(mdxGenerator));
                result.Append(expressionKind.ToOperatorString());
                result.Append(comparisonExpression.Right.Accept(mdxGenerator));
            }
            return result;
        }

        void AddComparisonAndValue(SqlBuilder result,
                                   DbExpressionKind comparisonExpressionKind,
                                   DbBinaryExpression valueComparisonExpression)
        {
            result.Append(comparisonExpressionKind.ToOperatorString());
            try
            {
                result.Append(valueComparisonExpression.GetValue().Enquote());
            }
            catch (NotSupportedException)
            {
                if (valueComparisonExpression.Right is DbPropertyExpression)
                {
                    result.Append(valueComparisonExpression.Left.Accept(mdxGenerator));
                }
                else
                {
                    result.Append(valueComparisonExpression.Right.Accept(mdxGenerator));
                }
            }
        }

        ISqlFragment CreateFilterExpressionFromFunction(DbBinaryExpression comparisonExpression)
        {
            var result = new SqlBuilder();
            result.Append(comparisonExpression.Left.Accept(mdxGenerator));
            return result;
        }
    }
}