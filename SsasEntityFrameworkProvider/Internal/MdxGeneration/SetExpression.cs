using SqlEntityFrameworkProvider;

namespace AgileDesign.SsasEntityFrameworkProvider.Internal.MdxGeneration
{
    class SetExpression
    : RowsAxisBuilder
    {
        public SetExpression(ISqlFragment setExpressionFragment)
        {
            Append(setExpressionFragment.ToString());
        }
    }

}
