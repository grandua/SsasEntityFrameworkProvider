using System.Data.Entity.Core.Common.CommandTrees;
using System.Data.Entity.Core.Metadata.Edm;

namespace SqlEntityFrameworkProvider.Utilities
{
    static class DbExpressionExtension
    {
        internal static ReadOnlyMetadataCollection<EdmProperty> GetMetaProperties(this DbExpression e)
        {
            return e.GetRowType().Properties;
        }

        static RowType GetRowType(this DbExpression e)
        {
            return MetadataHelpers.GetEdmType<RowType>
                (MetadataHelpers.GetEdmType<CollectionType>(e.ResultType).TypeUsage);
        }

    }
}
