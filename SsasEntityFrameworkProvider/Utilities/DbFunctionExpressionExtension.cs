using System.Data.Entity.Core.Common.CommandTrees;

namespace AgileDesign.SsasEntityFrameworkProvider.Utilities
{
    public static class DbFunctionExpressionExtension
    {
        public static bool IsCalculatedMemberFunction(this DbFunctionExpression expression)
        {
            return expression.Function.FullName.StartsWith(
                Mdx.EdmFunctionNamespace + "." + "CalculatedMemberAs");
        }

        public static bool IsSumOrCountFunction(this DbFunctionExpression expression)
        {
            return expression.Function.Name == "Sum"
                || expression.Function.Name == "Count"
                || expression.Function.Name == "LongCount";
        }
    }
}