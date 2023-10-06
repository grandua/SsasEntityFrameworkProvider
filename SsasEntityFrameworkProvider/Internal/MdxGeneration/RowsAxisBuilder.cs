using System.Data.Entity.Core.Common.CommandTrees;
using AgileDesign.SsasEntityFrameworkProvider.Utilities;
using SqlEntityFrameworkProvider;

namespace AgileDesign.SsasEntityFrameworkProvider.Internal.MdxGeneration
{
    class RowsAxisBuilder 
        : SqlBuilder
    {
        public void AddDimension
            (
            DbExpression columnExpression
            , MdxGenerator mdxGenerator
            )
        {
            Append(columnExpression.GetMdxColumnName(mdxGenerator));
            mdxGenerator.OnRowAdded();
        }

        public void ReplaceSets(RowsAxisBuilder prototypeSet)
        {
            if (IsSingleMemberSet(prototypeSet))
            {
                return;
            }
            bool isSetFound = false;
            foreach(var setExpression in GetFragmentFlatList<SetExpression>())
            {
                setExpression.Set(prototypeSet.ToString());
                isSetFound = true;
            }
            if( ! isSetFound)
            { //all content is 1 single set, replace it all
                Set(prototypeSet.ToString());
            }
        }

        bool IsSingleMemberSet(RowsAxisBuilder prototypeSet)
        {
            return ! prototypeSet.ToString().Contains(",");
        }
    }
}