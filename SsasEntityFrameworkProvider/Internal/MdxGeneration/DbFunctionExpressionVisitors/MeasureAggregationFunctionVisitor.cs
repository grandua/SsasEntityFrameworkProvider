using System;
using System.Data.Entity.Core.Common.CommandTrees;
using AgileDesign.SsasEntityFrameworkProvider.Utilities;
using SqlEntityFrameworkProvider;

namespace AgileDesign.SsasEntityFrameworkProvider.Internal.MdxGeneration.DbFunctionExpressionVisitors
{
    class MeasureAggregationFunctionVisitor
        : FunctionVisitor
    {
        public MeasureAggregationFunctionVisitor(MdxGenerator mdxGenerator)
            : base(mdxGenerator)
        {
        }

        public override ISqlFragment VisitDbFunctionExpression(DbFunctionExpression e)
        {
            string measureNamePartToReplace = SourcePropertyArgument(e).GetHierarchyName(mdxGenerator);
            string replacementName = GetReplacementNameIncludingFunctionName(e, measureNamePartToReplace);
            string sourceMeasureName = SourcePropertyArgument(e).GetMeasureName(MdxGenerator);
            string calculatedMeasureName = sourceMeasureName
                .Replace(measureNamePartToReplace, replacementName);

            string granularityLevelName = GrannularityArgument(e).Value.ToString();
            MdxGenerator.Header.AddCalculatedMember(
                calculatedMeasureName
                , String.Format("{0}({1}, {2})"
                                , GetDbFunctionName(e.Function.Name)
                                , granularityLevelName
                                , sourceMeasureName));

            AddCalculatedMeasure(calculatedMeasureName);
            return new SqlBuilder(); //nothing to add to ON ROWS axis here
        }

        DbConstantExpression GrannularityArgument(DbFunctionExpression e)
        {
            return ((DbConstantExpression)(e.Arguments[1]));
        }
    }
}