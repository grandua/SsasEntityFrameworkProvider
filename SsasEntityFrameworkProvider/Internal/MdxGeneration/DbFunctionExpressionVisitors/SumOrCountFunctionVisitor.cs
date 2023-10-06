using System;
using System.Data.Entity.Core.Common.CommandTrees;
using AgileDesign.SsasEntityFrameworkProvider.Utilities;
using SqlEntityFrameworkProvider;

namespace AgileDesign.SsasEntityFrameworkProvider.Internal.MdxGeneration.DbFunctionExpressionVisitors
{
    /// <summary>
    /// Sum() and Count() do not need granularity dimension level argument
    /// </summary>
    class SumOrCountFunctionVisitor
        : FunctionVisitor
    {
        public SumOrCountFunctionVisitor(MdxGenerator mdxGenerator)
            : base(mdxGenerator)
        {
        }

        public override ISqlFragment VisitDbFunctionExpression(DbFunctionExpression e)
        {
            var sourceProperty = SourcePropertyArgument(e);
            if(e.Function.Name == "Count"
                && ! sourceProperty.IsDimension(mdxGenerator))
            {
                throw CreateMeasureIsNotSupportedForCountArgumentException(sourceProperty);
            }
            string sourcePropertyLevelName = sourceProperty.GetHierarchyName(MdxGenerator);
            string replacementName = GetReplacementNameIncludingFunctionName(e, sourcePropertyLevelName);
            string calculatedMeasureName = String.Format("[Measures]{0}"
                                                         , replacementName);

            string granularityLevelName = sourceProperty.GetMdxColumnName(mdxGenerator);
            MdxGenerator.Header.AddCalculatedMember(
                calculatedMeasureName
                , String.Format("{0}({1})"
                                , GetDbFunctionName(e.Function.Name)
                                , granularityLevelName));

            AddCalculatedMeasure(calculatedMeasureName);
            return new SqlBuilder(); //nothing to add to ON ROWS axis here
        }

        NotSupportedException CreateMeasureIsNotSupportedForCountArgumentException(DbExpression sourceProperty)
        {
            return new NotSupportedException(string.Format(
                "Count() argument property '{0}' is a measure property.\r\n"
                +" Only dimension properties are supported currently.\r\n"
                + "Please, use an appropriate count measure for Count() on measure groups."
                , ((DbPropertyExpression)sourceProperty).Property.Name));
        }
    }
}