namespace SqlEntityFrameworkProvider
{
    internal class FromClause
        : SqlBuilder
    {
        public bool HasSubquery()
        {
            return ToString().Contains("(");
        }
    }
}