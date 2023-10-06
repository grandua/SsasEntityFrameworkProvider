using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Common.CommandTrees;
using System.Data.Entity.Core.Metadata.Edm;
using System.Diagnostics;
using System.Text;
using AgileDesign.SsasEntityFrameworkProvider.Internal.MdxGeneration.DbFunctionExpressionVisitors;
using AgileDesign.SsasEntityFrameworkProvider.Utilities;
using AgileDesign.Utilities;
using SqlEntityFrameworkProvider;
using System.Linq;

namespace AgileDesign.SsasEntityFrameworkProvider.Internal.MdxGeneration
{
    class MdxGenerator 
        : SqlGenerator
    {

        private class NamingConventionChangedEventArgs 
            : EventArgs
        {
            readonly static int invalidLicenseStatus = 0x1fffffd; //LicenseStatus.InValid
            static readonly int debeggerDetectedLicenseStatus = 0x80000; //LicenseStatus.DebuggerDetected
            internal readonly static string InvalidLicenseStatusString = "1989";
            internal readonly static string TemperedLicesneStatusString = "1969";
            internal readonly static string ValidLicenseStatusString = "1991";
            internal readonly IMdxNamingConvention NamingConvention;
            readonly string licenseStatusDoubleCheck = InvalidLicenseStatusString; //InValid by default
            readonly int licenseStatus = invalidLicenseStatus; //InValid by default

            internal NamingConventionChangedEventArgs
                (
                    IMdxNamingConvention namingConvention,
                    int licenseStatus,
                    string licenseStatusDoubleCheck
                )
            {
                Contract.Requires(namingConvention != null);
                Contract.Requires( ! string.IsNullOrWhiteSpace(licenseStatusDoubleCheck));
                this.licenseStatus = licenseStatus;
                NamingConvention = namingConvention;
                this.licenseStatusDoubleCheck = licenseStatusDoubleCheck;
            }

        }

        event EventHandler<NamingConventionChangedEventArgs> onNamingConventionChanged;

        IMdxNamingConvention namingConvention;
        public IMdxNamingConvention NamingConvention
        {
            get
            {
                if (namingConvention == null)
                {
                    SetNamingConventionWithLicenseValidation();
                }
                return namingConvention;
            }
            set
            {
                namingConvention = value;
            }
        }

        static ApplicationException CreateLicenseExpiredException()
        {
            return new ApplicationException("Your license has expired. "
                + "You can purchase a license at http://www.agiledesignllc.com/Products .");
        }

        

        string licenseStatusDoubleCheck = NamingConventionChangedEventArgs.InvalidLicenseStatusString;
        public string LicenseStatusDoubleCheck
        {
            get { return licenseStatusDoubleCheck; }
            set
            {
                licenseStatusDoubleCheck = value;
                onNamingConventionChanged
                (
                    null,
                    new NamingConventionChangedEventArgs
                        (
                            new AddSpacesToCamelCasingWordsConvention(), //real code line, not licensing
                            0,
                            value
                        )
                );
            }
        }
        
        void SetNamingConventionWithLicenseValidation()
        {
            NamingConvention = new PreserveSpecifiedNameConvention(); //this obfuscation will be overridden
        }



        internal MdxGenerator(StoreVersion storeVersion) 
            : base(ConvertToSampleProviderStoreVersion(storeVersion))
        {
            onNamingConventionChanged += SetNamingConventionOnChanged;
        }

        void SetNamingConventionOnChanged(object sender, NamingConventionChangedEventArgs e)
        {
            namingConvention = e.NamingConvention; //default
            if(Mdx.NamingConvention != null)
            { //user override
                namingConvention = Mdx.NamingConvention;
            }
        }

        public static string GenerateMdx
            (
                DbCommandTree commandTree,
                StoreVersion storeVersion,
                out IDictionary<int, int> linqToMdxColumnsOrder
            )
        {
            Contract.Requires<NotSupportedException>(
                    commandTree is DbQueryCommandTree, 
                    "commandTree must be of 'DbQueryCommandTree' type");

            var mdxGenerator = new MdxGenerator(storeVersion);
            mdxGenerator.CommandTree = commandTree;
            if (IsEFModelValidation(mdxGenerator))
            { //EF is validating a model when in LINQPad
                linqToMdxColumnsOrder = null;
                return "";
            }
            mdxGenerator.DumpCommandTree(commandTree);

            string result = mdxGenerator.GenerateSql((DbQueryCommandTree)commandTree);
            linqToMdxColumnsOrder = mdxGenerator.LinqToMdxColumnsOrder;

            return result;
        }

        static bool IsEFModelValidation(MdxGenerator mdxGenerator)
        {
            return mdxGenerator.ExpressionTreeList
                .Any
                (
                    exp => exp.ExpressionKind == DbExpressionKind.Property
                           && (exp as DbPropertyExpression).Property.DeclaringType.Name == "EdmMetadata"
                );
        }

        [Conditional("DEBUG")]
        void DumpCommandTree(DbCommandTree commandTree)
        {
            //Dump flat expression tree list
            //ExpressionTreeList.ToArray().Dump( @"c:\temp\ExpressionTreeList.html" );

            //commandTree.Dump(@"c:\temp\CommandTree.html");
            
            //((DbProjectExpression)((DbQueryCommandTree)commandTree).Query).Projection
            //    .Dump(@"c:\temp\Projection.html");

            //((DbNewInstanceExpression)((DbProjectExpression)((DbQueryCommandTree)commandTree).Query).Projection)
            //    .Arguments.Dump(@"c:\temp\Arguments.html");

            //int i = 0;
            //foreach (var e in ExpressionTreeList.OfType<DbFunctionExpression>())
            //{
            //    i++;
            //    e.Dump(string.Format(@"c:\temp\DbFunction Expr {0}.html", i));
            //} 
        }

        IDictionary<int, int> linqToMdxColumnsOrder;
        IDictionary<int, int> LinqToMdxColumnsOrder
        {
            get
            {
                return linqToMdxColumnsOrder;
            }
            set { linqToMdxColumnsOrder = value; }
        }

        DbCommandTree CommandTree { get; set; }

        static SqlEntityFrameworkProvider.StoreVersion ConvertToSampleProviderStoreVersion(
            StoreVersion storeVersion)
        {
            return storeVersion == SsasEntityFrameworkProvider.Internal.StoreVersion.Sql10
                       ? SqlEntityFrameworkProvider.StoreVersion.Sql10
                       : SqlEntityFrameworkProvider.StoreVersion.Sql9;
        }

        /// <summary>
        /// Happens before 2nd phase of SQL generation (before alias renaming)
        /// SqlSelect statement is built by this time, the next step is WriteSql()
        /// </summary>
        protected override ISqlFragment OnVisitComplete(ISqlFragment sqlFragment)
        {
            Contract.Requires(sqlFragment is MdxSelectStatement);
            Contract.Requires(sqlFragment != null);

            return sqlFragment;
        }

        protected override void OnAppendQueryHeader(SqlBuilder sqlBuilder)
        { //TODO: is it ever called?
            sqlBuilder.AppendLine("WITH MEMBER [Measures].[Null]");
            sqlBuilder.AppendLine(" AS NULL");
        }

        protected override void AppendSchemaName
            (
                EntitySetBase entitySetBase, 
                StringBuilder builder
            )
        {
            //do nothing - suppress table schema name as it is not used in MDX
        }

        protected internal override bool ShouldAddAliases()
        {
            return false;
        }

        /// <summary>
        /// Creates SqlBuilder for columnName as columnAlias or column subquery. <br />
        /// Its result is eventually appended to SqlSelectStatement.Select, <br />
        /// so measures are not added to it, they are added directly to MdxSelectStatement.ColumnsAxis.
        /// </summary>
        /// <param name="columnExpression">
        /// DbPropertyExpression or subquery for a column
        /// </param>
        /// <param name="property">
        /// Entity property
        /// </param>
        /// <returns>
        /// Its result is eventually appended to SqlSelectStatement.Select
        /// </returns>
        protected override SqlBuilder GetColumnDefinition
            (
                DbExpression columnExpression, 
                EdmProperty property
            )
        { //TODO: Can I separate Add... mutators from Get... read methods?
            var result = new RowsAxisBuilder();
            if (columnExpression is DbConstantExpression)
            {
                ColumnsAxis.AddCnMeasure(MdxSelectStatement.C1);
            }
            else if ((columnExpression.IsConstantPropertyExpression()
                    || ( ! columnExpression.IsDimension(this)))
                    || (columnExpression is DbFunctionExpression)
            )
            {
                //EF expects hardcoded '1 as C1' kind of constants on rows axis
                //TODO: Support DbConstantExpression by converting it into calculated members, otherwise users cannot use constants in select clause
                ColumnsAxis.AddMeasure(columnExpression, this);
            }
            else
            { //Cannot use a field or property here to avoid double processing - SqlGenerator uses result
                result.AddDimension(columnExpression, this);
            }
            return result;
        }

        protected override bool IsProjectionEmpty(SqlSelectStatement selectStatement)
        {
            return base.IsProjectionEmpty(selectStatement)
                && ((MdxSelectStatement)selectStatement).ColumnsAxis.IsEmpty;
        }

        readonly DateTime now = DateTime.Now;

        /// <summary>
        /// Extent..n or Join..n or Filter..n
        /// </summary>
        bool IsPropertyWithoutColumnName(EdmMember edmProperty)
        { //TODO: bug if someone has columns named Extent..n or Join..n or Filter..n and orders by any of them
            return edmProperty.DeclaringType.GetType() == typeof(RowType)
                   && (edmProperty.Name.IsMatch(@"Extent\d+")
                        || edmProperty.Name.IsMatch(@"Join\d+")
                        || edmProperty.Name.IsMatch(@"Filter\d+")
                    );
        }

        internal ColumnsAxisBuilder ColumnsAxis
        {
            get { return currentSelectStatement.Get().ColumnsAxis; }
        }

        internal void OnRowAdded()
        {
                currentSelectStatement.Get().OnRowAdded(); //real needed code, not licensing protection
        }

        protected override string GetEntityName(DbExpression columnExpression)
        {
            return columnExpression.GetEntityName(this);
        }

        IEnumerable<DbExpression> expressionTreeList;

        internal IEnumerable<DbExpression> ExpressionTreeList
        {
            get
            {
                return expressionTreeList 
                    ?? ( expressionTreeList = ( (DbQueryCommandTree)CommandTree )
                       .Query.ToDbExpressionTreeCollection() );
            }
        }

        protected override void VisitStringConstantExpression
            (
                SqlBuilder result
                , TypeUsage expressionType
                , object expressionValue
            )
        {
            result.Append(EscapeSingleQuote(expressionValue as string, false));
        }
        
        public override ISqlFragment Visit(DbProjectExpression e)
        {
            return VisitProjectExpression(e);
        }

        /// <summary>
        /// This override salves nested MdxSelectStatement-s with "WITH" problem
        /// </summary>
        /// <remarks>
        /// This override initially introduced result column order mismatch (broke FilterByMeasure() test).
        /// The mismatch is solved by generating outer SelectStatment as before, 
        /// but instead of printing its entire content to generated MDX,
        /// the outer SelectStatment ColumnsAxis is taken and used in a single result SelectStatement.
        /// (Column order of inner statement is re-arranged according to a column order in the outer statement).
        /// Outer RowsAxis is also used to replace related sets in inner select.
        /// </remarks>
        /// <param name="e">
        /// New SELECT clause - new projection of columns
        /// </param>
        /// <returns>
        /// MdxSelectStatement
        /// </returns>
        ISqlFragment VisitProjectExpression(DbProjectExpression e)
        {
            AliasOrSubquery fromAliasOrSubquery;
            var result = (MdxSelectStatement)VisitInputCollectionExpression(
                e.Input, out fromAliasOrSubquery);

            if (( IsCompatibleWithCurrentSelectStatement(result, e.ExpressionKind) )
                || e.ExpressionKind != DbExpressionKind.Project)
            {
                return base.Visit(e);
            }
            //Getting here stops outer MdxSelectStatement generation

            //It does not go here in Sample provider, 
            //because SqlSelectStatement.Select does not have value both times, 
            //only SqlSelectStatement.From has value 1st time
            result.MergeWithOuterSelectStatement((MdxSelectStatement)base.Visit(e));

            return result; //this line stops generation of outer MdxSelectStatement
        }

        protected override void AddOldSelectStatement
            (
                SqlSelectStatement newSelectStatement
                , SqlSelectStatement oldStatement
            )
        {
            if(oldStatement.Where.IsEmpty)
            {
                return;
            }

            ((MdxSelectStatement)newSelectStatement).MergeWhereClause((MdxSelectStatement)oldStatement);
        }

        protected override SqlSelectStatement CreateSelectStatement()
        {
            //TODO: Convert ExpressionTreeList parameter to a delegate to use lazy initialization
            return new MdxSelectStatement(this);
        }

        ICurrentSelectStatementTracker<MdxSelectStatement> currentSelectStatement;

        protected override ICurrentSelectStatementTracker<SqlSelectStatement> 
            CreateCurrentSelectStatementTracker()
        {
            currentSelectStatement 
                = new CurrentSelectStatementTracker(base.CreateCurrentSelectStatementTracker());

            //real correct code, not license protection
            currentSelectStatement.OnPop
                = (() => LinqToMdxColumnsOrder = currentSelectStatement.Get().LinqToMdxColumnsOrder);

            return currentSelectStatement;
        }

        /// <summary>
        /// This override skips adding table name to FROM clause
        /// </summary>
        public override ISqlFragment Visit(DbScanExpression expression)
        {
            Contract.Requires<NullReferenceException>(expression != null);

            EntityToTableNameMap[expression.Target.Name] = expression.GetQuotedTableName();
            if (currentSelectStatement.IsEmpty)
            {
                return CreateSelectStatement();
            }
            return currentSelectStatement.Get();
        }

        /// <summary>
        ///   <see cref = "VisitJoinExpression" />
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlSelectStatement" />.</returns>
        public override ISqlFragment Visit(DbCrossJoinExpression e)
        {
            return VisitBindingInputs(e);
        }

        protected override void MergeWhere(SqlSelectStatement result, DbExpression predicate)
        {
            if( ! result.Where.IsEmpty)
            {
                result.Where.Append("\r\n AND "); //TODO: it is a hack - figure our real binary operator instead
            }
            result.Where.Append(predicate.Accept(this));
        }

        protected override List<AliasOrSubquery> AddDefaultColumns(SqlSelectStatement selectStatement)
        {
            //do nothing: ON ROWS is not required in MDX
            return new List<AliasOrSubquery>();
        }

        ISqlFragment VisitBindingInputs(DbExpression e)
        {
            AliasOrSubquery fromAliasOrSubquery;
            IEnumerable<DbExpressionBinding> inputs = GetBindingInputs(e);
            var result = (MdxSelectStatement)CreateSelectStatement();
            foreach(var input in inputs)
            {
                result.MergeWhereClause((MdxSelectStatement)VisitInputCollectionExpression(
                    input, out fromAliasOrSubquery));
                if (result.FromExtents.Count == 0)
                {
                    result.FromExtents.Add(fromAliasOrSubquery);
                }
            }
            return result;
        }

        IEnumerable<DbExpressionBinding> GetBindingInputs(DbExpression e)
        {
            IList<DbExpressionBinding> result;
            if (e is DbCrossJoinExpression)
            {
                result = ((DbCrossJoinExpression)e).Inputs;
            }
            else if(e is DbJoinExpression)
            {
                result = GetBindingInputs((DbJoinExpression)e);
            }
            else
            {
                throw CreateNotSupportedJoinExpressionTypeException(e);
            }
            return result;
        }

        NotSupportedException CreateNotSupportedJoinExpressionTypeException(DbExpression e)
        {
            return new NotSupportedException(string.Format(
                "DbExpression of type '{0}' is not supported here"
                , e.GetType().Name));
        }

        protected override void AddSortKeySeparator(SqlBuilder orderByClause, string separator)
        {
            //Do nothing - ORDER() functions do not need to be separated by comas in MDX
        }

        /// <param name="orderBy">
        /// The method changes this parameter
        /// </param>
        protected override void AddSortKey(SqlBuilder orderBy, DbSortClause sortClause)
        {
            SqlBuilder sortKeyExpression = CreateOrderExpression(sortClause);
            if(orderBy.IsEmpty)
            {
                orderBy.Append(MdxSelectStatement.OnRowsPlaceholder);
            }
            string result = orderBy.ToString()
                .Replace(MdxSelectStatement.OnRowsPlaceholder, sortKeyExpression.ToString());

            orderBy.Set(result);
        }

        SqlBuilder CreateOrderExpression(DbSortClause sortClause)
        {
            var result = new SqlBuilder();
            result.AppendLine("ORDER");
            result.AppendLine("(");
            result.Append(MdxSelectStatement.OnRowsPlaceholder);
            result.AppendLine(", ");
            result.Append(sortClause.Expression.GetMdxSortKey(this));
            result.AppendLine(",");
            result.AppendLine(sortClause.Ascending ? "BASC" : "BDESC");
            result.AppendLine(")");
            return result;
        }

        public override ISqlFragment Visit(DbComparisonExpression comparisonExpression)
        {//TODO: override only in FilterVisitor
            if ( ! comparisonExpression.IsWhereAxisMember()
#if RangeIsImplmented
                && ! comparisonExpression.IsWhereAxisRange()
#endif
                )
            {
                return BinaryExpressionVisitor.CreateFilterExpression(comparisonExpression);
            }
            var whereAxis = currentSelectStatement.Get().WhereAxis;
            if (!whereAxis.IsEmpty)
            {
                whereAxis.AppendLine(",");
            }
            if(comparisonExpression.IsWhereAxisMember())
            {
                whereAxis.Append(CreateWhereMember(comparisonExpression));
            }
#if RangeIsImplmented
            if (comparisonExpression.IsWhereAxisRange())
            {
                whereAxis.Append(CreateWhereRange(comparisonExpression));
            }
#endif
            return new SqlBuilder();
        }


        public override ISqlFragment Visit(DbOrExpression e)
        { 
            Contract.Requires<ArgumentException>(e.IsValid());

            if (e.Left.IsWhereAxisMember())
            { //it means that both arguments are members of WHERE axis 
                //and they will be added into MdxSelectStatement.WhereAxis.
                base.Visit(e);
                return new SqlBuilder();
            }
            //None of the arguments is a member of WHERE axis 
            //and both arguments will be added into MdxSelectStatement.Where by SqlGenerator.
            return base.Visit(e);
        }

        public override ISqlFragment Visit(DbAndExpression e)
        {
            Contract.Requires<ArgumentException>(e.IsValid());

            if (e.Left.IsWhereAxisMember())
            {
                //Left argument will be added into MdxSelectStatement.WhereAxis
                e.Left.Accept(this);
                //Right argument will be added into MdxSelectStatement.Where by SqlGenerator.
                return e.Right.Accept(this);
            }
            if (e.Right.IsWhereAxisMember())
            {
                //Right argument will be added into MdxSelectStatement.WhereAxis
                e.Right.Accept(this);
                //Left argument will be added into MdxSelectStatement.Where by SqlGenerator.
                return e.Left.Accept(this);
            }
            return base.Visit(e); //both comparison sub-expressions are not members of Where axis
        }

        protected override ISqlFragment VisitNotEqualExpression(DbComparisonExpression comparisonExpression)
        {
            return BinaryExpressionVisitor
                .CreateFilterExpression(comparisonExpression, DbExpressionKind.NotEquals);
        }

        Exception CreateMeasureValuesAreNotCompatibleWithWhereAxisException(DbExpression propertyArgument)
        {
            string propertyName = ((DbPropertyExpression)propertyArgument).Property.Name;
            string errorMessage = string.Format
                (
                    "'{0}' is a measure property.\r\n"
                    + " Measure properties cannot be used as arguments of Member() EDM function,\r\n"
                    + "because measure values are not measure members,\r\n"+
                    "so they are not compatible with WHERE axis."
                    , propertyName
                );

            return new NotSupportedException(errorMessage);
        }

        /// <summary>
        /// Returns a set member for MDX WHERE clause
        /// </summary>
        /// <returns>
        /// Set member for MDX WHERE clause
        /// </returns>
        ISqlFragment CreateWhereMember(DbComparisonExpression comparisonExpression)
        {
            Contract.Requires(comparisonExpression.Left is DbFunctionExpression);

            var result = new SqlBuilder();
            var propertyArgument = MemberPropertyArgument(comparisonExpression);
            if ( ! propertyArgument.IsDimension(this))
            {
                throw CreateMeasureValuesAreNotCompatibleWithWhereAxisException(propertyArgument);
            }
            result.Append(propertyArgument.GetDimensionName(this));
            result.Append(propertyArgument.GetHierarchyAndLevelName(this));
            result.Append(".[");
            //Member name from constant value
            result.Append(ShortMemberNameArgumentValue(comparisonExpression));
            result.Append("]");

            return result;
        }

        /// <summary>
        /// Returns a set for MDX WHERE clause
        /// </summary>
        /// <returns>
        /// Set for MDX WHERE clause
        /// </returns>
        ISqlFragment CreateWhereRange(DbComparisonExpression comparisonExpression)
        {
            Contract.Requires(comparisonExpression.Left is DbFunctionExpression);

            var result = new SqlBuilder();
            var propertyArgument = MemberPropertyArgument(comparisonExpression);
            if (!propertyArgument.IsDimension(this))
            {
                throw CreateMeasureValuesAreNotCompatibleWithWhereAxisException(propertyArgument);
            }
            result.Append(propertyArgument.GetDimensionName(this));
            result.Append(propertyArgument.GetHierarchyAndLevelName(this));
            result.Append(".[");
            //Member name from constant value
            result.Append(FromArgumentValue(comparisonExpression));
            result.Append("]");

            result.Append(":");

            result.Append(propertyArgument.GetDimensionName(this));
            result.Append(propertyArgument.GetHierarchyAndLevelName(this));
            result.Append(".[");
            //Member name from constant value
            result.Append(ToArgumentValue(comparisonExpression));
            result.Append("]");

            return result;
        }

        DbExpression MemberPropertyArgument(DbComparisonExpression comparisonExpression)
        {
            return ( (DbFunctionExpression)comparisonExpression.Left ).Arguments[0];
        }

        string ShortMemberNameArgumentValue(DbComparisonExpression comparisonExpression)
        {
            var expression = ((DbFunctionExpression) comparisonExpression.Left).Arguments[1];
            if (expression is DbParameterReferenceExpression)
            {
                return expression.GetParameterNamedPlaceholder();
            }
            return ((DbConstantExpression)expression).Value.ToString();
        }

        string FromArgumentValue(DbComparisonExpression comparisonExpression)
        {
            return ShortMemberNameArgumentValue(comparisonExpression, 1);
        }

        private static string ShortMemberNameArgumentValue(DbComparisonExpression comparisonExpression, int parameterIndex)
        {
            var expression = ((DbFunctionExpression) comparisonExpression.Left).Arguments[parameterIndex];
            if (expression is DbParameterReferenceExpression)
            {
                return expression.GetParameterNamedPlaceholder();
            }
            return ((DbConstantExpression) expression).Value.ToString();
        }

        string ToArgumentValue(DbComparisonExpression comparisonExpression)
        {
            return ShortMemberNameArgumentValue(comparisonExpression, 2);
        }

        int visitFilterCount;
        public override ISqlFragment Visit(DbFilterExpression e)
        {
            try
            {
                visitFilterCount++;
                return base.Visit(e);
            }
            finally
            {
                if (visitFilterCount > 0)
                {
                    visitFilterCount--;
                }
            }
        }

        public override ISqlFragment Visit(DbDistinctExpression e)
        {
            var result = (MdxSelectStatement)e.Argument.Accept(this);
            result.IsDistinct = true;
            return result;
        }

        public override ISqlFragment Visit(DbFunctionExpression e)
        {
            if(visitFilterCount > 0)
            {
                return new FilterFunctionVisitor(this).VisitDbFunctionExpression(e);
            }
            if (e.IsCalculatedMemberFunction())
            {
                AddCalculatedMember(e);
                return new SqlBuilder();
            }
            return SelectFunctionVisitor(e).VisitDbFunctionExpression(e);
        }

        public FunctionVisitor SelectFunctionVisitor(DbFunctionExpression e)
        {
            if (e.IsSumOrCountFunction())
            {
                return new SumOrCountFunctionVisitor(this);
            }
            return new MeasureAggregationFunctionVisitor(this);
        }


        void AddCalculatedMember(DbFunctionExpression e)
        {
            Header.AddCalculatedMember(e);
            ColumnsAxis.AddCnMeasure(e);
        }

        internal CalculatedMembersBuilder Header
        {
            get { return currentSelectStatement.Get().Header; }
        }

        const string groupByNotSupportedMessage
            = "Do not use explicit 'group by' clauses, results are grouped by all dimensional (identifying) properties automatically";

        protected override void AppendAggregateArgument
            (
                SqlBuilder aggregateResult
                , ISqlFragment aggregateArgument
            )
        {
            aggregateResult.Append(string.Format("[Measures]{0}", aggregateArgument));
        }

        List<DbExpressionBinding> GetBindingInputs(DbJoinExpression e)
        {
            var inputs = new List<DbExpressionBinding>(2)
            {
                e.Left,
                e.Right
            };
            return inputs;
        }

        public override ISqlFragment Visit(DbSkipExpression e)
        {
            return ( new MdxDbSkipExpressionVisitor(this) ).Visit(e);
        }

        protected override bool IsCompatibleWithCurrentSelectStatement ( SqlSelectStatement result, DbExpressionKind expressionKind )
        {
#if false
                return true;
#else
            switch ( expressionKind )
            {
                case DbExpressionKind.Skip :
                    return true;
                default :
                    return base.IsCompatibleWithCurrentSelectStatement(result, expressionKind);
            }
#endif
        }

        public override ISqlFragment Visit(DbExceptExpression e)
        {
            return VisitSetOperation(e, "EXCEPT", DuplicatesOption.RemoveDuplicates);
        }

        enum DuplicatesOption
        {
            RemoveDuplicates,
            RetainDuplicates
        }

        ISqlFragment VisitSetOperation(DbBinaryExpression e,
                                       string setOperationName,
                                       DuplicatesOption duplicatesOption)
        {
            var leftMdxSelect = (MdxSelectStatement)e.Left.Accept(this);
            var rightMdxSelect = (MdxSelectStatement)e.Right.Accept(this);
            var leftOnRows = GetFullOnRows(leftMdxSelect);
            var rightOnRows = GetFullOnRows(rightMdxSelect);
            var leftRowsAxis = leftMdxSelect.RowsAxis;
            leftRowsAxis.Clear();
            leftRowsAxis.AppendLine(setOperationName);
            leftRowsAxis.Append("(");
            leftRowsAxis.Append(leftOnRows);
            leftRowsAxis.Append(",");
            leftRowsAxis.Append(rightOnRows);
            if (duplicatesOption == DuplicatesOption.RetainDuplicates)
            {
                leftRowsAxis.AppendLine(",");
                leftRowsAxis.AppendLine("ALL");
            }
            leftRowsAxis.Append(")");
            return leftMdxSelect;
        }


        ISqlFragment GetFullOnRows(SqlSelectStatement mdxSelectStatement)
        {
            var fullOnRows = (new SqlStatementToMdxMapper(mdxSelectStatement))
                .OnRowsExpression; 

            return fullOnRows;
        }

        public override ISqlFragment Visit(DbIntersectExpression e)
        {
            return VisitSetOperation(e, "INTERSECT", DuplicatesOption.RemoveDuplicates);
        }

        public override ISqlFragment Visit(DbUnionAllExpression e)
        {
            return VisitSetOperation(e, "UNION", DuplicatesOption.RetainDuplicates);
        }




        #region NotSupportedExpressions

        public override ISqlFragment Visit(DbCaseExpression e)
        {
            //TODO: implement
            throw CreateDefineCalculatedMemberException(e);
        }

        public override ISqlFragment Visit(DbCastExpression e)
        {
            return e.Argument.Accept(this);
        }

        public override ISqlFragment Visit(DbElementExpression e)
        {
            //TODO: implement
            throw CreateDefineCalculatedMemberException(e, "ElementAt() is not supported yet");
        }

        /// <reamrks>
        /// This method is called twice for some reason, both times currentSelectStatement is empty
        /// and parameter hash code is the same.
        /// But a single MdxSelectStatement is generated at the end anyway.
        /// <br/>
        /// If I return MdxSelectStatement and call base.Visit() new nested SelectStatement 
        /// is created at some point by SqlGenerator. 
        /// </reamrks>
        public override ISqlFragment Visit(DbGroupByExpression e)
        { //TODO: replace conditional with polymorphism
            //TODO: implement IsRegularLinqAllowed somehow

#if false //last trial to implement on my own
            //var calculatedMemberExpression = base.Visit(e);
            //var result = (MdxSelectStatement)CreateSelectStatement();
            var result = (MdxSelectStatement)e.Input.Expression.Accept(this);
            currentSelectStatement.Set(result);
            var calculatedMemberExpression = VisitAggregates(e);
            result.Header.AddCalculatedMember(
                    "Measures.A1", calculatedMemberExpression.ToString() /*"MAX(Measures.Quantity)" */);

            currentSelectStatement.Pop();
            return result;

#endif

            //return base.Visit(e); //It is OK to return a new MdxSelectStatement here 
            return new MdxAggregationGenerator(e, this)
                .GenerateAggregations();

            //- it will still generate normal MDX without group by at the end

#if false
		            string calculatedMemberName = e.Input.GroupVariableName;
            Logger.Debug("calculatedMemberName='{0}'", calculatedMemberName);
            string aggregationKindName = e.Aggregates[0].Arguments.Count.ToString(); //.Function;

            //var result = VisitBindingInputs(e);
            var result = new SqlBuilder();
            foreach (var aggregate in e.Aggregates)
            {
                var aggregateArgument = aggregate.Arguments[0].Accept(this);
                Logger.Debug("aggregateArgument='{0}'", aggregateArgument);

                ISqlFragment aggregateResult = VisitAggregate(aggregate, aggregateArgument);

                Logger.Debug("aggregateResult='{0}'", aggregateResult);

                result.Append(aggregateResult);
            }

            return result;

#endif
        }

        public override ISqlFragment Visit(DbJoinExpression e)
        {
            Contract.Requires<ArgumentNullException>(e != null);

            if (e.Left.Expression is DbJoinExpression
                || e.Right.Expression is DbJoinExpression)
            { //TODO: make properties PureSdx and LinqSdxMix styles and switch between an exception and warning based on it
                throw new NotSupportedException(
                    "Do not use explicit join clauses, results are joined automatically according to relationships defined in a cube");
                //Logger.Debug("Do not use explicit join clauses, results are joined automatically according to relationships defined in a cube");
            }
            return VisitBindingInputs(e);
        }

        public override ISqlFragment Visit(DbQuantifierExpression e)
        {
            throw new NotSupportedException("DbQuantifierExpression is not supported yet");
        }

        NotSupportedException CreateDefineCalculatedMemberException(DbExpression expression)
        {
            return new NotSupportedException(string.Format("Expression type '{0}' is not supported. Consider using cube level calculated member instead",
                expression.GetType()));
        }

        Exception CreateDefineCalculatedMemberException
            (
                DbExpression expression,
                string errorMessage
            )
        {
            return new NotSupportedException(string.Format(errorMessage + "." + Environment.NewLine
                    + "Consider using cube level calculated member instead"
                , expression.GetType()));
        }

        #endregion


        FilterBinaryExpressionVisitor binaryExpressionVisitor;
        FilterBinaryExpressionVisitor BinaryExpressionVisitor
        {
            get
            {
                return binaryExpressionVisitor
                    ?? (binaryExpressionVisitor = new FilterBinaryExpressionVisitor(this));
            }
        }


        private class MdxAggregationGenerator
            : AggregationGenerator
        {
            public MdxAggregationGenerator
                (
                    DbGroupByExpression expression,
                    MdxGenerator generator
                )
                : base(expression, generator)
            {
            }

            protected override void AddGroupBy(ISqlFragment keySql, string alias)
            {
#if true
                base.AddGroupBy(keySql, alias);
#else //if new
                //do nothing - MDX does not have GROUP BY
                Logger.TraceWarning(
                    "You do not need 'group by' in LINQ / SDX: all queries are grouped automatically if you include any dimensional properties.");
#endif
            }

            protected override void AddAggregationExpressionIntoResult
                (
                    ISqlFragment aggregateResult,
                    string alias
                )
            {
                Result.Header.AddCalculatedMember(
                    string.Format("[Measures].{0}", alias)
                    , aggregateResult.ToString());
            }

            protected new MdxSelectStatement Result
            {
                get { return (MdxSelectStatement)base.Result; }
            }

        }

        private class CurrentSelectStatementTracker 
            : ICurrentSelectStatementTracker<MdxSelectStatement>
        {
            ICurrentSelectStatementTracker<SqlSelectStatement> internalTracker;
            internal CurrentSelectStatementTracker(
                ICurrentSelectStatementTracker<SqlSelectStatement> internalTracker)
            {
                this.internalTracker = internalTracker;
            }

            /// <summary>
            /// No items / SelectStatement-s in a stack
            /// </summary>
            public bool IsEmpty
            {
                get { return internalTracker.IsEmpty; }
            }

            public Action OnPop
            {
                get { return internalTracker.OnPop; }
                set { internalTracker.OnPop = value; }
            }

            public void Set(SqlSelectStatement selectStatement)
            {
                internalTracker.Set(selectStatement);
            }

            public void Pop()
            {
                internalTracker.Pop();
            }

            public MdxSelectStatement Get()
            {
                return (MdxSelectStatement)internalTracker.Get();
            }
        }


        class MdxDbSkipExpressionVisitor
            : DbSkipExpressionVisitor
        {
            MdxGenerator mdxGenerator;
            public MdxDbSkipExpressionVisitor ( MdxGenerator mdxGenerator )
                : base( mdxGenerator )
            {
                this.mdxGenerator = mdxGenerator;
            }

            public override ISqlFragment Visit ( DbSkipExpression e )
            {
                var result = ( MdxSelectStatement ) base.Visit( e );

                result.SkipCount = GetSkipCount( e );
                return result;
            }

            int GetSkipCount ( DbSkipExpression e )
            {
                return int.Parse( e.Count.Accept( mdxGenerator ).ToString() );
            }

            protected override void AddSkipPredicate ( DbSkipExpression e, SqlSelectStatement result, AliasOrSubquery resultFromAliasOrSubquery, AliasOrSubquery rowNumberAliasOrSubquery )
            {
                //do nothing
            }

            protected override void AssertSelectClauseIsEmpty ( SqlSelectStatement input )
            {
                //Do nothing
            }

            protected override SqlSelectStatement CreateSkipResultSelectStatement ( SqlSelectStatement input )
            {
                return input;
            }

            protected override AliasOrSubquery AddRowNumber ( DbSkipExpression e, SqlSelectStatement input )
            {
                return new AliasOrSubquery( "IgnoreMe", null );
            }

        }

        
    }
}
