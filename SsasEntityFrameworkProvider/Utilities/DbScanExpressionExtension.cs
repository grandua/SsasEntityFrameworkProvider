using System.Data.Entity.Core.Common.CommandTrees;
using AgileDesign.SsasEntityFrameworkProvider.Internal;

namespace AgileDesign.SsasEntityFrameworkProvider.Utilities
{
    static class DbScanExpressionExtension
    {
        public static string GetTableName(this DbScanExpression scanExpression)
        {
            string result = MetadataHelpers.TryGetValueForMetadataProperty<string>(
                scanExpression.Target, "Table");
            
            if (string.IsNullOrWhiteSpace(result))
            {
                result = scanExpression.Target.Name;
            }
            return result;
        }

        public static string GetQuotedTableName(this DbScanExpression scanExpression)
        {
            return scanExpression.GetTableName().InQuoteIdentifier();
        }
    }

}
