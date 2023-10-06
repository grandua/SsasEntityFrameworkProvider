using System.Collections.Generic;
using System.Text;
using AgileDesign.Utilities;
using SqlEntityFrameworkProvider;

namespace AgileDesign.SsasEntityFrameworkProvider.Internal.MdxGeneration
{
    class MdxSelectStatement
        : SqlSelectStatement
    {
        MdxGenerator mdxGenerator;
        public const string OnRowsPlaceholder = "<OnRowsAxisExpression>";

        public MdxSelectStatement(MdxGenerator mdxGenerator = null)
        {
            this.mdxGenerator = mdxGenerator;
        }

        CalculatedMembersBuilder header;
        public CalculatedMembersBuilder Header
        {
            get { return Init.InitIfNull(ref header); }
        }

        ColumnsAxisBuilder columnsAxis;
        internal ColumnsAxisBuilder ColumnsAxis
        {
            get
            {
                return columnsAxis 
                    ?? ( columnsAxis = new ColumnsAxisBuilder(this) );
            }
        }

        public RowsAxisBuilder RowsAxis
        {
            get { return Select; }
        }

        protected override SqlBuilder CreateSelectStatement()
        {
            return new RowsAxisBuilder();
        }
        /// <remarks>
        ///Select is replaced with RowsAxis, hiding an inherited Select violates LSP though 
        ///but it is too much code to replace inheritance with delegation, 
        ///and it is not that important in this case
        /// </remarks>
        private new RowsAxisBuilder Select
        {
            get { return (RowsAxisBuilder)base.Select; }
        }

        ColumnOrderTracker columnOrder;
        internal ColumnOrderTracker ColumnOrder
        {
            get { return Init.InitIfNull(ref columnOrder); }
            set { columnOrder = value; }
        }

        public IDictionary<int, int> LinqToMdxColumnsOrder
        {
            get { return ColumnOrder.LinqToMdxColumnsOrder; }
        }

        public int SkipCount { get; set; }

        SqlBuilder whereAxis;
        public SqlBuilder WhereAxis
        {
            get
            {
                return whereAxis 
                    ?? ( whereAxis = new SqlBuilder() );
            }
        }

        public bool IsEmpty
        {
            get
            {
                return ColumnsAxis.IsEmpty
                       && Where.IsEmpty
                       && Header.IsEmpty
                       && RowsAxis.IsEmpty
                       && WhereAxis.IsEmpty;
            }
        }

        public void OnColumnAdded()
        {
            ColumnOrder.UpdateColumnsAxisColumnOrder();
        }

        public void OnRowAdded()
        {
            ColumnOrder.UpdateRowsAxisColumnOrder();
        }

        /// <summary>
        /// EF uses "1 as C1" (Constant1?) in its SQL queries 
        /// so we need to provide it back because it is expected in the result recordset
        /// </summary>
        public const string C1 = "[Measures].[C1]";

        public void MergeWhereClause(MdxSelectStatement other)
        { 
            if(other.Where.IsEmpty)
            {
                return;
            }
            if( ! Where.IsEmpty)
            {
                Where.Append(" AND ");
            }
            Where.Append(other.Where);
        }

        public void MergeWithOuterSelectStatement(MdxSelectStatement outerSelectStatementWithCn)
        {
            //base.Visit(e)).ColumnsAxis has Measures.C1 instead of '<name_p__linq__...>'
            //but result.ColumnsAxis has a wrong column order of an internal DbProjectionExpression.
            //We can have multiple different <name_p> and Cn.
            //Cn in 2nd outer select corresponds to DbFunction(n) in 1st inner select
            //Solution:
            //Put Cx before <name_p__> and replace Cx from 2nd outer pass with Cx<name_p>

            //We use set expressions only in rows axis,
            //so no column replacement is needed on rows axis so far.
            //Outer select statement has correct column order in its row axis, and we just take it as is.
            RowsAxis.ReplaceSets(outerSelectStatementWithCn.RowsAxis);

            //TODO: Remove hardcoded "1" for C1
            ColumnsAxis.Set(
                outerSelectStatementWithCn.ColumnsAxis
                    .ReplaceCnWithNameParameters(ColumnsAxis));
        }


        protected override void DoWriteSql(SqlGenerator sqlGenerator, SqlWriter writer)
        {
            var resultMdxSelectStatement = new SqlStatementToMdxMapper(this).MapSqlToMdx();
            resultMdxSelectStatement.WriteSqlNoRemapping(sqlGenerator, writer);
        }

        void WriteSqlNoRemapping(SqlGenerator sqlGenerator,
                                 SqlWriter writer)
        {
            Header.WriteSql(writer, sqlGenerator);
            writer.Write("SELECT ");
            WriteDistinct(writer);
            WriteTop(sqlGenerator, writer);
            WriteSelectColumns(sqlGenerator, writer);
            ColumnsAxis.WriteSql(writer, sqlGenerator);
            WriteFrom(sqlGenerator, writer);
            WriteWhere(sqlGenerator, writer);
            WriteOrderBy(sqlGenerator, writer);
        }

        public override string ToString()
        {
            if (mdxGenerator == null)
            {
                return ToStringBase();
            }
            return ToStringViaWriteSql();
        }

        string ToStringBase()
        {
            var result = new SqlStatementToMdxMapper(this).MapSqlToMdx();
            //prevent infinite recursion by calling method with a different name
            return result.ToStringInternal();
        }

        string ToStringViaWriteSql()
        {
            var result = new StringBuilder();
            using (var writer = new SqlWriter(result))
            {
                WriteSql(writer, mdxGenerator);
            }
            return result.ToString(); //writer.ToString() causes StackOverflow as well
        }

        string ToStringInternal()
        {
            return base.ToString();
        }
    }
}