using System;
using System.Reflection;
using AgileDesign.SsasEntityFrameworkProvider.Utilities;
using AgileDesign.Utilities;
using SqlEntityFrameworkProvider;

namespace AgileDesign.SsasEntityFrameworkProvider.Internal.MdxGeneration
{
    class SqlStatementToMdxMapper
    { //TODO: reduce or remove this class by moving its fragments to ClauseBuilders WriteSql() or ToString()
        MdxSelectStatement source;
        readonly MdxSelectStatement destination = new MdxSelectStatement();

        public SqlStatementToMdxMapper()
        {
        }

        public SqlStatementToMdxMapper(SqlSelectStatement source)
        {
            Contract.Requires(source is MdxSelectStatement);

            this.source = (MdxSelectStatement)source;
        }

        public MdxSelectStatement MapSqlToMdx()
        {
            AddDummyMemberHeader();
            MapCalculatedMembers();
            MapRowsAndColumnsProjection();
            MapWhereAxis();
            MapFromClause();

            return destination;
        }

        void MapWhereAxis()
        {
            if(source.WhereAxis.IsEmpty)
            {
                return;
            }
            destination.Where.AppendLine();
            destination.Where.AppendLine("{");
            destination.Where.AppendLine(source.WhereAxis);
            destination.Where.AppendLine("}");
        }

        void MapCalculatedMembers()
        {
            if(source.Header.IsEmpty)
            {
                return;
            }
            //TODO: move to Header.WriteSql() or Header.ToString()
            if (destination.Header.IsEmpty)
            {
                destination.Header.Append("WITH ");
            }
            destination.Header.Append(source.Header);
        }

        void AddDummyMemberHeader()
        { 
            if (! source.ColumnsAxis.IsDummyMemberRequired())
            {
                return;
            }
            //TODO: move to Header.WriteSql() or Header.ToString()
            if (destination.Header.IsEmpty)
            {
                destination.Header.Append("WITH ");
            }
            destination.Header.Append("MEMBER ");
            destination.Header.AppendLine(MdxSelectStatement.C1);
            destination.Header.AppendLine("\tAS 1");
        }

        void MapFromClause()
        {
            if( source.OrderBy.IsEmpty == false
                || ! source.From.HasSubquery())
            { //It is a hack, EF creates a redundant subquery for all queries with orderby, 
                //it will be a bug if we really need a subquery in a query with orderby
                destination.From.Append(SsasProviderManifest.CubeNamePlaceholder);
                return;
            }
            destination.From.Append(source.From);
        }

        void MapRowsAndColumnsProjection()
        {
            //TODO: move to RowsAxis.WriteSql() or RowsAxis.ToString()
            destination.RowsAxis.AppendLine();
            destination.RowsAxis.Append
            (
                WithOnRows(OnRowsExpression)
            );
            destination.ColumnsAxis.Append(GetOnColumns());
        }

        public SqlBuilder OnRowsExpression
        {
            get
            {
                return 
                    Distinct(
                        TopOrSubset
                        (
                            Ordered
                            (
                                Filtered
                                (
                                    EnclosedInParentheses(new SetExpression(source.RowsAxis))
                                )
                            )
                        )
                    );
            }
        }

        SqlBuilder Distinct(SqlBuilder onRows)
        {
            if(source.IsDistinct == false
                || source.RowsAxis.IsEmpty)
            {
                return onRows;
            }
            var result = new SqlBuilder();
            result.AppendLine("DISTINCT");
            result.AppendLine("(");
            result.AppendLine(onRows);
            result.Append(")");
            return result;
        }

        SqlBuilder EnclosedInParentheses(SqlBuilder onRows)
        {
            if(onRows.IsEmpty)
            {
                return onRows;
            }
            var result = new SqlBuilder();
            result.AppendLine("(");
            result.AppendLine(onRows);
            result.AppendLine(")");
            return result;
        }

        SqlBuilder Filtered(SqlBuilder onRows)
        {
            if( source.Where.IsEmpty )
            {
                return onRows;
            }
            var result = new SqlBuilder();
            result.AppendLine("FILTER");
            result.AppendLine("(");
            result.AppendLine(onRows);
            result.AppendLine(",");
            result.AppendLine(source.Where);
            result.Append(")");

            return result;
        }

        SqlBuilder TopOrSubset(SqlBuilder onRows)
        {
            if ((source.Top == null 
                    && source.SkipCount == 0)
                || onRows.IsEmpty)
            {
                return onRows;
            }
            if (source.SkipCount == 0)
            {
                return Top(onRows);
            }
            return Subset(onRows);
        }

        SqlBuilder Subset(ISqlFragment onRows)
        {
            var result = new SqlBuilder();
            result.AppendLine("SUBSET");
            result.AppendLine("(");
            result.Append(onRows);
            result.AppendLine(",");
            result.Append(source.SkipCount.ToString());
            if (source.Top != null)
            {
                result.AppendLine(",");
                result.AppendLine(source.Top.TopCount);
            }
            result.AppendLine(")");
            return result;
        }

        SqlBuilder Top(ISqlFragment onRows)
        {
            var result = new SqlBuilder();
            result.AppendLine("HEAD");
            result.AppendLine("(");
            result.Append(onRows);
            result.AppendLine(",");
            result.AppendLine(source.Top.TopCount);
            result.AppendLine(")");
            return result;
        }

        /// <summary>
        /// Wraps ON ROWS with OrderBy
        /// </summary>
        SqlBuilder Ordered(SqlBuilder onRows)
        {
            if(source.OrderBy.IsEmpty)
            {
                return onRows;
            }
            var onRowsWrapped = new SqlBuilder();
            onRowsWrapped.AppendLine("(");
            onRowsWrapped.AppendLine(onRows);
            onRowsWrapped.AppendLine(")");
            //TODO: move Replace() into SqlBuilder and get rid of SqlBuilder.ToString() conversion everywhere
            return source.OrderBy.ToString()
                .Replace(MdxSelectStatement.OnRowsPlaceholder, onRowsWrapped.ToString()) ; 
        }

        SqlBuilder GetOnColumns()
        {
            var result = new SqlBuilder();
            result.AppendLine("{");
            result.AppendLine
                (
                    source.ColumnsAxis.HasCustomColumns
                        ? source.ColumnsAxis.ToString()
                        : MdxSelectStatement.C1
                );
            result.AppendLine("}");
            result.Append("ON COLUMNS");

            return result;
        }

        SqlBuilder WithOnRows(SqlBuilder onRowsExpression)
        {
            var result = new SqlBuilder();
            if (onRowsExpression.IsEmpty)
            {
                return result;
            }

            if ( IsNonEmptyRequired())
            {
                result.AppendLine("NON EMPTY");
            }
            result.Append(onRowsExpression);
            result.AppendLine("ON ROWS,");

#if ExistsByMeasureGroupIsREady
            Select.AppendLine("EXISTS"); //TODO: Patent EXISTS usage for MDX generation
            Select.AppendLine("(");
            //1st EXISTS set parameter (to be returned)
            Select.AppendLine("(");
            Select.AppendLine(sqlSelectStatement.Select);
            Select.AppendLine("),");
            //2nd EXISTS set parameter (to cross-join with for auto-exist filtering)
            Select.AppendLine("{");
            Select.AppendLine(WrappedColumnsAxis);
            Select.AppendLine("},");
            //MeasureGroup name by which members from 1st set are filtered
            Select.Append("'"); //TODO: implement multiple measure groups case support, use UNION() multiple EXISTIS() together
            Select.Append("Order Details"); //TODO: replace with SourceColumnsAxis.Measures[i].MeasureGroupName
            Select.AppendLine("'");

            Select.AppendLine(")"); //end of EXISTS
            Select.AppendLine("ON ROWS,");
#endif
            return result;
        }

        #region LicensingProtection
        static Assembly ourAssembly;
        static Assembly OurAssembly
        {
            get
            {
                return ourAssembly 
                    ?? ( ourAssembly = Assembly.GetExecutingAssembly() );
            }
        }

        bool IsNonEmptyRequired()
        {
            string publicKey = OurAssembly.GetPublicKey();
            if (publicKey.Length == 320
                && publicKey.Right(25) == "17561913bd08aa549cb7e55e5")
            {
                return source.ColumnsAxis.HasCustomColumns
                    && source.IsTopMost; //real code line
            }
            return false; //licensing protection line
        }
        #endregion

    }
}