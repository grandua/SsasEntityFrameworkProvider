using System;
using System.Data.Entity.Core.Common.CommandTrees;
using SqlEntityFrameworkProvider;

namespace AgileDesign.SsasEntityFrameworkProvider.Internal.MdxGeneration.DbFunctionExpressionVisitors
{
    abstract class FunctionVisitor
    {
        protected MdxGenerator mdxGenerator;

        protected FunctionVisitor(MdxGenerator mdxGenerator)
        {
            this.mdxGenerator = mdxGenerator;
        }

        protected MdxGenerator MdxGenerator
        {
            get { return mdxGenerator; }
        }

        protected string GetDbFunctionName(string edmFunctionName)
        {
            switch (edmFunctionName)
            {
                case "IndexOf" :
                    return "INSTR";
                case "ToLower":
                    return "LCASE";
                case "ToUpper":
                    return "UCASE";
                case "Average":
                    return "AVG";
                case "LongCount":
                    return "COUNT";
                case "Sum":
                case "Max":
                case "Min":
                case "Count":
                    return edmFunctionName.ToUpper();
                case "CurrentDateTime" :
                    return "NOW";
                default:
                    throw new NotSupportedException(String.Format(
                        "EDM function '{0}' is not supported!"
                        , edmFunctionName));
            }
        }

        protected string GetReplacementNameIncludingFunctionName(DbFunctionExpression e,
                                                                 string mdxNamePartToReplace)
        {
            string mdxNamePartToReplaceWithoutBrackets
                = mdxNamePartToReplace
                    .Replace(".[", "").Replace("]", "");

            return String.Format(".[{0} {1}]"
                                 , e.Function.Name
                                 , mdxNamePartToReplaceWithoutBrackets);
        }

        protected DbExpression SourcePropertyArgument(DbFunctionExpression e)
        {
            return e.Arguments[0];
        }

        public abstract ISqlFragment VisitDbFunctionExpression(DbFunctionExpression e);

        protected void AddCalculatedMeasure(string calculatedMeasureName)
        {
            MdxGenerator.ColumnsAxis.AddCnMeasure(calculatedMeasureName);
        }
    }
}