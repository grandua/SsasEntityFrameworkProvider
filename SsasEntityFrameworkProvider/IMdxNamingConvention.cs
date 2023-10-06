namespace AgileDesign.SsasEntityFrameworkProvider
{
    public interface IMdxNamingConvention
    {
        string GetHierarchyAndColumnName(string sqlColumnName);
        string GetMdxName(string sqlName);
    }
}