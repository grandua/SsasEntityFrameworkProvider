//---------------------------------------------------------------------
// <copyright file="SqlGenerator.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//---------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Entity.Core.Common.CommandTrees;
using System.Data.Entity.Core.Metadata.Edm;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Text;
using SqlEntityFrameworkProvider.Utilities;

namespace SqlEntityFrameworkProvider
{
    ///<summary>
    ///  Translates the command object into a SQL string targeting 
    ///  SQL Server 2005 or SQL Server 2008.
    ///</summary>
    ///<remarks>
    ///  The translation is implemented as a visitor <see cref = "DbExpressionVisitor{TResultType}" />
    ///  over the query tree.  It makes a single pass over the tree, collecting the sql
    ///  fragments for the various nodes in the tree <see cref = "ISqlFragment" />.
    ///
    ///  The major operations are
    ///  <list type = "bullet">
    ///    <item>Select statement minimization.  Multiple nodes in the query tree
    ///      that can be part of a single SQL select statement are merged. e.g. a
    ///      Filter node that is the input of a Project node can typically share the
    ///      same SQL statement.</item>
    ///    <item>Alpha-renaming.  As a result of the statement minimization above, there
    ///      could be name collisions when using correlated subqueries
    ///      <example>
    ///        <code>
    ///          Filter(
    ///          b = Project( c.x
    ///          c = Extent(foo)
    ///          )
    ///          exists (
    ///          Filter(
    ///          c = Extent(foo)
    ///          b.x = c.x
    ///          )
    ///          )
    ///          )
    ///        </code>
    ///        The first Filter, Project and Extent will share the same SQL select statement.
    ///        The alias for the Project i.e. b, will be replaced with c.
    ///        If the alias c for the Filter within the exists clause is not renamed,
    ///        we will get <c>c.x = c.x</c>, which is incorrect.
    ///        Instead, the alias c within the second filter should be renamed to c1, to give
    ///        <c>c.x = c1.x</c> i.e. b is renamed to c, and c is renamed to c1.
    ///      </example>
    ///    </item>
    ///    <item>Join flattening.  In the query tree, a list of join nodes is typically
    ///      represented as a tree of Join nodes, each with 2 children. e.g.
    ///      <example>
    ///        <code>
    ///          a = Join(InnerJoin
    ///          b = Join(CrossJoin
    ///          c = Extent(foo)
    ///          d = Extent(foo)
    ///          )
    ///          e = Extent(foo)
    ///          on b.c.x = e.x
    ///          )
    ///        </code>
    ///        If translated directly, this will be translated to
    ///        <code>
    ///          FROM ( SELECT c.*, d.*
    ///          FROM foo as c
    ///          CROSS JOIN foo as d) as b
    ///          INNER JOIN foo as e on b.x' = e.x
    ///        </code>
    ///        It would be better to translate this as
    ///        <code>
    ///          FROM foo as c
    ///          CROSS JOIN foo as d
    ///          INNER JOIN foo as e on c.x = e.x
    ///        </code>
    ///        This allows the optimizer to choose an appropriate join ordering for evaluation.
    ///      </example>
    ///    </item>
    ///    <item>Select * and column renaming.  In the example above, we noticed that
    ///      in some cases we add <c>SELECT * FROM ...</c> to complete the SQL
    ///      statement. i.e. there is no explicit PROJECT list.
    ///      In this case, we enumerate all the columns available in the FROM clause
    ///      This is particularly problematic in the case of Join trees, since the columns
    ///      from the extents joined might have the same name - this is illegal.  To solve
    ///      this problem, we will have to rename columns if they are part of a SELECT *
    ///      for a JOIN node - we do not need renaming in any other situation.
    ///      <see cref = "SqlGenerator.AddDefaultColumns" />.
    ///    </item>
    ///  </list>
    ///
    ///  <para>
    ///    Renaming issues.
    ///    When rows or columns are renamed, we produce names that are unique globally
    ///    with respect to the query.  The names are derived from the original names,
    ///    with an integer as a suffix. e.g. CustomerId will be renamed to CustomerId1,
    ///    CustomerId2 etc.
    ///
    ///    Since the names generated are globally unique, they will not conflict when the
    ///    columns of a JOIN SELECT statement are joined with another JOIN. 
    ///
    ///  </para>
    ///
    ///  <para>
    ///    Record flattening.
    ///    SQL server does not have the concept of records.  However, a join statement
    ///    produces records.  We have to flatten the record accesses into a simple
    ///    <c>alias.column</c> form.  <see cref = "SqlGenerator.Visit(DbPropertyExpression)" />
    ///  </para>
    ///
    ///  <para>
    ///    Building the SQL.
    ///    There are 2 phases
    ///    <list type = "numbered">
    ///      <item>Traverse the tree, producing a sql builder <see cref = "SqlBuilder" /></item>
    ///      <item>Write the SqlBuilder into a string, renaming the aliases and columns
    ///        as needed.</item>
    ///    </list>
    ///
    ///    In the first phase, we traverse the tree.  We cannot generate the SQL string
    ///    right away, since
    ///    <list type = "bullet">
    ///      <item>The WHERE clause has to be visited before the from clause.</item>
    ///      <item>extent aliases and column aliases need to be renamed.  To minimize
    ///        renaming collisions, all the names used must be known, before any renaming
    ///        choice is made.</item>
    ///    </list>
    ///    To defer the renaming choices, we use symbols <see cref = "AliasOrSubquery" />.  These
    ///    are renamed in the second phase.
    ///
    ///    Since visitor methods cannot transfer information to child nodes through
    ///    parameters, we use some global stacks,
    ///    <list type = "bullet">
    ///      <item>A stack for the current SQL select statement.  This is needed by
    ///        <see cref = "SqlGenerator.Visit(DbVariableReferenceExpression)" /> to create a
    ///        list of free variables used by a select statement.  This is needed for
    ///        alias renaming.
    ///      </item>
    ///      <item>A stack for the join context.  When visiting a <see cref = "DbScanExpression" />,
    ///        we need to know whether we are inside a join or not.  If we are inside
    ///        a join, we do not create a new SELECT statement.</item>
    ///    </list>
    ///  </para>
    ///
    ///  <para>
    ///    Global state.
    ///    To enable renaming, we maintain
    ///    <list type = "bullet">
    ///      <item>The set of all extent aliases used.</item>
    ///      <item>The set of all column aliases used.</item>
    ///    </list>
    ///
    ///    Finally, we have a symbol table to lookup variable references.  All references
    ///    to the same extent have the same symbol.
    ///  </para>
    ///
    ///  <para>
    ///    Sql select statement sharing.
    ///
    ///    Each of the relational operator nodes
    ///    <list type = "bullet">
    ///      <item>Project</item>
    ///      <item>Filter</item>
    ///      <item>GroupBy</item>
    ///      <item>Sort/OrderBy</item>
    ///    </list>
    ///    can add its non-input (e.g. project, predicate, sort order etc.) to
    ///    the SQL statement for the input, or create a new SQL statement.
    ///    If it chooses to reuse the input's SQL statement, we play the following
    ///    symbol table trick to accomplish renaming.  The symbol table entry for
    ///    the alias of the current node points to the symbol for the input in
    ///    the input's SQL statement.
    ///    <example>
    ///      <code>
    ///        Project(b.x
    ///        b = Filter(
    ///        c = Extent(foo)
    ///        c.x = 5)
    ///        )
    ///      </code>
    ///      The Extent node creates a new SqlSelectStatement.  This is added to the
    ///      symbol table by the Filter as {c, Symbol(c)}.  Thus, <c>c.x</c> is resolved to
    ///      <c>Symbol(c).x</c>.
    ///      Looking at the project node, we add {b, Symbol(c)} to the symbol table if the
    ///      SQL statement is reused, and {b, Symbol(b)}, if there is no reuse.
    ///
    ///      Thus, <c>b.x</c> is resolved to <c>Symbol(c).x</c> if there is reuse, and to
    ///      <c>Symbol(b).x</c> if there is no reuse.
    ///    </example>
    ///  </para>
    ///</remarks>
    internal class SqlGenerator : DbExpressionVisitor<ISqlFragment>
    {
        #region Visitor state stacks

        /// <summary>
        ///   Every relational node has to pass its SELECT statement to its children
        ///   This allows them (DbVariableReferenceExpression eventually) to update the list of
        ///   outer extents (free variables) used by this select statement.
        /// </summary>
        ICurrentSelectStatementTracker<SqlSelectStatement> currentSelectStatement;


        /// <summary>
        /// Former isParentAJoinStack
        ///   Nested joins and extents need to know whether they should create
        ///   a new Select statement, or reuse the parent's.  This flag
        ///   indicates whether the parent is a join or not.
        /// </summary>
        Stack<bool> shouldCreateNewSubqueryForNestedExtentsStack;

        protected bool ShouldStartNewSubqueryWhenParentIsJoin
        {
            // There might be no entry on the stack if a Join node has never
            // been seen, so we return false in that case.
            get
            {
                return shouldCreateNewSubqueryForNestedExtentsStack.Count == 0
                           ? false
                           : shouldCreateNewSubqueryForNestedExtentsStack.Peek();
            }
        }

        #endregion


        #region Visitor lists and state

        readonly NamingScopes namingScopes = new NamingScopes();
        Dictionary<string, int> allColumnNames;
        Dictionary<string, int> allExtentNames;

        /// <summary>
        ///   VariableReferenceExpressions are allowed only as children of DbPropertyExpression
        ///   or MethodExpression.  The cheapest way to ensure this is to set the following
        ///   property in DbVariableReferenceExpression and reset it in the allowed parent expressions.
        /// </summary>
        bool isVariableReferenceExpressionChildOfPropertyOrMethodExpression;

        /// <summary>
        /// For table alias rename to make them unique
        /// int value is an incremental alias suffix if it is > 0
        /// </summary>
        internal Dictionary<string, int> AllExtentNames
        {
            get { return allExtentNames; }
        }

        // For each column name, we store the last integer suffix that
        // was added to produce a unique column name.  This speeds up
        // the creation of the next unique name for this column name.

        /// <summary>
        /// For column aliases rename to make them unique
        /// int value is an incremental alias suffix if it is > 0
        /// </summary>
        internal Dictionary<string, int> AllColumnNames
        {
            get { return allColumnNames; }
        }

        #endregion


        #region Statics

        static readonly Dictionary<string, FunctionHandler> _builtInFunctionHandlers =
            InitializeBuiltInFunctionHandlers();

        static readonly Dictionary<string, FunctionHandler> _canonicalFunctionHandlers =
            InitializeCanonicalFunctionHandlers();

        static readonly Dictionary<string, string> _functionNameToOperatorDictionary =
            InitializeFunctionNameToOperatorDictionary();

        static readonly Dictionary<string, string> _dateAddFunctionNameToDatepartDictionary =
            InitializeDateAddFunctionNameToDatepartDictionary();

        static readonly Dictionary<string, string> _dateDiffFunctionNameToDatepartDictionary =
            InitializeDateDiffFunctionNameToDatepartDictionary();

        static readonly HashSet<string> _datepartKeywords = InitializeDatepartKeywords();

        static readonly char[] hexDigits = {
                                               '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E',
                                               'F'
                                           };

        /// <summary>
        ///   All special built-in functions and their handlers
        /// </summary>
        /// <returns></returns>
        static Dictionary<string, FunctionHandler> InitializeBuiltInFunctionHandlers()
        {
            Dictionary<string, FunctionHandler> functionHandlers = new Dictionary<string, FunctionHandler>
                (5, StringComparer.Ordinal);
            functionHandlers.Add("concat", HandleConcatFunction);
            functionHandlers.Add("dateadd", HandleDatepartDateFunction);
            functionHandlers.Add("datediff", HandleDatepartDateFunction);
            functionHandlers.Add("datename", HandleDatepartDateFunction);
            functionHandlers.Add("datepart", HandleDatepartDateFunction);
            return functionHandlers;
        }

        /// <summary>
        ///   All special non-aggregate canonical functions and their handlers
        /// </summary>
        /// <returns></returns>
        static Dictionary<string, FunctionHandler> InitializeCanonicalFunctionHandlers()
        {
            Dictionary<string, FunctionHandler> functionHandlers = new Dictionary<string, FunctionHandler>
                (51, StringComparer.Ordinal);
            functionHandlers.Add("IndexOf", HandleCanonicalFunctionIndexOf);
            functionHandlers.Add("Length", HandleCanonicalFunctionLength);
            functionHandlers.Add("NewGuid", HandleCanonicalFunctionNewGuid);
            functionHandlers.Add("Round", HandleCanonicalFunctionRound);
            functionHandlers.Add("Truncate", HandleCanonicalFunctionTruncate);
            functionHandlers.Add("Abs", HandleCanonicalFunctionAbs);
            functionHandlers.Add("ToLower", HandleCanonicalFunctionToLower);
            functionHandlers.Add("ToUpper", HandleCanonicalFunctionToUpper);
            functionHandlers.Add("Trim", HandleCanonicalFunctionTrim);
            functionHandlers.Add("Contains", HandleCanonicalFunctionContains);
            functionHandlers.Add("StartsWith", HandleCanonicalFunctionStartsWith);
            functionHandlers.Add("EndsWith", HandleCanonicalFunctionEndsWith);

            //DateTime Functions
            functionHandlers.Add("Year", HandleCanonicalFunctionDatepart);
            functionHandlers.Add("Month", HandleCanonicalFunctionDatepart);
            functionHandlers.Add("Day", HandleCanonicalFunctionDatepart);
            functionHandlers.Add("Hour", HandleCanonicalFunctionDatepart);
            functionHandlers.Add("Minute", HandleCanonicalFunctionDatepart);
            functionHandlers.Add("Second", HandleCanonicalFunctionDatepart);
            functionHandlers.Add("Millisecond", HandleCanonicalFunctionDatepart);
            functionHandlers.Add("DayOfYear", HandleCanonicalFunctionDatepart);
            functionHandlers.Add("CurrentDateTime", HandleCanonicalFunctionCurrentDateTime);
            functionHandlers.Add("CurrentUtcDateTime", HandleCanonicalFunctionCurrentUtcDateTime);
            functionHandlers.Add("CurrentDateTimeOffset", HandleCanonicalFunctionCurrentDateTimeOffset);
            functionHandlers.Add("GetTotalOffsetMinutes", HandleCanonicalFunctionGetTotalOffsetMinutes);
            functionHandlers.Add("TruncateTime", HandleCanonicalFunctionTruncateTime);
            functionHandlers.Add("CreateDateTime", HandleCanonicalFunctionCreateDateTime);
            functionHandlers.Add("CreateDateTimeOffset", HandleCanonicalFunctionCreateDateTimeOffset);
            functionHandlers.Add("CreateTime", HandleCanonicalFunctionCreateTime);
            functionHandlers.Add("AddYears", HandleCanonicalFunctionDateAdd);
            functionHandlers.Add("AddMonths", HandleCanonicalFunctionDateAdd);
            functionHandlers.Add("AddDays", HandleCanonicalFunctionDateAdd);
            functionHandlers.Add("AddHours", HandleCanonicalFunctionDateAdd);
            functionHandlers.Add("AddMinutes", HandleCanonicalFunctionDateAdd);
            functionHandlers.Add("AddSeconds", HandleCanonicalFunctionDateAdd);
            functionHandlers.Add("AddMilliseconds", HandleCanonicalFunctionDateAdd);
            functionHandlers.Add("AddMicroseconds", HandleCanonicalFunctionDateAddKatmaiOrNewer);
            functionHandlers.Add("AddNanoseconds", HandleCanonicalFunctionDateAddKatmaiOrNewer);
            functionHandlers.Add("DiffYears", HandleCanonicalFunctionDateDiff);
            functionHandlers.Add("DiffMonths", HandleCanonicalFunctionDateDiff);
            functionHandlers.Add("DiffDays", HandleCanonicalFunctionDateDiff);
            functionHandlers.Add("DiffHours", HandleCanonicalFunctionDateDiff);
            functionHandlers.Add("DiffMinutes", HandleCanonicalFunctionDateDiff);
            functionHandlers.Add("DiffSeconds", HandleCanonicalFunctionDateDiff);
            functionHandlers.Add("DiffMilliseconds", HandleCanonicalFunctionDateDiff);
            functionHandlers.Add("DiffMicroseconds", HandleCanonicalFunctionDateDiffKatmaiOrNewer);
            functionHandlers.Add("DiffNanoseconds", HandleCanonicalFunctionDateDiffKatmaiOrNewer);

            //Functions that translate to operators
            functionHandlers.Add("Concat", HandleConcatFunction);
            functionHandlers.Add("BitwiseAnd", HandleCanonicalFunctionBitwise);
            functionHandlers.Add("BitwiseNot", HandleCanonicalFunctionBitwise);
            functionHandlers.Add("BitwiseOr", HandleCanonicalFunctionBitwise);
            functionHandlers.Add("BitwiseXor", HandleCanonicalFunctionBitwise);

            return functionHandlers;
        }

        /// <summary>
        ///   Valid datepart values
        /// </summary>
        /// <returns></returns>
        static HashSet<string> InitializeDatepartKeywords()
        {
            #region Datepart Keywords

            //
            // valid datepart values
            //
            HashSet<string> datepartKeywords = new HashSet<string>(StringComparer.Ordinal);
            datepartKeywords.Add("d");
            datepartKeywords.Add("day");
            datepartKeywords.Add("dayofyear");
            datepartKeywords.Add("dd");
            datepartKeywords.Add("dw");
            datepartKeywords.Add("dy");
            datepartKeywords.Add("hh");
            datepartKeywords.Add("hour");
            datepartKeywords.Add("m");
            datepartKeywords.Add("mi");
            datepartKeywords.Add("millisecond");
            datepartKeywords.Add("minute");
            datepartKeywords.Add("mm");
            datepartKeywords.Add("month");
            datepartKeywords.Add("ms");
            datepartKeywords.Add("n");
            datepartKeywords.Add("q");
            datepartKeywords.Add("qq");
            datepartKeywords.Add("quarter");
            datepartKeywords.Add("s");
            datepartKeywords.Add("second");
            datepartKeywords.Add("ss");
            datepartKeywords.Add("week");
            datepartKeywords.Add("weekday");
            datepartKeywords.Add("wk");
            datepartKeywords.Add("ww");
            datepartKeywords.Add("y");
            datepartKeywords.Add("year");
            datepartKeywords.Add("yy");
            datepartKeywords.Add("yyyy");
            return datepartKeywords;

            #endregion
        }

        /// <summary>
        ///   Initializes the mapping from functions to T-SQL operators
        ///   for all functions that translate to T-SQL operators
        /// </summary>
        /// <returns></returns>
        static Dictionary<string, string> InitializeFunctionNameToOperatorDictionary()
        {
            Dictionary<string, string> functionNameToOperatorDictionary = new Dictionary<string, string>
                (5, StringComparer.Ordinal);
            functionNameToOperatorDictionary.Add("Concat", "+"); //canonical
            functionNameToOperatorDictionary.Add("CONCAT", "+"); //store
            functionNameToOperatorDictionary.Add("BitwiseAnd", "&");
            functionNameToOperatorDictionary.Add("BitwiseNot", "~");
            functionNameToOperatorDictionary.Add("BitwiseOr", "|");
            functionNameToOperatorDictionary.Add("BitwiseXor", "^");
            return functionNameToOperatorDictionary;
        }

        /// <summary>
        ///   Initalizes the mapping from names of canonical function for date/time addition
        ///   to corresponding dateparts
        /// </summary>
        /// <returns></returns>
        static Dictionary<string, string> InitializeDateAddFunctionNameToDatepartDictionary()
        {
            Dictionary<string, string> dateAddFunctionNameToDatepartDictionary = new Dictionary<string, string>
                (5, StringComparer.Ordinal);
            dateAddFunctionNameToDatepartDictionary.Add("AddYears", "year");
            dateAddFunctionNameToDatepartDictionary.Add("AddMonths", "month");
            dateAddFunctionNameToDatepartDictionary.Add("AddDays", "day");
            dateAddFunctionNameToDatepartDictionary.Add("AddHours", "hour");
            dateAddFunctionNameToDatepartDictionary.Add("AddMinutes", "minute");
            dateAddFunctionNameToDatepartDictionary.Add("AddSeconds", "second");
            dateAddFunctionNameToDatepartDictionary.Add("AddMilliseconds", "millisecond");
            dateAddFunctionNameToDatepartDictionary.Add("AddMicroseconds", "microsecond");
            dateAddFunctionNameToDatepartDictionary.Add("AddNanoseconds", "nanosecond");
            return dateAddFunctionNameToDatepartDictionary;
        }

        /// <summary>
        ///   Initalizes the mapping from names of canonical function for date/time difference
        ///   to corresponding dateparts
        /// </summary>
        /// <returns></returns>
        static Dictionary<string, string> InitializeDateDiffFunctionNameToDatepartDictionary()
        {
            Dictionary<string, string> dateDiffFunctionNameToDatepartDictionary = new Dictionary<string, string>
                (5, StringComparer.Ordinal);
            dateDiffFunctionNameToDatepartDictionary.Add("DiffYears", "year");
            dateDiffFunctionNameToDatepartDictionary.Add("DiffMonths", "month");
            dateDiffFunctionNameToDatepartDictionary.Add("DiffDays", "day");
            dateDiffFunctionNameToDatepartDictionary.Add("DiffHours", "hour");
            dateDiffFunctionNameToDatepartDictionary.Add("DiffMinutes", "minute");
            dateDiffFunctionNameToDatepartDictionary.Add("DiffSeconds", "second");
            dateDiffFunctionNameToDatepartDictionary.Add("DiffMilliseconds", "millisecond");
            dateDiffFunctionNameToDatepartDictionary.Add("DiffMicroseconds", "microsecond");
            dateDiffFunctionNameToDatepartDictionary.Add("DiffNanoseconds", "nanosecond");
            return dateDiffFunctionNameToDatepartDictionary;
        }

        delegate ISqlFragment FunctionHandler(SqlGenerator sqlgen,
                                              DbFunctionExpression functionExpr);

        #endregion


        #region StoreVersion

        readonly StoreVersion storeVersion;

        internal StoreVersion StoreVersion
        {
            get { return storeVersion; }
        }

        internal bool IsPreKatmai
        {
            get { return StoreVersion == StoreVersion.Sql9; }
        }

        #endregion


        #region Constructor

        /// <summary>
        ///   Basic constructor.
        /// </summary>
        /// <param name = "storeVersion">server version</param>
        public SqlGenerator(StoreVersion storeVersion)
        {
            this.storeVersion = storeVersion;
        }

        #endregion


        #region Entry points

        /// <summary>
        ///   General purpose static function that can be called from System.Data assembly
        /// </summary>
        ///<param name = "commandTree">
        /// I bet it is an output command tree root that EF DB specific provider deals with
        /// command tree
        /// </param>
        /// <param name = "version">version</param>
        /// <param name = "parameters">Parameters to add to the command tree corresponding
        ///   to constants in the command tree. Used only in ModificationCommandTrees.</param>
        /// <returns>The string representing the SQL to be executed.</returns>
        public static string GenerateSql
            (
                DbCommandTree commandTree,
                StoreVersion version,
                out List<DbParameter> parameters,
                out CommandType commandType
            )
        {
            commandType = CommandType.Text;

            //Handle Query
            var queryCommandTree = commandTree as DbQueryCommandTree;
            if (queryCommandTree != null)
            {
                var sqlGen = new SqlGenerator(version);
                parameters = null;
                return sqlGen.GenerateSql((DbQueryCommandTree)commandTree);
            }

            //Handle Function
            var DbFunctionCommandTree = commandTree as DbFunctionCommandTree;
            if (DbFunctionCommandTree != null)
            {
                var sqlGen = new SqlGenerator(version);
                parameters = null;

                string sql = sqlGen.GenerateFunctionSql(DbFunctionCommandTree, out commandType);

                return sql;
            }

            //Handle Insert
            var insertCommandTree = commandTree as DbInsertCommandTree;
            if (insertCommandTree != null)
            {
                return DmlSqlGenerator.GenerateInsertSql(insertCommandTree, out parameters);
            }

            //Handle Delete
            var deleteCommandTree = commandTree as DbDeleteCommandTree;
            if (deleteCommandTree != null)
            {
                return DmlSqlGenerator.GenerateDeleteSql(deleteCommandTree, out parameters);
            }

            //Handle Update
            var updateCommandTree = commandTree as DbUpdateCommandTree;
            if (updateCommandTree != null)
            {
                return DmlSqlGenerator.GenerateUpdateSql(updateCommandTree, out parameters);
            }

            throw new NotSupportedException("Unrecognized command tree type");
        }

        #endregion


        #region Driver Methods

        ///<summary>
        ///  Translate a command tree to a SQL string.
        ///
        ///  The input tree could be translated to either a SQL SELECT statement
        ///  or a SELECT expression.  This choice is made based on the return type
        ///  of the expression
        ///  CollectionType => select statement
        ///  non collection type => select expression
        ///</summary>
        ///<param name = "tree">
        /// I bet it is an output command tree root that EF DB specific provider deals with
        /// command tree
        /// </param>
        ///<returns>The string representing the SQL to be executed.</returns>
        public string GenerateSql(DbQueryCommandTree tree)
        {
            var targetExperssion = tree.Query;
            InitFields();
            // Literals will not be converted to parameters.
            ISqlFragment result;
            if (MetadataHelpers.IsCollectionType(targetExperssion.ResultType))
            {
                result = CreateSqlSelectStatement(targetExperssion);
            }
            else
            {
                result = CreateSelectSqlExpression(targetExperssion);
            }
            if (isVariableReferenceExpressionChildOfPropertyOrMethodExpression)
            {
                throw CreateVariableExprHasToBeChildOfPropertyOrMethodExprException();
            }
		    AssertParameterStacksAreNotLeaking();
            result = OnVisitComplete(result);
            //2nd pass - renames and final SQL generation
            string sql = WriteSql(result);
            return sql;
        }

        /// <summary>
        /// Happens before 2nd phase of SQL generation (before alias renaming)
        /// SqlSelect statement is built by this time, the next step is WriteSql()
        /// </summary>
        protected virtual ISqlFragment OnVisitComplete(ISqlFragment sqlFragment)
        {
            return sqlFragment;
        }

        void AssertParameterStacksAreNotLeaking()
        { // Check that the parameter stacks are not leaking.

            Contract.Assert(currentSelectStatement.IsEmpty);
            Contract.Assert(shouldCreateNewSubqueryForNestedExtentsStack.Count == 0);
        }

        NotSupportedException CreateVariableExprHasToBeChildOfPropertyOrMethodExprException()
        {
            return new NotSupportedException(
                "A DbVariableReferenceExpression has to be a child of DbPropertyExpression or MethodExpression");
        }

        ISqlFragment CreateSelectSqlExpression(DbExpression targetExperssion)
        {
            var sqlBuilder = new SqlBuilder();
            OnAppendQueryHeader(sqlBuilder);
            sqlBuilder.Append("SELECT ");
            sqlBuilder.Append(targetExperssion.Accept(this));

            return sqlBuilder;
        }

        /// <summary>
        /// Override to add a query header before select (e.g. with CTE)
        /// </summary>
        protected virtual void OnAppendQueryHeader(SqlBuilder sqlBuilder)
        {
        }

        ISqlFragment CreateSqlSelectStatement(DbExpression targetExperssion)
        {
            var sqlStatement = VisitExpressionEnsureSqlStatement(targetExperssion);
            Debug.Assert(sqlStatement != null, "The outer most sql statement is null");
            sqlStatement.IsTopMost = true;
            return sqlStatement;
        }

        void InitFields()
        {
            currentSelectStatement = CreateCurrentSelectStatementTracker();
            shouldCreateNewSubqueryForNestedExtentsStack = new Stack<bool>();

            allExtentNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            allColumnNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        protected virtual ICurrentSelectStatementTracker<SqlSelectStatement> 
            CreateCurrentSelectStatementTracker()
        {
            return new CurrentSelectStatementTracker(namingScopes);
        }

        /// <summary>
        ///   Translate a function command tree to a SQL string.
        /// </summary>
        string GenerateFunctionSql
            (
            DbFunctionCommandTree tree,
            out CommandType commandType
            )
        {
            EdmFunction function = tree.EdmFunction;

            // We expect function to always have these properties
            string userCommandText = (string)function.MetadataProperties["CommandTextAttribute"].Value;
            string userSchemaName = (string)function.MetadataProperties["Schema"].Value;
            string userFuncName = (string)function.MetadataProperties["StoreFunctionNameAttribute"].Value;

            if (String.IsNullOrEmpty(userCommandText))
            {
                // build a quoted description of the function
                commandType = CommandType.StoredProcedure;

                // if the schema name is not explicitly given, it is assumed to be the metadata namespace
                string schemaName = String.IsNullOrEmpty(userSchemaName)
                                        ? function.NamespaceName
                                        : userSchemaName;

                // if the function store name is not explicitly given, it is assumed to be the metadata name
                string functionName = String.IsNullOrEmpty(userFuncName)
                                          ? function.Name
                                          : userFuncName;

                // quote elements of function text
                string quotedSchemaName = QuoteIdentifier(schemaName);
                string quotedFunctionName = QuoteIdentifier(functionName);

                // separator
                const string schemaSeparator = ".";

                // concatenate elements of function text
                string quotedFunctionText = quotedSchemaName + schemaSeparator + quotedFunctionName;

                return quotedFunctionText;
            }
            else
            {
                // if the user has specified the command text, pass it through verbatim and choose CommandType.Text
                commandType = CommandType.Text;
                return userCommandText;
            }
        }

        /// <summary>
        ///   Convert the SQL fragments to a string.
        ///   We have to setup the Stream for writing.
        /// </summary>
        /// <param name = "sqlStatement"></param>
        /// <returns>A string representing the SQL to be executed.</returns>
        string WriteSql(ISqlFragment sqlStatement)
        {
            var builder = new StringBuilder(1024);
            using (var writer = new SqlWriter(builder))
            {
                sqlStatement.WriteSql(writer, this);
            }
            return builder.ToString();
        }

        #endregion


        /// <summary>
        ///   Translate(left) AND Translate(right)
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlBuilder" />.</returns>
        public override ISqlFragment Visit(DbAndExpression e)
        {
            return VisitBinaryExpression("\r\n AND ", e.Left, e.Right);
        }

        /// <summary>
        ///   An apply is just like a join, so it shares the common join processing
        ///   in <see cref = "VisitJoinExpression" />
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlSelectStatement" />.</returns>
        public override ISqlFragment Visit(DbApplyExpression e)
        {
            throw new NotSupportedException("DbApplyExpression is not supported");

#if UsedForSqlGeneration
            List<DbExpressionBinding> inputs = new List<DbExpressionBinding>();
            inputs.Add(e.Input);
            inputs.Add(e.Apply);

            string joinString;
            switch (e.ExpressionKind)
            {
                case DbExpressionKind.CrossApply :
                    joinString = "CROSS APPLY";
                    break;

                case DbExpressionKind.OuterApply :
                    joinString = "OUTER APPLY";
                    break;

                default :
                    Debug.Assert(false);
                    throw new InvalidOperationException();
            }

            // The join condition does not exist in this case, so we use null.
            // We do not have a on clause, so we use JoinType.CrossJoin.
            return VisitJoinExpression(inputs, DbExpressionKind.CrossJoin, joinString, null);
#endif
        }

        /// <summary>
        ///   For binary expressions, we delegate to <see cref = "VisitBinaryExpression" />.
        ///   We handle the other expressions directly.
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlBuilder" /></returns>
        public override ISqlFragment Visit(DbArithmeticExpression e)
        {
            SqlBuilder result;

            switch (e.ExpressionKind)
            {
                case DbExpressionKind.Divide :
                    result = VisitBinaryExpression(" / ", e.Arguments[0], e.Arguments[1]);
                    break;
                case DbExpressionKind.Minus :
                    result = VisitBinaryExpression(" - ", e.Arguments[0], e.Arguments[1]);
                    break;
                case DbExpressionKind.Modulo :
                    result = VisitBinaryExpression(" % ", e.Arguments[0], e.Arguments[1]);
                    break;
                case DbExpressionKind.Multiply :
                    result = VisitBinaryExpression(" * ", e.Arguments[0], e.Arguments[1]);
                    break;
                case DbExpressionKind.Plus :
                    result = VisitBinaryExpression(" + ", e.Arguments[0], e.Arguments[1]);
                    break;

                case DbExpressionKind.UnaryMinus :
                    result = new SqlBuilder();
                    result.Append(" -(");
                    result.Append(e.Arguments[0].Accept(this));
                    result.Append(")");
                    break;

                default :
                    Debug.Assert(false);
                    throw new InvalidOperationException();
            }

            return result;
        }

        /// <summary>
        ///   If the ELSE clause is null, we do not write it out.
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlBuilder" /></returns>
        public override ISqlFragment Visit(DbCaseExpression e)
        {
            //TODO: implement expressions (v2)
			Debug.Assert(e.When.Count == e.Then.Count);
            var result = new SqlBuilder();

            result.Append("CASE");
            for (int i = 0; i < e.When.Count; ++i)
            {
                result.Append(" WHEN (");
                result.Append(e.When[i].Accept(this));
                result.Append(") THEN ");
                result.Append(e.Then[i].Accept(this));
            }
            if (e.Else != null && !( e.Else is DbNullExpression ))
            {
                result.Append(" ELSE ");
                result.Append(e.Else.Accept(this));
            }

            result.Append(" END");

            return result;
        }

        ///<summary>
        ///</summary>
        ///<param name = "e"></param>
        ///<returns></returns>
        public override ISqlFragment Visit(DbCastExpression e)
        {
            var result = new SqlBuilder();
            result.Append(" CAST( ");
            result.Append(e.Argument.Accept(this));
            result.Append(" AS ");
            result.Append(GetSqlPrimitiveType(e.ResultType));
            result.Append(")");

            return result;
        }

        /// <summary>
        ///   The parser generates Not(Equals(...)) for &lt;&gt;.
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlBuilder" />.</returns>
        public override ISqlFragment Visit(DbComparisonExpression e)
        {
            SqlBuilder result;
            switch (e.ExpressionKind)
            {
                case DbExpressionKind.Equals :
                    result = VisitBinaryExpression(" = ", e.Left, e.Right);
                    break;
                case DbExpressionKind.LessThan :
                    result = VisitBinaryExpression(" < ", e.Left, e.Right);
                    break;
                case DbExpressionKind.LessThanOrEquals :
                    result = VisitBinaryExpression(" <= ", e.Left, e.Right);
                    break;
                case DbExpressionKind.GreaterThan :
                    result = VisitBinaryExpression(" > ", e.Left, e.Right);
                    break;
                case DbExpressionKind.GreaterThanOrEquals :
                    result = VisitBinaryExpression(" >= ", e.Left, e.Right);
                    break;
                    // The parser does not generate the expression kind below.
                case DbExpressionKind.NotEquals :
                    result = VisitBinaryExpression(" <> ", e.Left, e.Right);
                    break;

                default :
                    Debug.Assert(false); // The constructor should have prevented this
                    throw new InvalidOperationException(String.Empty);
            }

            return result;
        }

        /// <summary>
        ///   Constants will be send to the store as part of the generated TSQL, not as parameters
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlBuilder" />.  Strings are wrapped in single
        ///   quotes and escaped.  Numbers are written literally.</returns>
        public override ISqlFragment Visit(DbConstantExpression e)
        {
            return VisitConstantExpression(e.ResultType, e.Value);
        }

        ISqlFragment VisitConstantExpression
            (
            TypeUsage expressionType,
            object expressionValue)
        {
            var result = new SqlBuilder();

            PrimitiveTypeKind typeKind;
            // Model Types can be (at the time of this implementation):
            //      Binary, Boolean, Byte, DateTime, Decimal, Double, Guid, Int16, Int32, Int64,Single, String
            if (MetadataHelpers.TryGetPrimitiveTypeKind(expressionType, out typeKind))
            {
                switch (typeKind)
                {
                    case PrimitiveTypeKind.Int32 :
                        // default sql server type for integral values.
                        result.Append(expressionValue.ToString());
                        break;

                    case PrimitiveTypeKind.Binary :
                        result.Append(" 0x");
                        result.Append(ByteArrayToBinaryString((Byte[])expressionValue));
                        result.Append(" ");
                        break;

                    case PrimitiveTypeKind.Boolean :
                        result.Append
                            (
                                (bool)expressionValue
                                    ? "cast(1 as bit)"
                                    : "cast(0 as bit)");
                        break;

                    case PrimitiveTypeKind.Byte :
                        result.Append("cast(");
                        result.Append(expressionValue.ToString());
                        result.Append(" as tinyint)");
                        break;

                    case PrimitiveTypeKind.DateTime :
                        result.Append("convert(");
                        result.Append
                            (
                                IsPreKatmai
                                    ? "datetime"
                                    : "datetime2");
                        result.Append(", ");
                        result.Append
                            (
                                EscapeSingleQuote
                                    (
                                        ( (System.DateTime)expressionValue ).ToString
                                            (
                                                IsPreKatmai
                                                    ? "yyyy-MM-dd HH:mm:ss.fff"
                                                    : "yyyy-MM-dd HH:mm:ss.fffffff",
                                                CultureInfo.InvariantCulture),
                                        false /* IsUnicode */));
                        result.Append(", 121)");
                        break;

                    case PrimitiveTypeKind.Time :
                        AssertKatmaiOrNewer(typeKind);
                        result.Append("convert(");
                        result.Append(expressionType.EdmType.Name);
                        result.Append(", ");
                        result.Append(EscapeSingleQuote(expressionValue.ToString(), false /* IsUnicode */));
                        result.Append(", 121)");
                        break;

                    case PrimitiveTypeKind.DateTimeOffset :
                        AssertKatmaiOrNewer(typeKind);
                        result.Append("convert(");
                        result.Append(expressionType.EdmType.Name);
                        result.Append(", ");
                        result.Append
                            (
                                EscapeSingleQuote
                                    (
                                        ( (System.DateTimeOffset)expressionValue ).ToString
                                            ("yyyy-MM-dd HH:mm:ss.fffffff zzz", CultureInfo.InvariantCulture),
                                        false /* IsUnicode */));
                        result.Append(", 121)");
                        break;


                    case PrimitiveTypeKind.Decimal :
                        string strDecimal = ( (Decimal)expressionValue ).ToString(CultureInfo.InvariantCulture);
                        // if the decimal value has no decimal part, cast as decimal to preserve type
                        // if the number has precision > int64 max precision, it will be handled as decimal by sql server
                        // and does not need cast. if precision is lest then 20, then cast using Max(literal precision, sql default precision)
                        if (-1 == strDecimal.IndexOf('.') && ( strDecimal.TrimStart(new[] {'-'}).Length < 20 ))
                        {
                            byte precision = (Byte)strDecimal.Length;
                            FacetDescription precisionFacetDescription;
                            Debug.Assert
                                (
                                    MetadataHelpers.TryGetTypeFacetDescriptionByName
                                        (expressionType.EdmType, "precision", out precisionFacetDescription),
                                    "Decimal primitive type must have Precision facet");
                            if (MetadataHelpers.TryGetTypeFacetDescriptionByName
                                (expressionType.EdmType, "precision", out precisionFacetDescription))
                            {
                                if (precisionFacetDescription.DefaultValue != null)
                                {
                                    precision = Math.Max(precision, (byte)precisionFacetDescription.DefaultValue);
                                }
                            }
                            Debug.Assert(precision > 0, "Precision must be greater than zero");
                            result.Append("cast(");
                            result.Append(strDecimal);
                            result.Append(" as decimal(");
                            result.Append(precision.ToString(CultureInfo.InvariantCulture));
                            result.Append("))");
                        }
                        else
                        {
                            result.Append(strDecimal);
                        }
                        break;

                    case PrimitiveTypeKind.Double :
                        result.Append("cast(");
                        result.Append(( (Double)expressionValue ).ToString(CultureInfo.InvariantCulture));
                        result.Append(" as float(53))");
                        break;

                    case PrimitiveTypeKind.Guid :
                        result.Append("cast(");
                        result.Append(EscapeSingleQuote(expressionValue.ToString(), false /* IsUnicode */));
                        result.Append(" as uniqueidentifier)");
                        break;

                    case PrimitiveTypeKind.Int16 :
                        result.Append("cast(");
                        result.Append(expressionValue.ToString());
                        result.Append(" as smallint)");
                        break;

                    case PrimitiveTypeKind.Int64 :
                        result.Append("cast(");
                        result.Append(expressionValue.ToString());
                        result.Append(" as bigint)");
                        break;

                    case PrimitiveTypeKind.Single :
                        result.Append("cast(");
                        result.Append(( (Single)expressionValue ).ToString(CultureInfo.InvariantCulture));
                        result.Append(" as real)");
                        break;

                    case PrimitiveTypeKind.String :
                        VisitStringConstantExpression(result, expressionType, expressionValue);
                        break;

                    default :
                        // all known scalar types should been handled already.
                        throw new NotSupportedException
                            ("Primitive type kind " + typeKind + " is not supported by the Sample Provider");
                }
            }
            else
            {
                throw new NotSupportedException();
            }

            return result;
        }

        protected virtual void VisitStringConstantExpression(SqlBuilder result,
                                            TypeUsage expressionType,
                                           object expressionValue)
        {
            bool isUnicode = MetadataHelpers.GetFacetValueOrDefault
                (expressionType, MetadataHelpers.UnicodeFacetName, true);

            result.Append(EscapeSingleQuote(expressionValue as string, isUnicode));
        }

        /// <summary>
        ///   <see cref = "DbDerefExpression" /> is illegal at this stage
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        public override ISqlFragment Visit(DbDerefExpression e)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        ///   The DISTINCT has to be added to the beginning of SqlSelectStatement.Select,
        ///   but it might be too late for that.  So, we use a flag on SqlSelectStatement
        ///   instead, and add the "DISTINCT" in the second phase.
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlSelectStatement" /></returns>
        public override ISqlFragment Visit(DbDistinctExpression e)
        {
            //TODO: add support for DbDistinctExpression in MDX
            var result = VisitExpressionEnsureSqlStatement(e.Argument);
            if (IsCompatibleWithCurrentSelectStatement(result, e.ExpressionKind))
            {
                result.IsDistinct = true;
                return result;
            }
            AliasOrSubquery fromAliasOrSubquery;
            TypeUsage inputType = MetadataHelpers.GetElementTypeUsage(e.Argument.ResultType);
            result = CreateNewSelectStatement(result, "distinct", inputType, out fromAliasOrSubquery);
            AddAliasToFrom(result, "distinct", fromAliasOrSubquery, false);
            return result;
        }

        /// <summary>
        ///   An element expression returns a scalar - so it is translated to
        ///   ( Select ... )
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        public override ISqlFragment Visit(DbElementExpression e)
        {
            SqlBuilder result = new SqlBuilder();
            result.Append("(");
            result.Append(VisitExpressionEnsureSqlStatement(e.Argument));
            result.Append(")");

            return result;
        }

        /// <summary>
        ///   <see cref = "Visit(DbUnionAllExpression)" />
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        public override ISqlFragment Visit(DbExceptExpression e)
        {
            //TODO: implement EXCEPT() for MDX
            return VisitSetOpExpression(e.Left, e.Right, "EXCEPT");
        }

        /// <summary>
        ///   Only concrete expression types will be visited.
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        public override ISqlFragment Visit(DbExpression e)
        {
            throw new InvalidOperationException("Only concrete expression types are allowed");
        }

        /// <summary>
        /// When used in output command trees, 
        /// the DbScanExpression effectively represents a scan over a table, a view, or a store query, 
        /// represented by EnitySetBase::Target.
        /// </summary>
        ///<returns>
        /// If we are in a Join context, returns a <see cref = "SqlBuilder" />
        ///  with the extent name, otherwise, a new <see cref = "SqlSelectStatement" />
        ///  with the From field set.
        /// </returns>
        public override ISqlFragment Visit(DbScanExpression expression)
        {
            EntitySetBase target = expression.Target;
            if (ShouldStartNewSubqueryWhenParentIsJoin)
            {
                var result = new SqlBuilder();
                result.Append(GetEscapedEntityTableName(target));

                return result;
            }
            else
            {
                var result = CreateSelectStatement();
                result.From.Append(GetEscapedEntityTableName(target));

                return result;
            }
        }

        protected virtual SqlSelectStatement CreateSelectStatement()
        {
            return new SqlSelectStatement();
        }

        /// <summary>
        /// For DmlGenerator only, do not use this method!
        /// </summary>
        internal static string GetEscapedTableWithSchemaOrSubquery(EntitySetBase entitySetBase)
        {
            return ( new SqlGenerator(StoreVersion.Sql10) )
                .GetEscapedEntityTableName(entitySetBase);
        }

        /// <summary>
        /// Gets escaped "[schema].[TableName]" or "( &lt;subquery&gt; )" 
        /// TSql identifier describing this entity set.
        /// </summary>
        string GetEscapedEntityTableName(EntitySetBase entitySetBase)
        {
            // construct escaped T-SQL referencing entity set
            var builder = new StringBuilder(50);
            string definingQuery = GetDefiningQuery(entitySetBase);
            if (!string.IsNullOrEmpty(definingQuery))
            {
                return GetSubqueryInParantheses(definingQuery, builder);
            }
            AppendSchemaName(entitySetBase, builder);
            AppendTableName(entitySetBase, builder);
            return builder.ToString();
        }

        static string GetDefiningQuery(EntitySetBase entitySetBase)
        {
            return MetadataHelpers.TryGetValueForMetadataProperty<string>
                (entitySetBase, "DefiningQuery");
        }

        void AppendTableName(EntitySetBase entitySetBase,
                                    StringBuilder builder)
        {
            string tableName = GetQuotedTableNameFromEntitySet(entitySetBase);
            EntityToTableNameMap[entitySetBase.Name] = tableName;
            builder.Append(tableName);
        }

        string GetQuotedTableNameFromEntitySet(EntitySetBase entitySetBase)
        { //TODO: this method is duplicated in DbScanExpressionExtension. Should I move my related extension methods into SqlEFProvider?
            string tableName = GetTableNameFromEntitySet(entitySetBase);
            string result = ( !string.IsNullOrEmpty(tableName) )
                       ? QuoteIdentifier(tableName)
                       : QuoteIdentifier(entitySetBase.Name);

            return result;
        }

        Dictionary<string, string> entityToTableNameMap;
        protected Dictionary<string, string> EntityToTableNameMap
        {//TODO: I can move these property and field to MdxGenerator completely, they are not used for SQL
            get
            {
                return entityToTableNameMap 
                    ?? ( entityToTableNameMap = new Dictionary<string, string>() );
            }
        }


        static string GetTableNameFromEntitySet(EntitySetBase entitySetBase)
        {
            return MetadataHelpers.TryGetValueForMetadataProperty<string>(
                entitySetBase, "Table");
        }

        protected virtual void AppendSchemaName
            (
                EntitySetBase entitySetBase,
                StringBuilder builder
            )
        {
            string schemaName = GetSchema(entitySetBase);
            builder.Append(QuoteIdentifier(!string.IsNullOrEmpty(schemaName)
                                               ? schemaName
                                               : entitySetBase.EntityContainer.Name));
            builder.Append(".");
        }

        static string GetSchema(EntitySetBase entitySetBase)
        {
            return MetadataHelpers.TryGetValueForMetadataProperty<string>(
                entitySetBase, "Schema");
        }

        static string GetSubqueryInParantheses(string definingQuery,
                                               StringBuilder builder)
        {
            builder.Append("(");
            builder.Append(definingQuery);
            builder.Append(")");
            return builder.ToString();
        }

        /// <summary>
        ///   The bodies of <see cref = "Visit(DbFilterExpression)" />, <see cref = "Visit(DbGroupByExpression)" />,
        ///   <see cref = "Visit(DbProjectExpression)" />, <see cref = "Visit(DbSortExpression)" /> are similar.
        ///   Each does the following.
        ///   <list type = "number">
        ///     <item> Visit the input expression</item>
        ///     <item> Determine if the input's SQL statement can be reused, or a new
        ///       one must be created.</item>
        ///     <item>Create a new symbol table scope</item>
        ///     <item>Push the Sql statement onto a stack, so that children can
        ///       update the free variable list.</item>
        ///     <item>Visit the non-input expression.</item>
        ///     <item>Cleanup</item>
        ///   </list>
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlSelectStatement" /></returns>
        public override ISqlFragment Visit(DbFilterExpression e)
        {
            return VisitFilterExpression(e.Predicate, e.Input, false 
                /*Negate predicate*/);
        }

        /// <summary>
        ///   Lambda functions are not supported.
        ///   The functions supported are:
        ///   <list type = "number">
        ///     <item>Canonical Functions - We recognize these by their dataspace, it is DataSpace.CSpace</item>
        ///     <item>Store Functions - We recognize these by the BuiltInAttribute and not being Canonical</item>
        ///     <item>User-defined Functions - All the rest except for Lambda functions</item>
        ///   </list>
        ///   We handle Canonical and Store functions the same way: If they are in the list of functions 
        ///   that need special handling, we invoke the appropriate handler, otherwise we translate them to
        ///   FunctionName(arg1, arg2, ..., argn).
        ///   We translate user-defined functions to NamespaceName.FunctionName(arg1, arg2, ..., argn).
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlBuilder" /></returns>
        public override ISqlFragment Visit(DbFunctionExpression e)
        {
            //
            // check if function requires special case processing, if so, delegates to it
            //
            if (IsSpecialBuiltInFunction(e))
            {
                return HandleSpecialBuiltInFunction(e);
            }

            if (IsSpecialCanonicalFunction(e))
            {
                return HandleSpecialCanonicalFunction(e);
            }
            return HandleFunctionDefault(e);
        }

        /// <summary>
        ///   <see cref = "DbLambdaExpression" /> is illegal at this stage
        /// </summary>
        /// <param name = "expression"></param>
        /// <returns></returns>
        public override ISqlFragment Visit(DbLambdaExpression expression)
        {
            throw CreateProviderDealsWithOutputTreeExpressionsOnlyException();
        }

        NotSupportedException CreateProviderDealsWithOutputTreeExpressionsOnlyException()
        {
            return new NotSupportedException(
                "EF data providers deal with output tree expressions only");
        }

        /// <summary>
        ///   <see cref = "DbEntityRefExpression" /> is illegal at this stage
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        public override ISqlFragment Visit(DbEntityRefExpression e)
        {
            throw CreateProviderDealsWithOutputTreeExpressionsOnlyException();
        }

        /// <summary>
        ///   <see cref = "DbRefKeyExpression" /> is illegal at this stage
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        public override ISqlFragment Visit(DbRefKeyExpression e)
        {
            throw CreateProviderDealsWithOutputTreeExpressionsOnlyException();
        }



        protected class AggregationGeneratorWithInnerQuery
            : AggregationGeneratorStrategy
        {
            SqlSelectStatement innerQuery;
            
            public AggregationGeneratorWithInnerQuery
                (
                    DbGroupByExpression e
                    , SqlGenerator sqlGenerator
                ) : base(e, sqlGenerator)
            {
            }

            protected override ISqlFragment GetAggregateArgument(string alias,
                                              ISqlFragment translatedAggregateArgument)
            {
                ISqlFragment result;
                //In this case the argument to the aggregate is reference to the one projected out by the
                // inner query
                SqlBuilder wrappingAggregateArgument = new SqlBuilder();
                wrappingAggregateArgument.Append(fromAliasOrSubquery);
                wrappingAggregateArgument.Append(".");
                wrappingAggregateArgument.Append(alias);
                result = wrappingAggregateArgument;

                innerQuery.Select.Append(separator);
                innerQuery.Select.AppendLine();
                innerQuery.Select.Append(translatedAggregateArgument);
                innerQuery.Select.Append(" AS ");
                innerQuery.Select.Append(alias);
                return result;
            }

            protected override void AddGroupBy(
                ISqlFragment keySql, string alias)
            {
                // The inner query contains the default translation Key AS Alias
                innerQuery.Select.Append(separator);
                innerQuery.Select.AppendLine();
                innerQuery.Select.Append(keySql);
                innerQuery.Select.Append(" AS ");
                innerQuery.Select.Append(alias);

                //The outer resulting query projects over the key aliased in the inner query: 
                //  fromSymbol.Alias AS Alias
                Result.Select.Append(separator);
                Result.Select.AppendLine();
                Result.Select.Append(fromAliasOrSubquery);
                Result.Select.Append(".");
                Result.Select.Append(alias);
                Result.Select.Append(" AS ");
                Result.Select.Append(alias);

                Result.GroupBy.Append(alias);
            }

            protected override void ChooseResult()
            {
                innerQuery = currentQuery;
                //Create the inner query
                Result = generator.CreateNewSelectStatement
                    (
                        innerQuery,
                        expression.Input.VariableName,
                        expression.Input.VariableType,
                        false,
                        out fromAliasOrSubquery);

                generator.AddAliasToFrom(Result, expression.Input.VariableName, fromAliasOrSubquery, false);
            }

        }

        protected class AggregationGenerator
            : AggregationGeneratorStrategy
        {
            public AggregationGenerator
                (
                    DbGroupByExpression e
                    , SqlGenerator sqlGenerator
                )
                : base(e, sqlGenerator)
            {
            }

            protected override ISqlFragment GetAggregateArgument
                (
                    string alias
                    , ISqlFragment translatedAggregateArgument
                )
            {
                return translatedAggregateArgument;
            }

            protected override void AddGroupBy
                (
                    ISqlFragment keySql
                    , string alias
                )
            {
                //Default translation: Key AS Alias
                Result.Select.Append(separator);
                Result.Select.AppendLine();
                Result.Select.Append(keySql);
                Result.Select.Append(" AS ");
                Result.Select.Append(alias);

                Result.GroupBy.Append(keySql);
            }

            protected override void ChooseResult()
            {
                Result = currentQuery;
            }

        }

        protected abstract class AggregationGeneratorStrategy
        {
            protected SqlGenerator generator;
            protected DbGroupByExpression expression;

            protected string separator;
            protected AliasOrSubquery fromAliasOrSubquery;
            protected SqlSelectStatement Result { get; set; }
            protected SqlSelectStatement currentQuery;

            protected AggregationGeneratorStrategy
                (
                    DbGroupByExpression expression
                    , SqlGenerator sqlGenerator
                )
            {
                this.expression = expression;
                generator = sqlGenerator;
            }

            public static AggregationGeneratorStrategy Create
                (
                    DbGroupByExpression e
                    , SqlGenerator sqlGenerator
                )
            {
                if (IsArgumentRequiringInnerQuery(e.Aggregates))
                {
                    return new AggregationGeneratorWithInnerQuery(e, sqlGenerator);
                }
                return new AggregationGenerator(e, sqlGenerator);
            }

            public ISqlFragment GenerateAggregations()
            {
                currentQuery = generator.VisitInputCollectionExpression(expression.Input, out fromAliasOrSubquery);
                if (!generator.IsCompatibleWithCurrentSelectStatement(currentQuery, expression.ExpressionKind))
                {
                    currentQuery = generator.CreateNewSelectStatement
                        (currentQuery, expression.Input.VariableName, expression.Input.VariableType, out fromAliasOrSubquery);
                }
                generator.currentSelectStatement.Set(currentQuery);
                //TODO: should I move next 2 lines into CreateNewSelectStatement() ?:
                generator.AddAliasToFrom(currentQuery, expression.Input.VariableName, fromAliasOrSubquery);
                // This line is not present for other relational nodes.
                generator.namingScopes.Add(expression.Input.GroupVariableName, fromAliasOrSubquery);
                ChooseResult();
                // The enumerator is shared by both the keys and the aggregates,
                // so, we do not close it in between.
                using (IEnumerator<EdmProperty> members = expression.GetMetaProperties().GetEnumerator())
                {
                    members.MoveNext();
                    Debug.Assert(Result.Select.IsEmpty);

                    separator = "";
                    GenerateGroupByPerKey(members);
                    GenerateAggregateFunctions(members);
                }
                generator.currentSelectStatement.Pop();
                return Result;
            }

            protected abstract void ChooseResult();

            void GenerateAggregateFunctions(IEnumerator<EdmProperty> members)
            {
                foreach (DbAggregate aggregate in expression.Aggregates)
                {
                    Debug.Assert(aggregate.Arguments.Count == 1);

                    EdmProperty member = members.Current;
                    string alias = QuoteIdentifier(member.Name);
                    SqlBuilder aggregateResult = GetAggregateExpression(aggregate, alias);
                    AddAggregationExpressionIntoResult(aggregateResult, alias);
                    separator = ", ";
                    members.MoveNext();
                }
            }

            SqlBuilder GetAggregateExpression(DbAggregate aggregate,
                                              string alias)
            {
                var translatedAggregateArgument = aggregate.Arguments[0].Accept(generator);
                return generator.VisitAggregate(
                    aggregate, GetAggregateArgument(alias, translatedAggregateArgument));
            }

            protected virtual void AddAggregationExpressionIntoResult
                (
                    ISqlFragment aggregateResult
                    , string alias
                )
            {
                Result.Select.Append(separator);
                Result.Select.AppendLine();
                //this is where innerQuery may be added into result query:
                Result.Select.Append(aggregateResult); 
                Result.Select.Append(" AS ");
                Result.Select.Append(alias);
            }

            protected abstract ISqlFragment GetAggregateArgument(
                string alias, ISqlFragment translatedAggregateArgument);

            void GenerateGroupByPerKey(IEnumerator<EdmProperty> members)
            {
                foreach (DbExpression key in expression.Keys)
                {
                    EdmProperty member = members.Current;
                    string alias = QuoteIdentifier(member.Name);

                    Result.GroupBy.Append(separator);

                    ISqlFragment keySql = key.Accept(generator);

                    AddGroupBy(keySql, alias);

                    separator = ", ";
                    members.MoveNext();
                }
            }

            protected abstract void AddGroupBy(
                ISqlFragment keySql, string alias);
        }

        /// <summary>
        ///   <see cref = "Visit(DbFilterExpression)" /> for general details.
        ///   We modify both the GroupBy and the Select fields of the SqlSelectStatement.
        ///   GroupBy gets just the keys without aliases,
        ///   and Select gets the keys and the aggregates with aliases.
        /// 
        ///   Whenever there exists at least one aggregate with an argument that is not is not a simple
        ///   <see cref = "DbPropertyExpression" />  over <see cref = "DbVariableReferenceExpression" />, 
        ///   we create a nested query in which we alias the arguments to the aggregates. 
        ///   That is due to the following two limitations of Sql Server:
        ///   <list type = "number">
        ///     <item>If an expression being aggregated contains an outer reference, then that outer 
        ///       reference must be the only column referenced in the expression </item>
        ///     <item>Sql Server cannot perform an aggregate function on an expression containing 
        ///       an aggregate or a subquery. </item>
        ///   </list>
        /// 
        ///   The default translation, without inner query is: 
        /// 
        ///   SELECT 
        ///   kexp1 AS key1, kexp2 AS key2,... kexpn AS keyn, 
        ///   aggf1(aexpr1) AS agg1, .. aggfn(aexprn) AS aggn
        ///   FROM input AS a
        ///   GROUP BY kexp1, kexp2, .. kexpn
        /// 
        ///   When we inject an innner query, the equivalent translation is:
        /// 
        ///   SELECT 
        ///   key1 AS key1, key2 AS key2, .. keyn AS keys,  
        ///   aggf1(agg1) AS agg1, aggfn(aggn) AS aggn
        ///   FROM (
        ///   SELECT 
        ///   kexp1 AS key1, kexp2 AS key2,... kexpn AS keyn, 
        ///   aexpr1 AS agg1, .. aexprn AS aggn
        ///   FROM input AS a
        ///   ) as a
        ///   GROUP BY key1, key2, keyn
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlSelectStatement" /></returns>
        public override ISqlFragment Visit(DbGroupByExpression e)
        {
            return AggregationGeneratorStrategy.Create(e, this)
                .GenerateAggregations();
        }

        /// <summary>
        ///   <see cref = "Visit(DbUnionAllExpression)" />
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        public override ISqlFragment Visit(DbIntersectExpression e)
        {
            return VisitSetOpExpression(e.Left, e.Right, "INTERSECT");
        }

        ///<summary>
        ///  Not(IsEmpty) has to be handled specially, so we delegate to
        ///  <see cref = "VisitIsEmptyExpression" />.
        ///</summary>
        ///<param name = "e"></param>
        ///<returns>A <see cref = "SqlBuilder" />.
        ///  <code>[NOT] EXISTS( ... )</code>
        ///</returns>
        public override ISqlFragment Visit(DbIsEmptyExpression e)
        {
            return VisitIsEmptyExpression(e, false);
        }

        /// <summary>
        ///   Not(IsNull) is handled specially, so we delegate to
        ///   <see cref = "VisitIsNullExpression" />
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlBuilder" />
        ///   <code>IS [NOT] NULL</code>
        /// </returns>
        public override ISqlFragment Visit(DbIsNullExpression e)
        {
            return VisitIsNullExpression(e, false);
        }

        /// <summary>
        ///   <see cref = "DbIsOfExpression" /> is illegal at this stage
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlBuilder" /></returns>
        public override ISqlFragment Visit(DbIsOfExpression e)
        {
            throw CreateProviderDealsWithOutputTreeExpressionsOnlyException();
        }

        /// <summary>
        ///   <see cref = "VisitJoinExpression" />
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlSelectStatement" />.</returns>
        public override ISqlFragment Visit(DbCrossJoinExpression e)
        {
            return VisitJoinExpression(e.Inputs, e.ExpressionKind, "CROSS JOIN", null);
        }

        /// <summary>
        ///   <see cref = "VisitJoinExpression" />
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlSelectStatement" />.</returns>
        public override ISqlFragment Visit(DbJoinExpression e)
        {
            #region Map join type to a string

            string joinString;
            switch (e.ExpressionKind)
            {
                case DbExpressionKind.FullOuterJoin :
                    joinString = "FULL OUTER JOIN";
                    break;

                case DbExpressionKind.InnerJoin :
                    joinString = "INNER JOIN";
                    break;

                case DbExpressionKind.LeftOuterJoin :
                    joinString = "LEFT OUTER JOIN";
                    break;

                default :
                    Debug.Assert(false);
                    joinString = null;
                    break;
            }

            #endregion


            List<DbExpressionBinding> inputs = new List<DbExpressionBinding>(2);
            inputs.Add(e.Left);
            inputs.Add(e.Right);

            return VisitJoinExpression(inputs, e.ExpressionKind, joinString, e.JoinCondition);
        }

        ///<summary>
        ///</summary>
        ///<param name = "e"></param>
        ///<returns>A <see cref = "SqlBuilder" /></returns>
        public override ISqlFragment Visit(DbLikeExpression e)
        {
            SqlBuilder result = new SqlBuilder();
            result.Append(e.Argument.Accept(this));
            result.Append(" LIKE ");
            result.Append(e.Pattern.Accept(this));

            // if the ESCAPE expression is a DbNullExpression, then that's tantamount to 
            // not having an ESCAPE at all
            if (e.Escape.ExpressionKind != DbExpressionKind.Null)
            {
                result.Append(" ESCAPE ");
                result.Append(e.Escape.Accept(this));
            }

            return result;
        }

        /// <summary>
        ///   Translates to TOP expression.
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlBuilder" /></returns>
        public override ISqlFragment Visit(DbLimitExpression e)
        {
            return VisitTopLimitExpression(e);
        }

        ISqlFragment VisitTopLimitExpression(DbLimitExpression e)
        {
            Debug.Assert
                (
                    e.Limit is DbConstantExpression || e.Limit is DbParameterReferenceExpression,
                    "DbLimitExpression.Limit is of invalid expression type"
                );

            SqlSelectStatement result = VisitExpressionEnsureSqlStatement(e.Argument, false);
            AliasOrSubquery fromAliasOrSubquery;

            if (!IsCompatibleWithCurrentSelectStatement(result, e.ExpressionKind))
            {
                TypeUsage inputType = MetadataHelpers.GetElementTypeUsage(e.Argument.ResultType);

                result = CreateNewSelectStatement(result, "top", inputType, out fromAliasOrSubquery);
                AddAliasToFrom(result, "top", fromAliasOrSubquery, false);
            }

            ISqlFragment topCount = HandleCountExpression(e.Limit);

            result.Top = new TopClause(topCount, e.WithTies);
            return result;
        }

        ///<summary>
        ///  DbNewInstanceExpression is allowed as a child of DbProjectExpression only.
        ///  If anyone else is the parent, we throw.
        ///  We also perform special casing for collections - where we could convert
        ///  them into Unions
        ///
        ///  <see cref = "VisitNewInstanceExpression" /> for the actual implementation.
        ///</summary>
        ///<param name = "e"></param>
        ///<returns></returns>
        public override ISqlFragment Visit(DbNewInstanceExpression e)
        {
            if (MetadataHelpers.IsCollectionType(e.ResultType))
            {
                return VisitCollectionConstructor(e);
            }
            throw new NotSupportedException();
        }

        /// <summary>
        ///   The Not expression may cause the translation of its child to change.
        ///   These children are
        ///   <list type = "bullet">
        ///     <item><see cref = "DbNotExpression" />NOT(Not(x)) becomes x</item>
        ///     <item><see cref = "DbIsEmptyExpression" />NOT EXISTS becomes EXISTS</item>
        ///     <item><see cref = "DbIsNullExpression" />IS NULL becomes IS NOT NULL</item>
        ///     <item><see cref = "DbComparisonExpression" />= becomes&lt;&gt; </item>
        ///   </list>
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlBuilder" /></returns>
        public override ISqlFragment Visit(DbNotExpression e)
        {
            // Flatten Not(Not(x)) to x.
            DbNotExpression notExpression = e.Argument as DbNotExpression;
            if (notExpression != null)
            {
                return notExpression.Argument.Accept(this);
            }

            DbIsEmptyExpression isEmptyExpression = e.Argument as DbIsEmptyExpression;
            if (isEmptyExpression != null)
            {
                return VisitIsEmptyExpression(isEmptyExpression, true);
            }

            DbIsNullExpression isNullExpression = e.Argument as DbIsNullExpression;
            if (isNullExpression != null)
            {
                return VisitIsNullExpression(isNullExpression, true);
            }

            DbComparisonExpression comparisonExpression = e.Argument as DbComparisonExpression;
            if (comparisonExpression != null)
            {
                if (comparisonExpression.ExpressionKind == DbExpressionKind.Equals)
                {
                    return VisitNotEqualExpression(comparisonExpression);
                }
            }

            var result = new SqlBuilder();
            var innerResult = e.Argument.Accept(this);
            if (innerResult.ToString().Length == 0)
            {
                return result;
            }
            result.Append(" NOT (");
            result.Append(innerResult);
            result.Append(")");

            return result;
        }

        protected virtual ISqlFragment VisitNotEqualExpression(DbComparisonExpression comparisonExpression)
        {
            return VisitBinaryExpression(" <> ", comparisonExpression.Left, comparisonExpression.Right);
        }

        /// <summary>
        /// </summary>
        /// <param name = "e"></param>
        /// <returns><see cref = "SqlBuilder" /></returns>
        public override ISqlFragment Visit(DbNullExpression e)
        {
            SqlBuilder result = new SqlBuilder();
            // always cast nulls - sqlserver doesn't like case expressions where the "then" clause is null
            result.Append("CAST(NULL AS ");
            TypeUsage type = e.ResultType;
            result.Append(GetSqlPrimitiveType(type));
            result.Append(")");
            return result;
        }

        /// <summary>
        ///   <see cref = "DbOfTypeExpression" /> is illegal at this stage
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlBuilder" /></returns>
        public override ISqlFragment Visit(DbOfTypeExpression e)
        {
            throw CreateProviderDealsWithOutputTreeExpressionsOnlyException();
        }

        ///<summary>
        ///</summary>
        ///<param name = "e"></param>
        ///<returns>A <see cref = "SqlBuilder" /></returns>
        ///<seealso cref = "Visit(DbAndExpression)" />
        public override ISqlFragment Visit(DbOrExpression e)
        {
            return VisitBinaryExpression("\r\n OR ", e.Left, e.Right);
        }

        ///<summary>
        /// Add "@"
        ///</summary>
        ///<param name = "e"></param>
        ///<returns>A <see cref = "SqlBuilder" /></returns>
        public override ISqlFragment Visit(DbParameterReferenceExpression e)
        {
            SqlBuilder result = new SqlBuilder();
            // Do not quote this name.
            // We are not checking that e.Name has no illegal characters. e.g. space
            result.Append("@" + e.ParameterName);

            return result;
        }

        /// <summary>
        ///   <see cref = "Visit(DbFilterExpression)" /> for the general ideas.
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlSelectStatement" /></returns>
        /// <seealso cref = "Visit(DbFilterExpression)" />
        public override ISqlFragment Visit(DbProjectExpression e)
        {
            AliasOrSubquery fromAliasOrSubquery;
            SqlSelectStatement result = VisitInputCollectionExpression(
                e.Input, out fromAliasOrSubquery);

            // Project is compatible with Filter
            // but not with Project, GroupBy
            if (!IsCompatibleWithCurrentSelectStatement(result, e.ExpressionKind))
            { //TODO: does it go to here for Sample provider too? - It does not, because SqlSelectStatement.Select does not have value both times, only SqlSelectStatement.From has value 1st time
                result = CreateNewSelectStatement(
                    result, e.Input.VariableName, e.Input.VariableType, out fromAliasOrSubquery);
            }
            currentSelectStatement.Set(result);
            AddAliasToFrom(result, e.Input.VariableName, fromAliasOrSubquery);

            // Project is the only node that can have DbNewInstanceExpression as a child
            // so we have to check it here.
            // We call VisitNewInstanceExpression instead of Visit(DbNewInstanceExpression), since
            // the latter throws.
            DbNewInstanceExpression newInstanceExpression = e.Projection as DbNewInstanceExpression;
            if (newInstanceExpression != null)
            { //TODO: For MDX it goes here and adds rowtype garbage to Select on 2nd time, for Sample it also goes here and creates a new Select (leaving old Select nested in FROM)
                result.Select.Append(VisitNewInstanceExpression(newInstanceExpression));
            }
            else
            {
                result.Select.Append(e.Projection.Accept(this));
            }

            currentSelectStatement.Pop();

            return result;
        }

        ///<summary>
        ///  This method handles record flattening, which works as follows.
        ///  consider an expression <c>Prop(y, Prop(x, Prop(d, Prop(c, Prop(b, Var(a)))))</c>
        ///  where a,b,c are joins, d is an extent and x and y are fields.
        ///  b has been flattened into a, and has its own SELECT statement.
        ///  c has been flattened into b.
        ///  d has been flattened into c.
        ///
        ///  We visit the instance, so we reach Var(a) first.  This gives us a (join)symbol.
        ///  Symbol(a).b gives us a join symbol, with a SELECT statement i.e. Symbol(b).
        ///  From this point on , we need to remember Symbol(b) as the source alias,
        ///  and then try to find the column.  So, we use a AliasPairForRecordFlattening.
        ///
        ///  We have reached the end when the symbol no longer points to a join symbol.
        ///</summary>
        ///<param name = "e"></param>
        ///<returns>A <see cref = "JoinAlias" /> if we have not reached the first
        ///  Join node that has a SELECT statement.
        ///  A <see cref = "AliasPairForRecordFlattening" /> if we have seen the JoinNode, and it has
        ///  a SELECT statement.
        ///  A <see cref = "SqlBuilder" /> with {Input}.propertyName otherwise.
        ///</returns>
        public override ISqlFragment Visit(DbPropertyExpression e)
        {
            SqlBuilder result;

            ISqlFragment instanceSql = e.Instance.Accept(this);

            // Since the DbVariableReferenceExpression is a proper child of ours, we can reset
            // isVarSingle.
            DbVariableReferenceExpression DbVariableReferenceExpression = e.Instance as DbVariableReferenceExpression;
            if (DbVariableReferenceExpression != null)
            {
                isVariableReferenceExpressionChildOfPropertyOrMethodExpression = false;
            }

            // We need to flatten, and have not yet seen the first nested SELECT statement.
            JoinAlias joinAlias = instanceSql as JoinAlias;
            if (joinAlias != null)
            {
                Debug.Assert(joinAlias.NameToExtent.ContainsKey(e.Property.Name));
                if (joinAlias.IsNestedJoin)
                {
                    return new AliasPairForRecordFlattening(joinAlias, joinAlias.NameToExtent[e.Property.Name]);
                }
                return joinAlias.NameToExtent[e.Property.Name];
            }

            // ---------------------------------------
            // We have seen the first nested SELECT statement, but not the column.
            AliasPairForRecordFlattening aliasPairForRecordFlattening = instanceSql as AliasPairForRecordFlattening;
            if (aliasPairForRecordFlattening != null)
            {
                JoinAlias columnJoinAlias = aliasPairForRecordFlattening.Column as JoinAlias;
                if (columnJoinAlias != null)
                {
                    aliasPairForRecordFlattening.Column = columnJoinAlias.NameToExtent[e.Property.Name];
                    return aliasPairForRecordFlattening;
                }
                // AliasPairForRecordFlattening.Column has the base extent.
                // we need the symbol for the column, since it might have been renamed
                // when handling a JOIN.
                if (aliasPairForRecordFlattening.Column.Columns.ContainsKey(e.Property.Name))
                {
                    result = new SqlBuilder();
                    result.Append(aliasPairForRecordFlattening.Source);
                    result.Append(".");
                    result.Append(aliasPairForRecordFlattening.Column.Columns[e.Property.Name]);
                    return result;
                }
            }
            // ---------------------------------------

            result = new SqlBuilder();
            result.Append(instanceSql);
            AddColumnName(result, e);

            return result;
        }

        protected virtual void AddColumnName(SqlBuilder result,
                           DbPropertyExpression expression)
        {
            result.Append(".");
            // At this point the column name cannot be renamed, so we do
            // not use a symbol.
            result.Append(QuoteIdentifier(expression.Property.Name));
        }

        /// <summary>
        ///   Any(input, x) => Exists(Filter(input,x))
        ///   All(input, x) => Not Exists(Filter(input, not(x))
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        public override ISqlFragment Visit(DbQuantifierExpression e)
        {
            SqlBuilder result = new SqlBuilder();

            bool negatePredicate = ( e.ExpressionKind == DbExpressionKind.All );
            if (e.ExpressionKind == DbExpressionKind.Any)
            {
                result.Append("EXISTS (");
            }
            else
            {
                Debug.Assert(e.ExpressionKind == DbExpressionKind.All);
                result.Append("NOT EXISTS (");
            }

            SqlSelectStatement filter = VisitFilterExpression(e.Predicate, e.Input, negatePredicate);
            if (filter.Select.IsEmpty)
            {
                AddDefaultColumns(filter);
            }

            result.Append(filter);
            result.Append(")");

            return result;
        }

        /// <summary>
        ///   <see cref = "DbRefExpression" /> is illegal at this stage
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        public override ISqlFragment Visit(DbRefExpression e)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        ///   <see cref = "DbRelationshipNavigationExpression" /> is illegal at this stage
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        public override ISqlFragment Visit(DbRelationshipNavigationExpression e)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        ///   For Sql9 it translates to:
        ///   SELECT Y.x1, Y.x2, ..., Y.xn
        ///   FROM (
        ///   SELECT X.x1, X.x2, ..., X.xn, row_number() OVER (ORDER BY sk1, sk2, ...) AS [row_number] 
        ///   FROM input as X 
        ///   ) as Y
        ///   WHERE Y.[row_number] > count 
        ///   ORDER BY sk1, sk2, ...
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlBuilder" /></returns>
        public override ISqlFragment Visit(DbSkipExpression e)
        {
            return ( new DbSkipExpressionVisitor(this) ).Visit(e);
        }

        /// <summary>
        ///   <see cref = "Visit(DbFilterExpression)" />
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlSelectStatement" /></returns>
        /// <seealso cref = "Visit(DbFilterExpression)" />
        public override ISqlFragment Visit(DbSortExpression e)
        {
            AliasOrSubquery fromAliasOrSubquery;
            SqlSelectStatement result = VisitInputCollectionExpression(e.Input, out fromAliasOrSubquery);

            // OrderBy is compatible with Filter
            // and nothing else
            if (!IsCompatibleWithCurrentSelectStatement(result, e.ExpressionKind))
            { //TODO: does Sample go here? - yes, it does
                result = CreateNewSelectStatement(result, e.Input.VariableName, e.Input.VariableType, out fromAliasOrSubquery);
            }

            currentSelectStatement.Set(result);

            AddAliasToFrom(result, e.Input.VariableName, fromAliasOrSubquery);

            AddSortKeys(result.OrderBy, e.SortOrder); //It works for Sample provider because it uses DbPropertyExpr.Property.Name only, it does not need to extract entity name from rowtype

            currentSelectStatement.Pop(); //Stack.Pop()

            return result;
        }

        /// <summary>
        ///   <see cref = "DbTreatExpression" /> is illegal at this stage
        /// </summary>
        /// <param name = "e"></param>
        /// <returns>A <see cref = "SqlBuilder" /></returns>
        public override ISqlFragment Visit(DbTreatExpression e)
        {
            throw new NotSupportedException();
        }

        ///<summary>
        ///  This code is shared by <see cref = "Visit(DbExceptExpression)" />
        ///  and <see cref = "Visit(DbIntersectExpression)" />
        ///
        ///  <see cref = "VisitSetOpExpression" />
        ///  Since the left and right expression may not be Sql select statements,
        ///  we must wrap them up to look like SQL select statements.
        ///</summary>
        ///<param name = "e"></param>
        ///<returns></returns>
        public override ISqlFragment Visit(DbUnionAllExpression e)
        {
            return VisitSetOpExpression(e.Left, e.Right, "UNION ALL");
        }

        ///<summary>
        ///  This method determines whether an extent from an outer scope(free variable)
        ///  is used in the CurrentSelectStatement.
        ///
        ///  An extent in an outer scope, if its symbol is not in the FromExtents
        ///  of the CurrentSelectStatement.
        ///</summary>
        ///<param name = "e"></param>
        ///<returns>A <see cref = "AliasOrSubquery" />.</returns>
        public override ISqlFragment Visit(DbVariableReferenceExpression e)
        {
            if (isVariableReferenceExpressionChildOfPropertyOrMethodExpression)
            {
                throw new NotSupportedException();
                // A DbVariableReferenceExpression has to be a child of DbPropertyExpression or MethodExpression
                // This is also checked in GenerateSql(...) at the end of the visiting.
            }
            isVariableReferenceExpressionChildOfPropertyOrMethodExpression = true; // This will be reset by DbPropertyExpression or MethodExpression

            AliasOrSubquery result = namingScopes.Lookup(e.VariableName);
            if (!currentSelectStatement.Get().FromExtents.Contains(result))
            {
                currentSelectStatement.Get().OuterExtents[result] = true;
            }

            return result;
        }


        #region Visits shared by multiple nodes

        /// <summary>
        ///   Aggregates are not visited by the normal visitor walk.
        /// </summary>
        /// <param name = "aggregate">The aggregate go be translated</param>
        /// <param name = "aggregateArgument">The translated aggregate argument</param>
        /// <returns></returns>
        protected virtual SqlBuilder VisitAggregate
            (
                DbAggregate aggregate,
                ISqlFragment aggregateArgument
            )
        {
            SqlBuilder aggregateResult = new SqlBuilder();
            DbFunctionAggregate functionAggregate = aggregate as DbFunctionAggregate;

            if (functionAggregate == null)
            {
                throw new NotSupportedException();
            }

            //The only aggregate function with different name is Big_Count
            //Note: If another such function is to be added, a dictionary should be created
            if (MetadataHelpers.IsCanonicalFunction(functionAggregate.Function)
                && String.Equals(functionAggregate.Function.Name, "BigCount", StringComparison.Ordinal))
            {
                aggregateResult.Append("COUNT_BIG");
            }
            else
            {
                WriteFunctionName(aggregateResult, functionAggregate.Function);
            }

            aggregateResult.Append("(");

            DbFunctionAggregate fnAggr = functionAggregate;
            if (( null != fnAggr ) 
                && ( fnAggr.Distinct ))
            {
                aggregateResult.Append("DISTINCT ");
            }

            AppendAggregateArgument(aggregateResult, aggregateArgument);

            aggregateResult.Append(")");
            return aggregateResult;
        }

        protected virtual void AppendAggregateArgument
            (
                SqlBuilder aggregateResult
                , ISqlFragment aggregateArgument
)
        {
            aggregateResult.Append(aggregateArgument);
        }

        /// <summary>
        /// They added AND IS NOT NULL to each join on traversal in EF6 - have to skip it
        /// </summary>
        private bool IsJunkExpression(DbExpression predicate)
        {
            return (predicate is DbIsNullExpression
                    || (predicate is DbUnaryExpression && ((DbUnaryExpression)(predicate)).Argument is DbIsNullExpression)
                    || predicate.Accept(this).ToString().Length == 0);
        }

        SqlBuilder VisitBinaryExpression
            (
            string op,
            DbExpression left,
            DbExpression right)
        {
            var result = new SqlBuilder();
            //if (string.IsNullOrWhiteSpace(left.Accept(this).ToString())
            //    || string.IsNullOrWhiteSpace(right.Accept(this).ToString()))
            //{
            //    return result;
            //}
            bool isLeftJunk = IsJunkExpression(left);
            if (!isLeftJunk)
            {
                ParanthesizeExpressionIfNeeded(left, result);
            }
            bool isRightJunk = IsJunkExpression(right);
            if ((!isLeftJunk) && (!isRightJunk))
            {
                result.Append(op);
            }
            else
            {
                //delete me
                ;
            }
            if (!isRightJunk)
            {
                ParanthesizeExpressionIfNeeded(right, result);
            }
            return result;
        }

        public class InputBindingExpression
        {
            DbExpression inputExpression;
            string inputVarName;
            TypeUsage inputVarType;

            public InputBindingExpression(DbExpression inputExpression,
                                          string inputVarName,
                                          TypeUsage inputVarType)
            {
                this.inputExpression = inputExpression;
                this.inputVarName = inputVarName;
                this.inputVarType = inputVarType;
            }
            public InputBindingExpression(DbGroupExpressionBinding input)
            {
                inputExpression = input.Expression;
                inputVarName = input.VariableName;
                inputVarType = input.VariableType;
            }

            public InputBindingExpression(DbExpressionBinding input)
            {
                inputExpression = input.Expression;
                inputVarName = input.VariableName;
                inputVarType = input.VariableType;
            }

            public DbExpression InputExpression
            {
                get { return inputExpression; }
            }

            public string InputVarName
            {
                get { return inputVarName; }
            }

            public TypeUsage InputVarType
            {
                get { return inputVarType; }
            }
        }

        SqlSelectStatement VisitInputCollectionExpression
            (
                DbGroupExpressionBinding input, 
                out AliasOrSubquery fromAliasOrSubquery
            )
        {
            return VisitInputCollectionExpression(
                new InputBindingExpression(input), 
                out fromAliasOrSubquery);
        }

        protected SqlSelectStatement VisitInputCollectionExpression
            (
                DbExpressionBinding input, 
                out AliasOrSubquery fromAliasOrSubquery
            )
        {
            return VisitInputCollectionExpression(
                new InputBindingExpression(input), 
                out fromAliasOrSubquery);
        }

        /// <summary>
        ///   This is called by the relational nodes.  It does the following
        ///   <list>
        ///     <item>If the input is not a SqlSelectStatement, it assumes that the input
        ///       is a collection expression, and creates a new SqlSelectStatement </item>
        ///   </list>
        /// </summary>
        /// <param name = "inputExpression"></param>
        /// <param name = "inputVarName"></param>
        /// <param name = "inputVarType"></param>
        /// <param name = "fromAliasOrSubquery"></param>
        /// <returns>A <see cref = "SqlSelectStatement" /> and the main fromSymbol
        ///   for this select statement.</returns>
        SqlSelectStatement VisitInputCollectionExpression
            (
                InputBindingExpression inputBindingExpression,
                 out AliasOrSubquery fromAliasOrSubquery
            )
        {
            SqlSelectStatement result;
            ISqlFragment sqlFragment = inputBindingExpression.InputExpression.Accept(this);
            result = sqlFragment as SqlSelectStatement;

            if (result == null)
            {
                result = CreateSelectStatement();
                WrapNonQueryExtent(
                    result, sqlFragment, inputBindingExpression.InputExpression.ExpressionKind);
            }

            if (result.FromExtents.Count == 0)
            {
                // input was an extent
                fromAliasOrSubquery = new AliasOrSubquery(
                    inputBindingExpression.InputVarName, inputBindingExpression.InputVarType);
            }
            else if (result.FromExtents.Count == 1)
            {
                // input was Filter/GroupBy/Project/OrderBy
                // we are likely to reuse this statement.
                fromAliasOrSubquery = result.FromExtents[0];
            }
            else
            {
                // input was a join.
                // we are reusing the select statement produced by a Join node
                // we need to remove the original extents, and replace them with a
                // new extent with just the Join symbol.
                var joinAlias 
                    = new JoinAlias(inputBindingExpression.InputVarName, 
                        inputBindingExpression.InputVarType, result.FromExtents)
                    {
                        FlattenedExtentList = result.AllJoinExtents
                    };

                fromAliasOrSubquery = joinAlias;
                result.FromExtents.Clear();
                result.FromExtents.Add(fromAliasOrSubquery);
            }

            return result;
        }

        /// <summary>
        ///   <see cref = "Visit(DbIsEmptyExpression)" />
        /// </summary>
        /// <param name = "e"></param>
        /// <param name = "negate">Was the parent a DbNotExpression?</param>
        /// <returns></returns>
        SqlBuilder VisitIsEmptyExpression
            (
            DbIsEmptyExpression e,
            bool negate)
        {
            SqlBuilder result = new SqlBuilder();
            if (!negate)
            {
                result.Append(" NOT");
            }
            result.Append(" EXISTS (");
            result.Append(VisitExpressionEnsureSqlStatement(e.Argument));
            result.AppendLine();
            result.Append(")");

            return result;
        }


        /// <summary>
        ///   Translate a NewInstance(Element(X)) expression into
        ///   "select top(1) * from X"
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        ISqlFragment VisitCollectionConstructor(DbNewInstanceExpression e)
        {
            Debug.Assert(e.Arguments.Count <= 1);

            if (e.Arguments.Count == 1 && e.Arguments[0].ExpressionKind == DbExpressionKind.Element)
            {
                DbElementExpression elementExpr = e.Arguments[0] as DbElementExpression;
                SqlSelectStatement result = VisitExpressionEnsureSqlStatement(elementExpr.Argument);

                if (!IsCompatibleWithCurrentSelectStatement(result, DbExpressionKind.Element))
                {
                    AliasOrSubquery fromAliasOrSubquery;
                    TypeUsage inputType = MetadataHelpers.GetElementTypeUsage(elementExpr.Argument.ResultType);

                    result = CreateNewSelectStatement(result, "element", inputType, out fromAliasOrSubquery);
                    AddAliasToFrom(result, "element", fromAliasOrSubquery, false);
                }
                result.Top = new TopClause(1, false);
                return result;
            }


            // Otherwise simply build this out as a union-all ladder
            CollectionType collectionType = MetadataHelpers.GetEdmType<CollectionType>(e.ResultType);
            Debug.Assert(collectionType != null);
            bool isScalarElement = MetadataHelpers.IsPrimitiveType(collectionType.TypeUsage);

            SqlBuilder resultSql = new SqlBuilder();
            string separator = "";

            // handle empty table
            if (e.Arguments.Count == 0)
            {
                Debug.Assert(isScalarElement);
                resultSql.Append(" SELECT CAST(null as ");
                resultSql.Append(GetSqlPrimitiveType(collectionType.TypeUsage));
                resultSql.Append(") AS X FROM (SELECT 1) AS Y WHERE 1=0");
            }

            foreach (DbExpression arg in e.Arguments)
            {
                resultSql.Append(separator);
                resultSql.Append(" SELECT ");
                resultSql.Append(arg.Accept(this));
                // For scalar elements, no alias is appended yet. Add this.
                if (isScalarElement)
                {
                    resultSql.Append(" AS X ");
                }
                separator = " UNION ALL ";
            }

            return resultSql;
        }

        /// <summary>
        ///   <see cref = "Visit(DbIsNullExpression)" />
        /// </summary>
        /// <param name = "e"></param>
        /// <param name = "negate">Was the parent a DbNotExpression?</param>
        /// <returns></returns>
        SqlBuilder VisitIsNullExpression
            (
            DbIsNullExpression e,
            bool negate
            )
        {
            //return new SqlBuilder("1 = 1"); //TODO: can I delete lines below?:

            var result = new SqlBuilder();
            if (negate)
            {
                result.Append(" NOT");
            }
            result.Append(" IsEmpty(");
            result.Append(e.Argument.Accept(this));
            result.Append(")");
            return result;
        }

        ///<summary>
        ///  This handles the processing of join expressions.
        ///  The extents on a left spine are flattened, while joins
        ///  not on the left spine give rise to new nested sub queries.
        ///
        ///  Joins work differently from the rest of the visiting, in that
        ///  the parent (i.e. the join node) creates the SqlSelectStatement
        ///  for the children to use.
        ///
        ///  The "parameter" IsInJoinContext indicates whether a child extent should
        ///  add its stuff to the existing SqlSelectStatement, or create a new SqlSelectStatement
        ///  By passing true, we ask the children to add themselves to the parent join,
        ///  by passing false, we ask the children to create new Select statements for
        ///  themselves.
        ///
        ///  This method is called from <see cref = "Visit(DbApplyExpression)" /> and
        ///  <see cref = "Visit(DbJoinExpression)" />.
        ///</summary>
        ///<param name = "inputs"></param>
        ///<param name = "joinKind"></param>
        ///<param name = "joinString"></param>
        ///<param name = "joinCondition"></param>
        ///<returns> A <see cref = "SqlSelectStatement" /></returns>
        ISqlFragment VisitJoinExpression
            (
                IList<DbExpressionBinding> inputs,
                DbExpressionKind joinKind,
                string joinString,
                DbExpression joinCondition
            )
        {
            SqlSelectStatement result;
            // If the parent is not a join( or says that it is not),
            // we should create a new SqlSelectStatement.
            // otherwise, we add our child extents to the parent's FROM clause.
            if (!ShouldStartNewSubqueryWhenParentIsJoin)
            { //TODO: the condition seems to be inverted, look into generated SQL nested subqueries as is and if I flip the condition
                result = CreateSelectStatement();
                currentSelectStatement.Set(result);
            }
            else
            {
                result = currentSelectStatement.Get();
            }

            // Process each of the inputs, and then the joinCondition if it exists.
            // It would be nice if we could call VisitInputCollectionExpression - that would
            // avoid some code duplication
            // but the Join post-processing is messy and prevents this reuse.

            string separator = "";
            bool isLeftMostInput = true;
            int inputCount = inputs.Count;
            for (int idx = 0; idx < inputCount; idx++)
            { //TODO: use 'foreach (var input in inputs)' instead
                DbExpressionBinding input = inputs[idx];

                if (separator != "")
                {
                    result.From.AppendLine();
                }
                result.From.Append(separator + " ");
                // Change this if other conditions are required
                // to force the child to produce a nested SqlStatement.
                bool needsJoinContext = ( input.Expression.ExpressionKind == DbExpressionKind.Scan )
                                        || ( isLeftMostInput &&
                                             ( IsJoinExpression(input.Expression)
                                               || IsApplyExpression(input.Expression) ) )
                    ;

                shouldCreateNewSubqueryForNestedExtentsStack.Push
                    (
                        needsJoinContext
                            ? true
                            : false);
                // if the child reuses our select statement, it will append the from
                // symbols to our FromExtents list.  So, we need to remember the
                // start of the child's entries.
                int fromSymbolStart = result.FromExtents.Count;

                ISqlFragment fromExtentFragment = input.Expression.Accept(this);

                shouldCreateNewSubqueryForNestedExtentsStack.Pop();

                ProcessJoinInputResult(fromExtentFragment, result, input, fromSymbolStart);
                separator = joinString;

                isLeftMostInput = false;
            }

            // Visit the on clause/join condition.
            switch (joinKind)
            {
                case DbExpressionKind.FullOuterJoin :
                case DbExpressionKind.InnerJoin :
                case DbExpressionKind.LeftOuterJoin :
                    result.From.Append(" ON ");
                    shouldCreateNewSubqueryForNestedExtentsStack.Push(false);
                    result.From.Append(joinCondition.Accept(this));
                    shouldCreateNewSubqueryForNestedExtentsStack.Pop();
                    break;
            }

            if (!ShouldStartNewSubqueryWhenParentIsJoin)
            {
                currentSelectStatement.Pop();
            }

            return result;
        }

        protected virtual bool IsProjectionEmpty(SqlSelectStatement selectStatement)
        {
            return selectStatement.Select.IsEmpty;
        }

        ///<summary>
        ///  This is called from <see cref = "VisitJoinExpression" />.
        ///
        ///  This is responsible for maintaining the symbol table (NamingScopes) after visiting
        ///  a child of a join expression.
        ///
        ///  The child's sql statement may need to be completed.
        ///
        ///  The child's result could be one of
        ///  <list type = "number">
        ///    <item>The same as the parent's - this is treated specially.</item>
        ///    <item>A sql select statement, which may need to be completed</item>
        ///    <item>An extent - just copy it to the from clause</item>
        ///    <item>Anything else (from a collection-valued expression) -
        ///      unnest and copy it.</item>
        ///  </list>
        ///
        ///  If the input was a Join, we need to create a new join symbol (alias),
        ///  otherwise, we create a normal symbol (alias).
        ///
        ///  We then call AddAliasToFrom to add the AS clause, and update the symbol table.
        ///
        ///
        ///  If the child's result was the same as the parent's, we have to clean up
        ///  the list of symbols (aliases) in the FromExtents list, since this contains symbols from
        ///  the children of both the parent and the child.
        ///  This happens when the child visited is a Join, and is the leftmost child of
        ///  the parent.
        ///</summary>
        ///<param name = "fromExtentFragment"></param>
        ///<param name = "result"></param>
        ///<param name = "input"></param>
        ///<param name = "fromSymbolStart"></param>
        void ProcessJoinInputResult
            (
            ISqlFragment fromExtentFragment,
            SqlSelectStatement result,
            DbExpressionBinding input,
            int fromSymbolStart)
        {
            AliasOrSubquery fromAliasOrSubquery = null;

            if (result != fromExtentFragment)
            {
                // The child has its own select statement, and is not reusing
                // our select statement.
                // This should look a lot like VisitInputCollectionExpression().
                SqlSelectStatement sqlSelectStatement = fromExtentFragment as SqlSelectStatement;
                if (sqlSelectStatement != null)
                {
                    if (IsProjectionEmpty(sqlSelectStatement))
                    {
                        List<AliasOrSubquery> columns = AddDefaultColumns(sqlSelectStatement);

                        if (IsJoinExpression(input.Expression)
                            || IsApplyExpression(input.Expression))
                        {
                            List<AliasOrSubquery> extents = sqlSelectStatement.FromExtents;
                            JoinAlias newJoinAlias = new JoinAlias(input.VariableName, input.VariableType, extents);
                            newJoinAlias.IsNestedJoin = true;
                            newJoinAlias.ColumnList = columns;

                            fromAliasOrSubquery = newJoinAlias;
                        }
                        else
                        {
                            // this is a copy of the code in CreateNewSelectStatement.

                            // if the oldStatement has a join as its input, ...
                            // clone the join symbol, so that we "reuse" the
                            // join symbol.  Normally, we create a new symbol - see the next block
                            // of code.
                            JoinAlias oldJoinAlias = sqlSelectStatement.FromExtents[0] as JoinAlias;
                            if (oldJoinAlias != null)
                            {
                                // Note: sqlSelectStatement.FromExtents will not do, since it might
                                // just be an alias of joinSymbol, and we want an actual JoinSymbol.
                                JoinAlias newJoinAlias = new JoinAlias
                                    (input.VariableName, input.VariableType, oldJoinAlias.ExtentList);
                                // This indicates that the sqlSelectStatement is a blocking scope
                                // i.e. it hides/renames extent columns
                                newJoinAlias.IsNestedJoin = true;
                                newJoinAlias.ColumnList = columns;
                                newJoinAlias.FlattenedExtentList = oldJoinAlias.FlattenedExtentList;

                                fromAliasOrSubquery = newJoinAlias;
                            }
                        }
                    }
                    result.From.Append(" (");
                    result.From.Append(sqlSelectStatement);
                    result.From.Append(" )");
                }
                else if (input.Expression is DbScanExpression)
                {
                    result.From.Append(fromExtentFragment);
                }
                else // bracket it
                {
                    WrapNonQueryExtent(result, fromExtentFragment, input.Expression.ExpressionKind);
                }

                if (fromAliasOrSubquery == null) // i.e. not a join symbol
                {
                    fromAliasOrSubquery = new AliasOrSubquery(input.VariableName, input.VariableType);
                }


                AddAliasToFrom(result, input.VariableName, fromAliasOrSubquery);
                result.AllJoinExtents.Add(fromAliasOrSubquery);
            }
            else // result == fromExtentFragment.  The child extents have been merged into the parent's.
            {
                // we are adding extents to the current sql statement via flattening.
                // We are replacing the child's extents with a single Join symbol.
                // The child's extents are all those following the index fromSymbolStart.
                //
                List<AliasOrSubquery> extents = new List<AliasOrSubquery>();

                // We cannot call extents.AddRange, since the is no simple way to
                // get the range of symbols fromSymbolStart..result.FromExtents.Count
                // from result.FromExtents.
                // We copy these symbols to create the JoinSymbol later.
                for (int i = fromSymbolStart; i < result.FromExtents.Count; ++i)
                {
                    extents.Add(result.FromExtents[i]);
                }
                result.FromExtents.RemoveRange(fromSymbolStart, result.FromExtents.Count - fromSymbolStart);
                fromAliasOrSubquery = new JoinAlias(input.VariableName, input.VariableType, extents);
                result.FromExtents.Add(fromAliasOrSubquery);
                // this Join Symbol does not have its own select statement, so we
                // do not set IsNestedJoin


                // We do not call AddAliasToFrom(), since we do not want to add
                // "AS alias" to the FROM clause- it has been done when the extent was added earlier.
                namingScopes.Add(input.VariableName, fromAliasOrSubquery);
            }
        }

        ///<summary>
        /// Per new table scan (new collection instance)
        ///  We write out the translation of each of the columns in the record.
        /// 
        ///  We assume that this is only called as a child of a Project.
        ///  This replaces <see cref = "Visit(DbNewInstanceExpression)" />, since
        ///  we do not allow DbNewInstanceExpression as a child of any node other than
        ///  DbProjectExpression.
        ///</summary>
        ///<param name = "dbNewInstanceExpression">
        /// New input collection descriptor which translates to a new table alias (new select or new join)
        /// </param>
        ///<returns>
        /// A <see cref = "SqlBuilder" />
        /// This result is appended to SqlSelectStatement.Select
        /// </returns>
        ISqlFragment VisitNewInstanceExpression(DbNewInstanceExpression dbNewInstanceExpression)
        {
            SqlBuilder result = new SqlBuilder();
            RowType rowType = dbNewInstanceExpression.ResultType.EdmType as RowType;
            if (null == rowType)
            {
                throw new NotSupportedException(
                    "Types other then RowType (such as UDTs for instance) are not supported.");
            }
            for (int i = 0; i < dbNewInstanceExpression.Arguments.Count; ++i)
            { //for each entity property / column
                DbExpression columnExpression = dbNewInstanceExpression.Arguments[i];
                if (MetadataHelpers.IsRowType(columnExpression.ResultType))
                {
                    throw new NotSupportedException(
                        "We do not support nested records or other complex objects.");
                }
                EdmProperty member = rowType.Properties[i];
                var columnDefinition = GetColumnDefinition(columnExpression, member);
                if(columnDefinition.IsEmpty)
                {
                    continue;
                }
                if ( ! result.IsEmpty)
                {
                    result.AppendLine(", ");
                }
                result.Append(columnDefinition);
            }

            return result;
        }

        protected virtual SqlBuilder GetColumnDefinition
            (
                DbExpression columnExpression,
                EdmProperty property
            )
        {
            var result = new SqlBuilder();
            ISqlFragment columnName = columnExpression.Accept(this);
            result.Append(columnName);
            AddAliasToColumnIfNeeded(result, columnName, property.Name);
            return result;
        }

        /// <summary>
        /// Returns a table name from columnExpression of DbPropertyExpression type (without '[]')
        /// </summary>
        /// <param name="columnExpression">
        /// If not columnExpression is DbPropertyExpression returns ""
        /// </param>
        /// <returns>
        /// A table name from columnExpression of DbPropertyExpression type 
        /// (without '[]')
        /// </returns>
        /// <remarks>
        /// Even though this method is used only in a subclass 'MdxGenerator'
        /// it cannot be pulled down to MdxGenerator without making 'namingScopes' protected
        /// and a couple of its related classes public
        /// </remarks>
        public string GetTableNameFromDbExpression(DbExpression columnExpression)
        {
            if (!(columnExpression is DbPropertyExpression))
            {
                return "";
            }
            string entityName = GetEntityName(columnExpression); 
            if(EntityToTableNameMap.ContainsKey(entityName))
            {
                return EntityToTableNameMap[entityName];
            }
            return namingScopes.Lookup(GetVariableName(columnExpression))
                .Type.EdmType.Name;
        }

        protected virtual string GetEntityName(DbExpression columnExpression)
        {
            return ((DbPropertyExpression)columnExpression).Property.DeclaringType.Name;
        }

        string GetVariableName(DbExpression columnExpression)
        {
            var propertyExpression = (DbPropertyExpression)columnExpression;
            if (propertyExpression.Instance is DbPropertyExpression)
            { //TODO: Is it a right thing to do?
                return
                    ( (DbVariableReferenceExpression)( (DbPropertyExpression)
                        propertyExpression.Instance ).Instance )
                            .VariableName;
            }
            return  ((DbVariableReferenceExpression) propertyExpression.Instance).VariableName;
        }

        protected internal virtual bool ShouldAddAliases()
        {
            return true;
        }

        void AddAliasToColumnIfNeeded
            (SqlBuilder result,
             ISqlFragment columnName,
             string memberName)
        { //TODO: Debug and figure out why initial alias name is extent rather than entity or property name and why rename happens all the time even if it is not needed. After that is done throw an exception in overridden MDX implementation whenever a rename is requested.

            if (ShouldAddAliases() == false 
                || columnName.ToString().ToLower().Contains(memberName.ToLower()))
            {
                return;
            }
            result.Append(" AS ");
            result.Append(QuoteIdentifier(memberName));
        }

        ISqlFragment VisitSetOpExpression
            (
            DbExpression left,
            DbExpression right,
            string separator)
        {
            SqlSelectStatement leftSelectStatement = VisitExpressionEnsureSqlStatement(left);
            SqlSelectStatement rightSelectStatement = VisitExpressionEnsureSqlStatement(right);

            SqlBuilder setStatement = new SqlBuilder();
            setStatement.Append(leftSelectStatement);
            setStatement.AppendLine();
            setStatement.Append(separator); // e.g. UNION ALL
            setStatement.AppendLine();
            setStatement.Append(rightSelectStatement);

            return setStatement;
        }

        #endregion


        #region Function Handling Helpers

        /// <summary>
        ///   Determines whether the given function is a built-in function that requires special handling
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        bool IsSpecialBuiltInFunction(DbFunctionExpression e)
        {
            return IsBuiltinFunction(e.Function) && _builtInFunctionHandlers.ContainsKey(e.Function.Name);
        }

        /// <summary>
        ///   Determines whether the given function is a canonical function that requires special handling
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        bool IsSpecialCanonicalFunction(DbFunctionExpression e)
        {
            return MetadataHelpers.IsCanonicalFunction(e.Function)
                   && _canonicalFunctionHandlers.ContainsKey(e.Function.Name);
        }

        /// <summary>
        ///   Default handling for functions
        ///   Translates them to FunctionName(arg1, arg2, ..., argn)
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        ISqlFragment HandleFunctionDefault(DbFunctionExpression e)
        {
            SqlBuilder result = new SqlBuilder();
            WriteFunctionName(result, e.Function);
            HandleFunctionArgumentsDefault(e, result);
            return result;
        }

        /// <summary>
        ///   Default handling for functions with a given name.
        ///   Translates them to functionName(arg1, arg2, ..., argn)
        /// </summary>
        /// <param name = "e"></param>
        /// <param name = "functionName"></param>
        /// <returns></returns>
        ISqlFragment HandleFunctionDefaultGivenName
            (
            DbFunctionExpression e,
            string functionName)
        {
            SqlBuilder result = new SqlBuilder();
            result.Append(functionName);
            HandleFunctionArgumentsDefault(e, result);
            return result;
        }

        /// <summary>
        ///   Default handling on function arguments
        ///   Appends the list of arguments to the given result
        ///   If the function is niladic it does not append anything,
        ///   otherwise it appends (arg1, arg2, ..., argn)
        /// </summary>
        /// <param name = "e"></param>
        /// <param name = "result"></param>
        void HandleFunctionArgumentsDefault
            (
            DbFunctionExpression e,
            SqlBuilder result)
        {
            bool isNiladicFunction = MetadataHelpers.TryGetValueForMetadataProperty<bool>
                (e.Function, "NiladicFunctionAttribute");
            if (isNiladicFunction && e.Arguments.Count > 0)
            {
                throw new InvalidOperationException("Niladic functions cannot have parameters");
            }

            if (!isNiladicFunction)
            {
                result.Append("(");
                string separator = "";
                foreach (DbExpression arg in e.Arguments)
                {
                    result.Append(separator);
                    result.Append(arg.Accept(this));
                    separator = ", ";
                }
                result.Append(")");
            }
        }

        /// <summary>
        ///   Handler for special built in functions
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        ISqlFragment HandleSpecialBuiltInFunction(DbFunctionExpression e)
        {
            return HandleSpecialFunction(_builtInFunctionHandlers, e);
        }

        /// <summary>
        ///   Handler for special canonical functions
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        ISqlFragment HandleSpecialCanonicalFunction(DbFunctionExpression e)
        {
            return HandleSpecialFunction(_canonicalFunctionHandlers, e);
        }

        /// <summary>
        ///   Dispatches the special function processing to the appropriate handler
        /// </summary>
        /// <param name = "handlers"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        ISqlFragment HandleSpecialFunction
            (
            Dictionary<string, FunctionHandler> handlers,
            DbFunctionExpression e)
        {
            if (!handlers.ContainsKey(e.Function.Name))
            {
                throw new InvalidOperationException
                    ("Special handling should be called only for functions in the list of special functions");
            }

            return handlers[e.Function.Name](this, e);
        }

        /// <summary>
        ///   Handles functions that are translated into TSQL operators.
        ///   The given function should have one or two arguments. 
        ///   Functions with one arguemnt are translated into 
        ///   op arg
        ///   Functions with two arguments are translated into
        ///   arg0 op arg1
        ///   Also, the arguments can be optionaly enclosed in parethesis
        /// </summary>
        /// <param name = "e"></param>
        /// <param name = "parenthesiseArguments">Whether the arguments should be enclosed in parethesis</param>
        /// <returns></returns>
        ISqlFragment HandleSpecialFunctionToOperator
            (
            DbFunctionExpression e,
            bool parenthesiseArguments)
        {
            SqlBuilder result = new SqlBuilder();
            Debug.Assert
                (e.Arguments.Count > 0 && e.Arguments.Count <= 2, "There should be 1 or 2 arguments for operator");

            if (e.Arguments.Count > 1)
            {
                if (parenthesiseArguments)
                {
                    result.Append("(");
                }
                result.Append(e.Arguments[0].Accept(this));
                if (parenthesiseArguments)
                {
                    result.Append(")");
                }
            }
            result.Append(" ");
            Debug.Assert
                (
                    _functionNameToOperatorDictionary.ContainsKey(e.Function.Name),
                    "The function can not be mapped to an operator");
            result.Append(_functionNameToOperatorDictionary[e.Function.Name]);
            result.Append(" ");

            if (parenthesiseArguments)
            {
                result.Append("(");
            }
            result.Append(e.Arguments[e.Arguments.Count - 1].Accept(this));
            if (parenthesiseArguments)
            {
                result.Append(")");
            }
            return result;
        }

        /// <summary>
        ///   <see cref = "HandleSpecialFunctionToOperator"></see>
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleConcatFunction
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            return sqlgen.HandleSpecialFunctionToOperator(e, false);
        }

        /// <summary>
        ///   <see cref = "HandleSpecialFunctionToOperator"></see>
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionBitwise
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            return sqlgen.HandleSpecialFunctionToOperator(e, true);
        }

        /// <summary>
        ///   Handles special case in which datapart 'type' parameter is present. all the functions
        ///   handles here have *only* the 1st parameter as datepart. datepart value is passed along
        ///   the QP as string and has to be expanded as TSQL keyword.
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleDatepartDateFunction
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            Debug.Assert(e.Arguments.Count > 0, "e.Arguments.Count > 0");

            DbConstantExpression constExpr = e.Arguments[0] as DbConstantExpression;
            if (null == constExpr)
            {
                throw new InvalidOperationException
                    (
                    String.Format
                        (
                            "DATEPART argument to function '{0}.{1}' must be a literal string",
                            e.Function.NamespaceName,
                            e.Function.Name));
            }

            string datepart = constExpr.Value as string;
            if (null == datepart)
            {
                throw new InvalidOperationException
                    (
                    String.Format
                        (
                            "DATEPART argument to function '{0}.{1}' must be a literal string",
                            e.Function.NamespaceName,
                            e.Function.Name));
            }

            SqlBuilder result = new SqlBuilder();

            //
            // check if datepart value is valid
            //
            if (!_datepartKeywords.Contains(datepart))
            {
                throw new InvalidOperationException
                    (
                    String.Format
                        (
                            "{0}' is not a valid value for DATEPART argument in '{1}.{2}' function",
                            datepart,
                            e.Function.NamespaceName,
                            e.Function.Name));
            }

            //
            // finaly, expand the function name
            //
            sqlgen.WriteFunctionName(result, e.Function);
            result.Append("(");

            // expand the datepart literal as tsql kword
            result.Append(datepart);
            string separator = ", ";

            // expand remaining arguments
            for (int i = 1; i < e.Arguments.Count; i++)
            {
                result.Append(separator);
                result.Append(e.Arguments[i].Accept(sqlgen));
            }

            result.Append(")");

            return result;
        }

        /// <summary>
        ///   Handler for canonical funcitons for extracting date parts. 
        ///   For example:
        ///   Year(date) -> DATEPART( year, date)
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionDatepart
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            return sqlgen.HandleCanonicalFunctionDatepart(e.Function.Name.ToLowerInvariant(), e);
        }

        /// <summary>
        ///   Handler for canonical funcitons for GetTotalOffsetMinutes.
        ///   GetTotalOffsetMinutes(e) --> Datepart(tzoffset, e)
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionGetTotalOffsetMinutes
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            return sqlgen.HandleCanonicalFunctionDatepart("tzoffset", e);
        }

        /// <summary>
        ///   Handler for turning a canonical function into DATEPART
        ///   Results in DATEPART(datepart, e)
        /// </summary>
        /// <param name = "datepart"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        ISqlFragment HandleCanonicalFunctionDatepart
            (
            string datepart,
            DbFunctionExpression e)
        {
            SqlBuilder result = new SqlBuilder();
            result.Append("DATEPART (");
            result.Append(datepart);
            result.Append(", ");

            Debug.Assert(e.Arguments.Count == 1, "Canonical datepart functions should have exactly one argument");
            result.Append(e.Arguments[0].Accept(this));

            result.Append(")");

            return result;
        }

        static ISqlFragment HandleCanonicalFunctionCurrentDateTime
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            return sqlgen.HandleFunctionDefaultGivenName(e, "GETDATE");
        }

        static ISqlFragment HandleCanonicalFunctionCurrentUtcDateTime
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            return sqlgen.HandleFunctionDefaultGivenName(e, "GETUTCDATE");
        }

        /// <summary>
        ///   Handler for the canonical function CurrentDateTimeOffset
        ///   For Sql8 and Sql9:  throw
        ///   For Sql10: CurrentDateTimeOffset() -> SysDateTimeOffset()
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionCurrentDateTimeOffset
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            sqlgen.AssertKatmaiOrNewer(e);
            return sqlgen.HandleFunctionDefaultGivenName(e, "SysDateTimeOffset");
        }

        /// <summary>
        ///   See <see cref = "HandleCanonicalFunctionDateTimeTypeCreation" /> for exact translation
        ///   Pre Katmai creates datetime.
        ///   On Katmai creates datetime2.
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionCreateDateTime
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            string typeName = ( sqlgen.IsPreKatmai )
                                  ? "datetime"
                                  : "datetime2";
            return sqlgen.HandleCanonicalFunctionDateTimeTypeCreation(typeName, e.Arguments, true, false);
        }

        /// <summary>
        ///   See <see cref = "HandleCanonicalFunctionDateTimeTypeCreation" /> for exact translation
        ///   Pre Katmai not supported.
        ///   On Katmai creates datetimeoffset.
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionCreateDateTimeOffset
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            sqlgen.AssertKatmaiOrNewer(e);
            return sqlgen.HandleCanonicalFunctionDateTimeTypeCreation("datetimeoffset", e.Arguments, true, true);
        }

        /// <summary>
        ///   See <see cref = "HandleCanonicalFunctionDateTimeTypeCreation" /> for exact translation
        ///   Pre Katmai not supported.
        ///   On Katmai creates time.
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionCreateTime
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            sqlgen.AssertKatmaiOrNewer(e);
            return sqlgen.HandleCanonicalFunctionDateTimeTypeCreation("time", e.Arguments, false, false);
        }

        /// <summary>
        ///   Dump out an expression - optionally wrap it with parantheses if possible
        /// </summary>
        /// <param name = "e"></param>
        /// <param name = "result"></param>
        void ParanthesizeExpressionIfNeeded
            (
            DbExpression e,
            SqlBuilder result)
        {
            if (IsComplexExpression(e))
            {
                result.Append("(");
                result.Append(e.Accept(this));
                result.Append(")");
            }
            else
            {
                result.Append(e.Accept(this));
            }
        }

        /// <summary>
        ///   Helper for all date and time types creating functions. 
        /// 
        ///   The given expression is in general trainslated into:
        /// 
        ///   CONVERT(@typename, [datePart] + [timePart] + [timeZonePart], 121), where the datePart and the timeZonePart are optional
        /// 
        ///   Only on Katmai, if a date part is present it is wrapped with a call for adding years as shown below.
        ///   The individual parts are translated as:
        /// 
        ///   Date part:  
        ///   PRE KATMAI: convert(varchar(255), @year) + '-' + convert(varchar(255), @month) + '-' + convert(varchar(255), @day)
        ///   KATMAI: DateAdd(year, @year-1, covert(@typename, '0001' + '-' + convert(varchar(255), @month) + '-' + convert(varchar(255), @day)  + [possibly time ], 121)     
        /// 
        ///   Time part: 
        ///   PRE KATMAI:  convert(varchar(255), @hour)+ ':' + convert(varchar(255), @minute)+ ':' + str(@second, 6, 3)
        ///   KATMAI:  convert(varchar(255), @hour)+ ':' + convert(varchar(255), @minute)+ ':' + str(@second, 10, 7)
        /// 
        ///   Time zone part:
        ///   (case when @tzoffset >= 0 then '+' else '-' end) + convert(varchar(255), ABS(@tzoffset)/60) + ':' + convert(varchar(255), ABS(@tzoffset)%60)
        /// </summary>
        /// <param name = "typeName"></param>
        /// <param name = "args"></param>
        /// <param name = "hasDatePart"></param>
        /// <param name = "hasTimeZonePart"></param>
        /// <returns></returns>
        ISqlFragment HandleCanonicalFunctionDateTimeTypeCreation
            (
            string typeName,
            IList<DbExpression> args,
            bool hasDatePart,
            bool hasTimeZonePart)
        {
            Debug.Assert
                (
                    args.Count == ( hasDatePart
                                        ? 3
                                        : 0 ) + 3 + ( hasTimeZonePart
                                                          ? 1
                                                          : 0 ),
                    "Invalid number of parameters for a date time creating function");

            SqlBuilder result = new SqlBuilder();
            int currentArgumentIndex = 0;

            if (!IsPreKatmai && hasDatePart)
            {
                result.Append("DATEADD(year, ");
                ParanthesizeExpressionIfNeeded(args[currentArgumentIndex++], result);
                result.Append(" - 1, ");
            }

            result.Append("convert (");
            result.Append(typeName);
            result.Append(",");

            //Building the string representation
            if (hasDatePart)
            {
                //  YEAR:   PREKATMAI:               CONVERT(VARCHAR, @YEAR)
                //          KATMAI   :              '0001'
                if (!IsPreKatmai)
                {
                    result.Append("'0001'");
                }
                else
                {
                    AppendConvertToVarchar(result, args[currentArgumentIndex++]);
                }

                //  MONTH
                result.Append(" + '-' + ");
                AppendConvertToVarchar(result, args[currentArgumentIndex++]);

                //  DAY 
                result.Append(" + '-' + ");
                AppendConvertToVarchar(result, args[currentArgumentIndex++]);
                result.Append(" + ' ' + ");
            }

            //  HOUR
            AppendConvertToVarchar(result, args[currentArgumentIndex++]);

            // MINUTE
            result.Append(" + ':' + ");
            AppendConvertToVarchar(result, args[currentArgumentIndex++]);

            // SECOND
            result.Append(" + ':' + str(");
            result.Append(args[currentArgumentIndex++].Accept(this));

            if (IsPreKatmai)
            {
                result.Append(", 6, 3)");
            }
            else
            {
                result.Append(", 10, 7)");
            }

            //  TZOFFSET
            if (hasTimeZonePart)
            {
                result.Append(" + (CASE WHEN ");
                ParanthesizeExpressionIfNeeded(args[currentArgumentIndex], result);
                result.Append(" >= 0 THEN '+' ELSE '-' END) + convert(varchar(255), ABS(");
                ParanthesizeExpressionIfNeeded(args[currentArgumentIndex], result);
                result.Append("/60)) + ':' + convert(varchar(255), ABS(");
                ParanthesizeExpressionIfNeeded(args[currentArgumentIndex], result);
                result.Append("%60))");
            }

            result.Append(", 121)");

            if (!IsPreKatmai && hasDatePart)
            {
                result.Append(")");
            }
            return result;
        }

        /// <summary>
        ///   Helper method that wrapps the given expession with a conver to varchar(255)
        /// </summary>
        /// <param name = "result"></param>
        /// <param name = "e"></param>
        void AppendConvertToVarchar
            (
            SqlBuilder result,
            DbExpression e)
        {
            result.Append("convert(varchar(255), ");
            result.Append(e.Accept(this));
            result.Append(")");
        }

        /// <summary>
        ///   TruncateTime(DateTime X) 
        ///   PreKatmai:    TRUNCATETIME(X) => CONVERT(DATETIME, CONVERT(VARCHAR(255), expression, 102),  102)
        ///   Katmai:    TRUNCATETIME(X) => CONVERT(DATETIME2, CONVERT(VARCHAR(255), expression, 102),  102)
        ///      
        ///   TruncateTime(DateTimeOffset X) 
        ///   TRUNCATETIME(X) => CONVERT(datetimeoffset, CONVERT(VARCHAR(255), expression,  102) 
        ///   + ' 00:00:00 ' +  Right(convert(varchar(255), @arg, 121), 6),  102)
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionTruncateTime
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            //The type that we need to return is based on the argument type.
            string typeName = null;
            bool isDateTimeOffset = false;

            PrimitiveTypeKind typeKind;
            bool isPrimitiveType = MetadataHelpers.TryGetPrimitiveTypeKind(e.Arguments[0].ResultType, out typeKind);
            Debug.Assert(isPrimitiveType, "Expecting primitive type as input parameter to TruncateTime");

            if (typeKind == PrimitiveTypeKind.DateTime)
            {
                typeName = sqlgen.IsPreKatmai
                               ? "datetime"
                               : "datetime2";
            }
            else if (typeKind == PrimitiveTypeKind.DateTimeOffset)
            {
                typeName = "datetimeoffset";
                isDateTimeOffset = true;
            }
            else
            {
                Debug.Assert(true, "Unexpected type to TruncateTime" + typeKind);
            }

            SqlBuilder result = new SqlBuilder();
            result.Append("convert (");
            result.Append(typeName);
            result.Append(", convert(varchar(255), ");
            result.Append(e.Arguments[0].Accept(sqlgen));
            result.Append(", 102) ");

            if (isDateTimeOffset)
            {
                result.Append("+ ' 00:00:00 ' +  Right(convert(varchar(255), ");
                result.Append(e.Arguments[0].Accept(sqlgen));
                result.Append(", 121), 6)  ");
            }

            result.Append(",  102)");
            return result;
        }

        /// <summary>
        ///   Handler for date addition functions supported only starting from Katmai
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionDateAddKatmaiOrNewer
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            sqlgen.AssertKatmaiOrNewer(e);
            return HandleCanonicalFunctionDateAdd(sqlgen, e);
        }

        /// <summary>
        ///   Handler for all date/time addition canonical functions.
        ///   Translation, e.g.
        ///   AddYears(datetime, number) =>  DATEADD(year, number, datetime)
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionDateAdd
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            SqlBuilder result = new SqlBuilder();

            result.Append("DATEADD (");
            result.Append(_dateAddFunctionNameToDatepartDictionary[e.Function.Name]);
            result.Append(", ");
            result.Append(e.Arguments[1].Accept(sqlgen));
            result.Append(", ");
            result.Append(e.Arguments[0].Accept(sqlgen));
            result.Append(")");

            return result;
        }

        /// <summary>
        ///   Hanndler for date differencing functions supported only starting from Katmai
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionDateDiffKatmaiOrNewer
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            sqlgen.AssertKatmaiOrNewer(e);
            return HandleCanonicalFunctionDateDiff(sqlgen, e);
        }

        /// <summary>
        ///   Handler for all date/time addition canonical functions.
        ///   Translation, e.g.
        ///   DiffYears(datetime, number) =>  DATEDIFF(year, number, datetime)
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionDateDiff
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            SqlBuilder result = new SqlBuilder();

            result.Append("DATEDIFF (");
            result.Append(_dateDiffFunctionNameToDatepartDictionary[e.Function.Name]);
            result.Append(", ");
            result.Append(e.Arguments[0].Accept(sqlgen));
            result.Append(", ");
            result.Append(e.Arguments[1].Accept(sqlgen));
            result.Append(")");

            return result;
        }

        /// <summary>
        ///   Function rename IndexOf -> CHARINDEX
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionIndexOf
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            return sqlgen.HandleFunctionDefaultGivenName(e, "CHARINDEX");
        }

        /// <summary>
        ///   Function rename NewGuid -> NEWID
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionNewGuid
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            return sqlgen.HandleFunctionDefaultGivenName(e, "NEWID");
        }

        /// <summary>
        ///   Function rename Length -> LEN
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionLength
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            // We are aware of SQL Server's trimming of trailing spaces. We disclaim that behavior in general.
            // It's up to the user to decide whether to trim them explicitly or to append a non-blank space char explicitly.
            // Once SQL Server implements a function that computes Length correctly, we'll use it here instead of LEN,
            // and we'll drop the disclaimer. 
            return sqlgen.HandleFunctionDefaultGivenName(e, "LEN");
        }

        /// <summary>
        ///   Round(numericExpression) -> Round(numericExpression, 0);
        ///   Round(numericExpression, digits) -> Round(numericExpression, digits);
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionRound
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            return sqlgen.HandleCanonicalFunctionRoundOrTruncate(e, true);
        }

        /// <summary>
        ///   Truncate(numericExpression) -> Round(numericExpression, 0, 1); (does not exist as canonical function yet)
        ///   Truncate(numericExpression, digits) -> Round(numericExpression, digits, 1);
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionTruncate
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            return sqlgen.HandleCanonicalFunctionRoundOrTruncate(e, false);
        }

        /// <summary>
        ///   Common handler for the canonical functions ROUND and TRUNCATE
        /// </summary>
        /// <param name = "e"></param>
        /// <param name = "round"></param>
        /// <returns></returns>
        ISqlFragment HandleCanonicalFunctionRoundOrTruncate
            (
            DbFunctionExpression e,
            bool round)
        {
            SqlBuilder result = new SqlBuilder();

            result.Append("ROUND(");

            Debug.Assert(e.Arguments.Count <= 2, "Round or truncate should have at most 2 arguments");
            result.Append(e.Arguments[0].Accept(this));
            result.Append(", ");

            if (e.Arguments.Count > 1)
            {
                result.Append(e.Arguments[1].Accept(this));
            }
            else
            {
                result.Append("0");
            }

            if (!round)
            {
                result.Append(", 1");
            }

            result.Append(")");

            return result;
        }

        /// <summary>
        ///   Handle the canonical function Abs().
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionAbs
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            // Convert the call to Abs(Byte) to a no-op, since Byte is an unsigned type. 
            if (MetadataHelpers.IsPrimitiveType(e.Arguments[0].ResultType, PrimitiveTypeKind.Byte))
            {
                SqlBuilder result = new SqlBuilder();
                result.Append(e.Arguments[0].Accept(sqlgen));
                return result;
            }
            else
            {
                return sqlgen.HandleFunctionDefault(e);
            }
        }

        /// <summary>
        ///   TRIM(string) -> LTRIM(RTRIM(string))
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionTrim
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            SqlBuilder result = new SqlBuilder();

            result.Append("LTRIM(RTRIM(");

            Debug.Assert(e.Arguments.Count == 1, "Trim should have one argument");
            result.Append(e.Arguments[0].Accept(sqlgen));

            result.Append("))");

            return result;
        }

        /// <summary>
        ///   Function rename ToLower -> LOWER
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionToLower
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            return sqlgen.HandleFunctionDefaultGivenName(e, "LOWER");
        }

        /// <summary>
        ///   Function rename ToUpper -> UPPER
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionToUpper
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            return sqlgen.HandleFunctionDefaultGivenName(e, "UPPER");
        }

        /// <summary>
        ///   Function to translate the StartsWith, EndsWith and Contains canonical functions to LIKE expression in T-SQL
        ///   and also add the trailing ESCAPE '~' when escaping of the search string for the LIKE expression has occurred
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "targetExpression"></param>
        /// <param name = "constSearchParamExpression"></param>
        /// <param name = "result"></param>
        /// <param name = "insertPercentStart"></param>
        /// <param name = "insertPercentEnd"></param>
        void TranslateConstantParameterForLike
            (
            DbExpression targetExpression,
            DbConstantExpression constSearchParamExpression,
            SqlBuilder result,
            bool insertPercentStart,
            bool insertPercentEnd)
        {
            result.Append(targetExpression.Accept(this));
            result.Append(" LIKE ");

            // If it's a DbConstantExpression then escape the search parameter if necessary.
            bool escapingOccurred;

            StringBuilder searchParamBuilder = new StringBuilder();
            if (insertPercentStart)
            {
                searchParamBuilder.Append("%");
            }
            searchParamBuilder.Append
                (
                    SqlProviderManifest.EscapeLikeText
                        (constSearchParamExpression.Value as string, false, out escapingOccurred));
            if (insertPercentEnd)
            {
                searchParamBuilder.Append("%");
            }

            result.Append(VisitConstantExpression(constSearchParamExpression.ResultType, searchParamBuilder.ToString()));

            // If escaping did occur (special characters were found), then append the escape character used.
            if (escapingOccurred)
            {
                result.Append(" ESCAPE '" + SqlProviderManifest.LikeEscapeChar + "'");
            }
        }

        /// <summary>
        ///   Handler for Contains. Wraps the normal translation with a case statement
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionContains
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            return sqlgen.WrapPredicate(HandleCanonicalFunctionContains, e);
        }

        /// <summary>
        ///   CONTAINS(arg0, arg1) => arg0 LIKE '%arg1%'
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "args"></param>
        /// <param name = "result"></param>
        /// <returns></returns>
        static SqlBuilder HandleCanonicalFunctionContains
            (
            SqlGenerator sqlgen,
            IList<DbExpression> args,
            SqlBuilder result)
        {
            Debug.Assert(args.Count == 2, "Contains should have two arguments");
            // Check if args[1] is a DbConstantExpression
            DbConstantExpression constSearchParamExpression = args[1] as DbConstantExpression;
            if (( constSearchParamExpression != null )
                && ( string.IsNullOrEmpty(constSearchParamExpression.Value as string) == false ))
            {
                sqlgen.TranslateConstantParameterForLike(args[0], constSearchParamExpression, result, true, true);
            }
            else
            {
                // We use CHARINDEX when the search param is a DbNullExpression because all of SQL Server 2008, 2005 and 2000
                // consistently return NULL as the result.
                //  However, if instead we use the optimized LIKE translation when the search param is a DbNullExpression,
                //  only SQL Server 2005 yields a True instead of a DbNull as compared to SQL Server 2008 and 2000.
                result.Append("CHARINDEX( ");
                result.Append(args[1].Accept(sqlgen));
                result.Append(", ");
                result.Append(args[0].Accept(sqlgen));
                result.Append(") > 0");
            }
            return result;
        }

        /// <summary>
        ///   Handler for StartsWith. Wraps the normal translation with a case statement
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionStartsWith
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            return sqlgen.WrapPredicate(HandleCanonicalFunctionStartsWith, e);
        }

        /// <summary>
        ///   STARTSWITH(arg0, arg1) => arg0 LIKE 'arg1%'
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "args"></param>
        /// <param name = "result"></param>
        /// <returns></returns>
        static SqlBuilder HandleCanonicalFunctionStartsWith
            (
            SqlGenerator sqlgen,
            IList<DbExpression> args,
            SqlBuilder result)
        {
            Debug.Assert(args.Count == 2, "StartsWith should have two arguments");
            // Check if args[1] is a DbConstantExpression
            DbConstantExpression constSearchParamExpression = args[1] as DbConstantExpression;
            if (( constSearchParamExpression != null )
                && ( string.IsNullOrEmpty(constSearchParamExpression.Value as string) == false ))
            {
                sqlgen.TranslateConstantParameterForLike(args[0], constSearchParamExpression, result, false, true);
            }
            else
            {
                // We use CHARINDEX when the search param is a DbNullExpression because all of SQL Server 2008, 2005 and 2000
                // consistently return NULL as the result.
                //      However, if instead we use the optimized LIKE translation when the search param is a DbNullExpression,
                //      only SQL Server 2005 yields a True instead of a DbNull as compared to SQL Server 2008 and 2000. This is
                //      bug 32315 in LIKE in SQL Server 2005.
                result.Append("CHARINDEX( ");
                result.Append(args[1].Accept(sqlgen));
                result.Append(", ");
                result.Append(args[0].Accept(sqlgen));
                result.Append(") = 1");
            }

            return result;
        }

        /// <summary>
        ///   Handler for EndsWith. Wraps the normal translation with a case statement
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        static ISqlFragment HandleCanonicalFunctionEndsWith
            (
            SqlGenerator sqlgen,
            DbFunctionExpression e)
        {
            return sqlgen.WrapPredicate(HandleCanonicalFunctionEndsWith, e);
        }

        /// <summary>
        ///   ENDSWITH(arg0, arg1) => arg0 LIKE '%arg1'
        /// </summary>
        /// <param name = "sqlgen"></param>
        /// <param name = "args"></param>
        /// <param name = "result"></param>
        /// <returns></returns>
        static SqlBuilder HandleCanonicalFunctionEndsWith
            (
            SqlGenerator sqlgen,
            IList<DbExpression> args,
            SqlBuilder result)
        {
            Debug.Assert(args.Count == 2, "EndsWith should have two arguments");

            // Check if args[1] is a DbConstantExpression and if args [0] is a DbPropertyExpression
            DbConstantExpression constSearchParamExpression = args[1] as DbConstantExpression;
            DbPropertyExpression targetParamExpression = args[0] as DbPropertyExpression;
            if (( constSearchParamExpression != null ) && ( targetParamExpression != null )
                && ( string.IsNullOrEmpty(constSearchParamExpression.Value as string) == false ))
            {
                // The LIKE optimization for EndsWith can only be used when the target is a column in table and
                // the search string is a constant. This is because SQL Server ignores a trailing space in a query like:
                // EndsWith('abcd ', 'cd'), which translates to:
                //      SELECT
                //      CASE WHEN ('abcd ' LIKE '%cd') THEN cast(1 as bit) WHEN ( NOT ('abcd ' LIKE '%cd')) THEN cast(0 as bit) END AS [C1]
                //      FROM ( SELECT 1 AS X ) AS [SingleRowTable1]
                // and "incorrectly" returns 1 (true), but the CLR would expect a 0 (false) back.

                sqlgen.TranslateConstantParameterForLike(args[0], constSearchParamExpression, result, true, false);
            }
            else
            {
                result.Append("CHARINDEX( REVERSE(");
                result.Append(args[1].Accept(sqlgen));
                result.Append("), REVERSE(");
                result.Append(args[0].Accept(sqlgen));
                result.Append(")) = 1");
            }
            return result;
        }

        /// <summary>
        ///   Turns a predicate into a statement returning a bit
        ///   PREDICATE => CASE WHEN (PREDICATE) THEN CAST(1 AS BIT) WHEN (NOT (PREDICATE)) CAST (O AS BIT) END
        ///   The predicate is produced by the given predicateTranslator.
        /// </summary>
        /// <param name = "predicateTranslator"></param>
        /// <param name = "e"></param>
        /// <returns></returns>
        ISqlFragment WrapPredicate
            (
            Func<SqlGenerator, IList<DbExpression>, SqlBuilder, SqlBuilder> predicateTranslator,
            DbFunctionExpression e)
        {
            SqlBuilder result = new SqlBuilder();
            result.Append("CASE WHEN (");
            predicateTranslator(this, e.Arguments, result);
            result.Append(") THEN cast(1 as bit) WHEN ( NOT (");
            predicateTranslator(this, e.Arguments, result);
            result.Append(")) THEN cast(0 as bit) END");
            return result;
        }

        #endregion


        #region Helper methods for the DbExpressionVisitor

        ///<summary>
        ///  <see cref = "AddDefaultColumns" />
        ///  Add the column names from the referenced extent/join to the
        ///  select statement.
        ///
        ///  If the symbol is a JoinSymbol, we recursively visit all the extents,
        ///  halting at real extents and JoinSymbols that have an associated SqlSelectStatement.
        ///
        ///  The column names for a real extent can be derived from its type.
        ///  The column names for a Join Select statement can be got from the
        ///  list of columns that was created when the Join's select statement
        ///  was created.
        ///
        ///  We do the following for each column.
        ///  <list type = "number">
        ///    <item>Add the SQL string for each column to the SELECT clause</item>
        ///    <item>Add the column to the list of columns - so that it can
        ///      become part of the "type" of a JoinSymbol</item>
        ///    <item>Check if the column name collides with a previous column added
        ///      to the same select statement.  Flag both the columns for renaming if true.</item>
        ///    <item>Add the column to a name lookup dictionary for collision detection.</item>
        ///  </list>
        ///</summary>
        ///<param name = "selectStatement">The select statement that started off as SELECT *</param>
        ///<param name = "aliasOrSubquery">The symbol containing the type information for
        ///  the columns to be added.</param>
        ///<param name = "columnList">Columns that have been added to the Select statement.
        ///  This is created in <see cref = "AddDefaultColumns" />.</param>
        ///<param name = "columnDictionary">A dictionary of the columns above.</param>
        ///<param name = "separator">Comma or nothing, depending on whether the SELECT
        ///  clause is empty.</param>
        void AddColumns
            (
            SqlSelectStatement selectStatement,
            AliasOrSubquery aliasOrSubquery,
            List<AliasOrSubquery> columnList,
            Dictionary<string, AliasOrSubquery> columnDictionary,
            ref string separator)
        {
            JoinAlias joinAlias = aliasOrSubquery as JoinAlias;
            if (joinAlias != null)
            {
                if (!joinAlias.IsNestedJoin)
                {
                    // Recurse if the join symbol is a collection of flattened extents
                    foreach (AliasOrSubquery sym in joinAlias.ExtentList)
                    {
                        // if sym is ScalarType means we are at base case in the
                        // recursion and there are not columns to add, just skip
                        if (MetadataHelpers.IsPrimitiveType(sym.Type))
                        {
                            continue;
                        }

                        AddColumns(selectStatement, sym, columnList, columnDictionary, ref separator);
                    }
                }
                else
                {
                    foreach (AliasOrSubquery joinColumn in joinAlias.ColumnList)
                    {
                        // we write tableName.columnName
                        // rather than tableName.columnName as alias
                        // since the column name is unique (by the way we generate new column names)
                        //
                        // We use the symbols for both the table and the column,
                        // since they are subject to renaming.
                        selectStatement.Select.Append(separator);
                        selectStatement.Select.Append(aliasOrSubquery);
                        selectStatement.Select.Append(".");
                        selectStatement.Select.Append(joinColumn);

                        // check for name collisions.  If there is,
                        // flag both the colliding symbols.
                        if (columnDictionary.ContainsKey(joinColumn.Name))
                        {
                            columnDictionary[joinColumn.Name].NeedsRenaming = true; // the original symbol
                            joinColumn.NeedsRenaming = true; // the current symbol.
                        }
                        else
                        {
                            columnDictionary[joinColumn.Name] = joinColumn;
                        }

                        columnList.Add(joinColumn);

                        separator = ", ";
                    }
                }
            }
            else
            {
                // This is a non-join extent/select statement, and the CQT type has
                // the relevant column information.

                // The type could be a record type(e.g. Project(...),
                // or an entity type ( e.g. EntityExpression(...)
                // so, we check whether it is a structuralType.

                // Consider an expression of the form J(a, b=P(E))
                // The inner P(E) would have been translated to a SQL statement
                // We should not use the raw names from the type, but the equivalent
                // symbols (they are present in symbol.Columns) if they exist.
                //
                // We add the new columns to the symbol's columns if they do
                // not already exist.
                //

                foreach (EdmProperty property in MetadataHelpers.GetProperties(aliasOrSubquery.Type))
                {
                    string recordMemberName = property.Name;
                    // Since all renaming happens in the second phase
                    // we lose nothing by setting the next column name index to 0
                    // many times.
                    allColumnNames[recordMemberName] = 0;

                    // Create a new symbol/reuse existing symbol for the column
                    AliasOrSubquery columnAliasOrSubquery;
                    if (!aliasOrSubquery.Columns.TryGetValue(recordMemberName, out columnAliasOrSubquery))
                    {
                        // we do not care about the types of columns, so we pass null
                        // when construction the symbol.
                        columnAliasOrSubquery = new AliasOrSubquery(recordMemberName, null);
                        aliasOrSubquery.Columns.Add(recordMemberName, columnAliasOrSubquery);
                    }

                    selectStatement.Select.Append(separator);
                    selectStatement.Select.Append(aliasOrSubquery);
                    selectStatement.Select.Append(".");

                    // We use the actual name before the "AS", the new name goes
                    // after the AS.
                    selectStatement.Select.Append(QuoteIdentifier(recordMemberName));

                    selectStatement.Select.Append(" AS ");
                    selectStatement.Select.Append(columnAliasOrSubquery);

                    // Check for column name collisions.
                    if (columnDictionary.ContainsKey(recordMemberName))
                    {
                        columnDictionary[recordMemberName].NeedsRenaming = true;
                        columnAliasOrSubquery.NeedsRenaming = true;
                    }
                    else
                    {
                        columnDictionary[recordMemberName] = aliasOrSubquery.Columns[recordMemberName];
                    }

                    columnList.Add(columnAliasOrSubquery);

                    separator = ", ";
                }
            }
        }

        ///<summary>
        ///  Expands Select * to "select the_list_of_columns"
        ///  If the columns are taken from an extent, they are written as
        ///  {original_column_name AS Symbol(original_column)} to allow renaming.
        ///
        ///  If the columns are taken from a Join, they are written as just
        ///  {original_column_name}, since there cannot be a name collision.
        ///
        ///  We concatenate the columns from each of the inputs to the select statement.
        ///  Since the inputs may be joins that are flattened, we need to recurse.
        ///  The inputs are inferred from the symbols in FromExtents.
        ///</summary>
        ///<param name = "selectStatement"></param>
        ///<returns></returns>
        protected virtual List<AliasOrSubquery> AddDefaultColumns(SqlSelectStatement selectStatement)
        {
            // This is the list of columns added in this select statement
            // This forms the "type" of the Select statement, if it has to
            // be expanded in another SELECT *
            List<AliasOrSubquery> columnList = new List<AliasOrSubquery>();

            // A lookup for the previous set of columns to aid column name
            // collision detection.
            Dictionary<string, AliasOrSubquery> columnDictionary = new Dictionary<string, AliasOrSubquery>
                (StringComparer.OrdinalIgnoreCase);

            string separator = "";
            // The Select should usually be empty before we are called,
            // but we do not mind if it is not.
            if (!selectStatement.Select.IsEmpty)
            {
                separator = ", ";
            }

            foreach (AliasOrSubquery symbol in selectStatement.FromExtents)
            {
                AddColumns(selectStatement, symbol, columnList, columnDictionary, ref separator);
            }

            return columnList;
        }

        ///<summary>
        ///  This method is called after the input to a relational node is visited.
        ///  <see cref = "Visit(DbProjectExpression)" /> and <see cref = "ProcessJoinInputResult" />
        ///  There are 2 scenarios
        ///  <list type = "number">
        ///    <item>The fromSymbol is new i.e. the select statement has just been
        ///      created, or a join extent has been added.</item>
        ///    <item>The fromSymbol is old i.e. we are reusing a select statement.</item>
        ///  </list>
        ///
        ///  If we are not reusing the select statement, we have to complete the
        ///  FROM clause with the alias
        ///  <code>
        ///    -- if the input was an extent
        ///    FROM = [SchemaName].[TableName]
        ///    -- if the input was a Project
        ///    FROM = (SELECT ... FROM ... WHERE ...)
        ///  </code>
        ///
        ///  These become
        ///  <code>
        ///    -- if the input was an extent
        ///    FROM = [SchemaName].[TableName] AS alias
        ///    -- if the input was a Project
        ///    FROM = (SELECT ... FROM ... WHERE ...) AS alias
        ///  </code>
        ///  and look like valid FROM clauses.
        ///
        ///  Finally, we have to add the alias to the global list of aliases used,
        ///  and also to the current symbol table.
        ///</summary>
        ///<param name = "selectStatement"></param>
        ///<param name = "inputVarName">The alias to be used.</param>
        ///<param name = "fromAliasOrSubquery"></param>
        ///<param name = "addToNamingScopes"></param>
        void AddAliasToFrom
            (
                SqlSelectStatement selectStatement,
                string inputVarName,
                AliasOrSubquery fromAliasOrSubquery,
                bool addToNamingScopes = true
            )
        {
            if (addToNamingScopes)
            {
                namingScopes.Add(inputVarName, fromAliasOrSubquery);
            }
            if ( ! ShouldAddTableAlias(selectStatement, fromAliasOrSubquery))
            {
                // We do not want to add "AS alias" if it has been done already
                // e.g. when we are reusing the Sql statement.
                return;
            }
            selectStatement.FromExtents.Add(fromAliasOrSubquery);
            selectStatement.From.Append(" "); //I prefer not to use " AS " for table aliases
            selectStatement.From.Append(fromAliasOrSubquery);

            // We have this inside the if statement, since
            // we only want to add extents that are actually used.
            allExtentNames[fromAliasOrSubquery.Name] = 0;
        }

        bool ShouldAddTableAlias
            (
                SqlSelectStatement selectStatement,
                AliasOrSubquery fromAliasOrSubquery
            )
        {
            return ShouldAddAliases()
                && (IsNewSelectStatement(selectStatement)
                   || InJoin(selectStatement, fromAliasOrSubquery));
        }

        bool InJoin(SqlSelectStatement selectStatement,
                    AliasOrSubquery fromAliasOrSubquery)
        {
            return fromAliasOrSubquery != selectStatement.FromExtents[0];
        }

        bool IsNewSelectStatement(SqlSelectStatement selectStatement)
        {
            return selectStatement.FromExtents.Count == 0;
        }

        /// <summary>
        ///   Translates a list of SortClauses.
        ///   Used in the translation of OrderBy
        /// </summary>
        /// <param name = "orderByClause">The SqlBuilder to which the sort keys should be appended</param>
        /// <param name = "sortKeys"></param>
        void AddSortKeys
            (
                SqlBuilder orderByClause,
                IList<DbSortClause> sortKeys
            )
        {
            string separator = "";
            foreach (DbSortClause sortClause in sortKeys)
            {
                AddSortKeySeparator(orderByClause, separator);
                AddSortKey(orderByClause, sortClause);
                separator = ", ";
            }
        }

        protected virtual void AddSortKeySeparator
            (
                SqlBuilder orderByClause,
                string separator
            )
        {
            orderByClause.Append(separator);
        }

        protected virtual void AddSortKey
            (
                SqlBuilder orderByClause,
                DbSortClause sortClause
            )
        {
            orderByClause.Append(sortClause.Expression.Accept(this));
            Debug.Assert(sortClause.Collation != null);
            if (!String.IsNullOrEmpty(sortClause.Collation))
            {
                orderByClause.Append(" COLLATE ");
                orderByClause.Append(sortClause.Collation);
            }

            orderByClause.Append
                (
                    sortClause.Ascending
                        ? " ASC"
                        : " DESC");
        }

        /// <summary>
        ///   <see cref = "CreateNewSelectStatement(SqlSelectStatement oldStatement, string inputVarName, TypeUsage inputVarType, bool finalizeOldStatement, out AliasOrSubquery fromSymbol) " />
        /// </summary>
        /// <param name = "oldStatement"></param>
        /// <param name = "inputVarName"></param>
        /// <param name = "inputVarType"></param>
        /// <param name = "fromAliasOrSubquery"></param>
        /// <returns>A new select statement, with the old one as the from clause.</returns>
        SqlSelectStatement CreateNewSelectStatement
            (
            SqlSelectStatement oldStatement,
            string inputVarName,
            TypeUsage inputVarType,
            out AliasOrSubquery fromAliasOrSubquery)
        {
            return CreateNewSelectStatement(oldStatement, inputVarName, inputVarType, true, out fromAliasOrSubquery);
        }


        ///<summary>
        ///  This is called after a relational node's input has been visited, and the
        ///  input's sql statement cannot be reused.  <see cref = "Visit(DbProjectExpression)" />
        ///
        ///  When the input's sql statement cannot be reused, we create a new sql
        ///  statement, with the old one as the from clause of the new statement.
        ///
        ///  The old statement must be completed i.e. if it has an empty select list,
        ///  the list of columns must be projected out.
        ///
        ///  If the old statement being completed has a join symbol as its from extent,
        ///  the new statement must have a clone of the join symbol as its extent.
        ///  We cannot reuse the old symbol, but the new select statement must behave
        ///  as though it is working over the "join" record.
        ///</summary>
        ///<param name = "oldStatement"></param>
        ///<param name = "inputVarName"></param>
        ///<param name = "inputVarType"></param>
        ///<param name = "finalizeOldStatement"></param>
        ///<param name = "fromAliasOrSubquery"></param>
        ///<returns>A new select statement, with the old one as the from clause.</returns>
        SqlSelectStatement CreateNewSelectStatement
            (
                SqlSelectStatement oldStatement,
                string inputVarName,
                TypeUsage inputVarType,
                bool finalizeOldStatement,
                out AliasOrSubquery fromAliasOrSubquery
            )
        {
            oldStatement.IsTopMost = false;
            fromAliasOrSubquery = null;

            // Finalize the old statement
            if (finalizeOldStatement 
                && IsProjectionEmpty(oldStatement))
            {
                List<AliasOrSubquery> columns = AddDefaultColumns(oldStatement);

                // Thid could not have been called from a join node.
                Debug.Assert(oldStatement.FromExtents.Count == 1);

                // if the oldStatement has a join as its input, ...
                // clone the join symbol, so that we "reuse" the
                // join symbol.  Normally, we create a new symbol - see the next block
                // of code.
                JoinAlias oldJoinAlias = oldStatement.FromExtents[0] as JoinAlias;
                if (oldJoinAlias != null)
                {
                    // Note: oldStatement.FromExtents will not do, since it might
                    // just be an alias of joinSymbol, and we want an actual JoinSymbol.
                    JoinAlias newJoinAlias = new JoinAlias(inputVarName, inputVarType, oldJoinAlias.ExtentList);
                    // This indicates that the oldStatement is a blocking scope
                    // i.e. it hides/renames extent columns
                    newJoinAlias.IsNestedJoin = true;
                    newJoinAlias.ColumnList = columns;
                    newJoinAlias.FlattenedExtentList = oldJoinAlias.FlattenedExtentList;

                    fromAliasOrSubquery = newJoinAlias;
                }
            }

            if (fromAliasOrSubquery == null)
            {
                // This is just a simple extent/SqlSelectStatement,
                // and we can get the column list from the type.
                fromAliasOrSubquery = new AliasOrSubquery(inputVarName, inputVarType);
            }

            // Observe that the following looks like the body of Visit(ExtentExpression).
            SqlSelectStatement selectStatement = CreateSelectStatement();
            AddOldSelectStatement(selectStatement, oldStatement);

            return selectStatement;
        }

        protected virtual void AddOldSelectStatement
            (
                SqlSelectStatement newSelectStatement
                , SqlSelectStatement oldStatement
            )
        {
            newSelectStatement.From.Append("( ");
            newSelectStatement.From.Append(oldStatement);
            newSelectStatement.From.AppendLine();
            newSelectStatement.From.Append(") ");
        }

        /// <summary>
        ///   Before we embed a string literal in a SQL string, we should
        ///   convert all ' to '', and enclose the whole string in single quotes.
        /// </summary>
        /// <param name = "s"></param>
        /// <param name = "isUnicode"></param>
        /// <returns>The escaped sql string.</returns>
        protected static string EscapeSingleQuote
            (
            string s,
            bool isUnicode)
        {
            return ( isUnicode
                         ? "N'"
                         : "'" ) + s.Replace("'", "''") + "'";
        }

        /// <summary>
        ///   Returns the sql primitive/native type name. 
        ///   It will include size, precision or scale depending on type information present in the 
        ///   type facets
        /// </summary>
        /// <param name = "type"></param>
        /// <returns></returns>
        string GetSqlPrimitiveType(TypeUsage type)
        {
            PrimitiveType primitiveType = MetadataHelpers.GetEdmType<PrimitiveType>(type);

            string typeName = primitiveType.Name;
            bool isUnicode = true;
            bool isFixedLength = false;
            int maxLength = 0;
            string length = "max";
            bool preserveSeconds = true;
            byte decimalPrecision = 0;
            byte decimalScale = 0;

            switch (primitiveType.PrimitiveTypeKind)
            {
                case PrimitiveTypeKind.Binary :
                    maxLength = MetadataHelpers.GetFacetValueOrDefault
                        (type, MetadataHelpers.MaxLengthFacetName, MetadataHelpers.BinaryMaxMaxLength);
                    if (maxLength == MetadataHelpers.BinaryMaxMaxLength)
                    {
                        length = "max";
                    }
                    else
                    {
                        length = maxLength.ToString(CultureInfo.InvariantCulture);
                    }
                    isFixedLength = MetadataHelpers.GetFacetValueOrDefault
                        (type, MetadataHelpers.FixedLengthFacetName, false);
                    typeName = ( isFixedLength
                                     ? "binary("
                                     : "varbinary(" ) + length + ")";
                    break;

                case PrimitiveTypeKind.String :
                    // Question: How do we handle ntext?
                    isUnicode = MetadataHelpers.GetFacetValueOrDefault(type, MetadataHelpers.UnicodeFacetName, true);
                    isFixedLength = MetadataHelpers.GetFacetValueOrDefault
                        (type, MetadataHelpers.FixedLengthFacetName, false);
                    maxLength = MetadataHelpers.GetFacetValueOrDefault
                        (type, MetadataHelpers.MaxLengthFacetName, Int32.MinValue);
                    if (maxLength == Int32.MinValue)
                    {
                        length = "max";
                    }
                    else
                    {
                        length = maxLength.ToString(CultureInfo.InvariantCulture);
                    }
                    if (isUnicode && !isFixedLength && maxLength > 4000)
                    {
                        length = "max";
                    }
                    if (!isUnicode && !isFixedLength && maxLength > 8000)
                    {
                        length = "max";
                    }
                    if (isFixedLength)
                    {
                        typeName = ( isUnicode
                                         ? "nchar("
                                         : "char(" ) + length + ")";
                    }
                    else
                    {
                        typeName = ( isUnicode
                                         ? "nvarchar("
                                         : "varchar(" ) + length + ")";
                    }
                    break;

                case PrimitiveTypeKind.DateTime :
                    preserveSeconds = MetadataHelpers.GetFacetValueOrDefault
                        (type, MetadataHelpers.PreserveSecondsFacetName, false);
                    typeName = preserveSeconds
                                   ? ( IsPreKatmai
                                           ? "datetime2"
                                           : "datetime" )
                                   : "smalldatetime";
                    break;

                case PrimitiveTypeKind.Time :
                    AssertKatmaiOrNewer(primitiveType.PrimitiveTypeKind);
                    typeName = "time";
                    break;

                case PrimitiveTypeKind.DateTimeOffset :
                    AssertKatmaiOrNewer(primitiveType.PrimitiveTypeKind);
                    typeName = "datetimeoffset";
                    break;

                case PrimitiveTypeKind.Decimal :
                    decimalPrecision = MetadataHelpers.GetFacetValueOrDefault<byte>
                        (type, MetadataHelpers.PrecisionFacetName, 18);
                    Debug.Assert(decimalPrecision > 0, "decimal precision must be greater than zero");
                    decimalScale = MetadataHelpers.GetFacetValueOrDefault<byte>(type, MetadataHelpers.ScaleFacetName, 0);
                    Debug.Assert
                        (decimalPrecision >= decimalScale, "decimalPrecision must be greater or equal to decimalScale");
                    Debug.Assert(decimalPrecision <= 38, "decimalPrecision must be less than or equal to 38");
                    typeName = typeName + "(" + decimalPrecision + "," + decimalScale + ")";
                    break;

                case PrimitiveTypeKind.Int32 :
                    typeName = "int";
                    break;

                case PrimitiveTypeKind.Int64 :
                    typeName = "bigint";
                    break;

                case PrimitiveTypeKind.Int16 :
                    typeName = "smallint";
                    break;

                case PrimitiveTypeKind.Byte :
                    typeName = "tinyint";
                    break;

                case PrimitiveTypeKind.Boolean :
                    typeName = "bit";
                    break;

                case PrimitiveTypeKind.Single :
                    typeName = "real";
                    break;

                case PrimitiveTypeKind.Double :
                    typeName = "float";
                    break;

                case PrimitiveTypeKind.Guid :
                    typeName = "uniqueidentifier";
                    break;

                default :
                    throw new NotSupportedException("Unsupported EdmType: " + primitiveType.PrimitiveTypeKind);
            }

            return typeName;
        }

        /// <summary>
        ///   Handles the expression represending DbLimitExpression.Limit and DbSkipExpression.Count.
        ///   If it is a constant expression, it simply does to string thus avoiding casting it to the specific value
        ///   (which would be done if <see cref = "Visit(DbConstantExpression)" /> is called)
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        ISqlFragment HandleCountExpression(DbExpression e)
        {
            ISqlFragment result;

            if (e.ExpressionKind == DbExpressionKind.Constant)
            {
                //For constant expression we should not cast the value, 
                // thus we don't go throught the default DbConstantExpression handling
                SqlBuilder sqlBuilder = new SqlBuilder();
                sqlBuilder.Append(( (DbConstantExpression)e ).Value.ToString());
                result = sqlBuilder;
            }
            else
            {
                result = e.Accept(this);
            }

            return result;
        }

        /// <summary>
        ///   This is used to determine if a particular expression is an Apply operation.
        ///   This is only the case when the DbExpressionKind is CrossApply or OuterApply.
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        static bool IsApplyExpression(DbExpression e)
        {
            return ( DbExpressionKind.CrossApply == e.ExpressionKind || DbExpressionKind.OuterApply == e.ExpressionKind );
        }

        /// <summary>
        ///   This is used to determine if a particular expression is a Join operation.
        ///   This is true for DbCrossJoinExpression and DbJoinExpression, the
        ///   latter of which may have one of several different ExpressionKinds.
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        static bool IsJoinExpression(DbExpression e)
        {
            return ( DbExpressionKind.CrossJoin == e.ExpressionKind ||
                     DbExpressionKind.FullOuterJoin == e.ExpressionKind ||
                     DbExpressionKind.InnerJoin == e.ExpressionKind ||
                     DbExpressionKind.LeftOuterJoin == e.ExpressionKind );
        }

        ///<summary>
        ///  This is used to determine if a calling expression needs to place
        ///  round brackets around the translation of the expression e.
        ///
        ///  Constants, parameters and properties do not require brackets,
        ///  everything else does.
        ///</summary>
        ///<param name = "e"></param>
        ///<returns>true, if the expression needs brackets </returns>
        static bool IsComplexExpression(DbExpression e)
        {
            switch (e.ExpressionKind)
            {
                case DbExpressionKind.Constant :
                case DbExpressionKind.ParameterReference :
                case DbExpressionKind.Property :
                    return false;

                default :
                    return true;
            }
        }

        /// <summary>
        ///   Determine if the owner expression can add its unique sql to the input's
        ///   SqlSelectStatement.
        /// 
        /// GroupBy is compatible with Filter and OrderBy
        /// but not with Project, GroupBy
        /// </summary>
        /// <param name = "result">The SqlSelectStatement of the input to the relational node.</param>
        /// <param name = "expressionKind">The kind of the expression node(not the input's)</param>
        /// <returns></returns>
        protected virtual bool IsCompatibleWithCurrentSelectStatement
            (
                SqlSelectStatement result,
                DbExpressionKind expressionKind
            )
        {
            switch (expressionKind)
            {
                case DbExpressionKind.Distinct :
                    return result.Top == null
                           // The projection after distinct may not project all 
                           // columns used in the Order By
                           && result.OrderBy.IsEmpty;

                case DbExpressionKind.Filter :
                    return IsProjectionEmpty(result)
                           && result.Where.IsEmpty
                           && result.GroupBy.IsEmpty
                           && result.Top == null;

                case DbExpressionKind.GroupBy :
                    return IsProjectionEmpty(result)
                           && result.GroupBy.IsEmpty
                           && result.OrderBy.IsEmpty
                           && result.Top == null;

                case DbExpressionKind.Limit :
                case DbExpressionKind.Element :
                    return result.Top == null;

                case DbExpressionKind.Project :
                    return IsProjectionEmpty(result)
                        && result.GroupBy.IsEmpty
                        && !result.IsDistinct;


                case DbExpressionKind.Skip :
                    return IsProjectionEmpty(result)
                           && result.GroupBy.IsEmpty
                           && result.OrderBy.IsEmpty
                           && !result.IsDistinct;

                case DbExpressionKind.Sort :
                    return IsProjectionEmpty(result)
                           && 
                           result.GroupBy.IsEmpty
                           && result.OrderBy.IsEmpty
                           && !result.IsDistinct;

                default :
                    Debug.Assert(false);
                    throw new InvalidOperationException();
            }
        }

        /// <summary>
        ///   We use the normal box quotes for SQL server.  We do not deal with ANSI quotes
        ///   i.e. double quotes.
        /// </summary>
        /// <param name = "name"></param>
        /// <returns></returns>
        protected internal static string QuoteIdentifier(string name)
        {
            Debug.Assert(!String.IsNullOrEmpty(name));
            // We assume that the names are not quoted to begin with.
            return "[" + name.Replace("]", "]]") + "]";
        }

        ///<summary>
        ///  This is called from <see cref = "GenerateSql(DbQueryCommandTree)" /> and nodes which require a
        ///  select statement as an argument e.g. <see cref = "Visit(DbIsEmptyExpression)" />,
        ///  <see cref = "Visit(DbUnionAllExpression)" />.
        ///
        ///  SqlGenerator needs its child to have a proper alias if the child is
        ///  just an extent or a join.
        ///
        ///  The normal relational nodes result in complete valid SQL statements.
        ///  For the rest, we need to treat them as there was a dummy
        ///  <code>
        ///    -- originally {expression}
        ///    -- change that to
        ///    SELECT *
        ///    FROM {expression} as c
        ///  </code>
        /// 
        ///  DbLimitExpression needs to start the statement but not add the default columns
        ///</summary>
        ///<param name = "e"></param>
        ///<param name = "addDefaultColumns"></param>
        ///<returns></returns>
        SqlSelectStatement VisitExpressionEnsureSqlStatement
            (
                DbExpression e,
                bool addDefaultColumns = true
            )
        {
            Debug.Assert(MetadataHelpers.IsCollectionType(e.ResultType));

            SqlSelectStatement result;
            switch (e.ExpressionKind)
            {
                case DbExpressionKind.Project :
                case DbExpressionKind.Filter :
                case DbExpressionKind.GroupBy :
                case DbExpressionKind.Sort :
                    result = e.Accept(this) as SqlSelectStatement;
                    break;

                default :
                    AliasOrSubquery fromAliasOrSubquery;
                    string inputVarName = "c"; // any name will do - this is my random choice.
                    namingScopes.EnterScope();

                    TypeUsage type = null;
                    switch (e.ExpressionKind)
                    {
                        case DbExpressionKind.Scan :
                        case DbExpressionKind.CrossJoin :
                        case DbExpressionKind.FullOuterJoin :
                        case DbExpressionKind.InnerJoin :
                        case DbExpressionKind.LeftOuterJoin :
                        case DbExpressionKind.CrossApply :
                        case DbExpressionKind.OuterApply :
                            type = MetadataHelpers.GetElementTypeUsage(e.ResultType);
                            break;

                        default :
                            Debug.Assert(MetadataHelpers.IsCollectionType(e.ResultType));
                            type = MetadataHelpers.GetEdmType<CollectionType>(e.ResultType).TypeUsage;
                            break;
                    }

                    result = VisitInputCollectionExpression(
                        new InputBindingExpression(e, inputVarName, type), 
                        out fromAliasOrSubquery);

                    AddAliasToFrom(result, inputVarName, fromAliasOrSubquery);
                    namingScopes.ExitScope();
                    break;
            }

            if (addDefaultColumns && result.Select.IsEmpty)
            {
                AddDefaultColumns(result);
            }

            return result;
        }

        ///<summary>
        ///  This method is called by <see cref = "Visit(DbFilterExpression)" /> and
        ///  <see cref = "Visit(DbQuantifierExpression)" />
        ///</summary>
        ///<param name="predicate"></param>
        ///<param name="input"></param>
        ///<param name = "negatePredicate">This is passed from <see cref = "Visit(DbQuantifierExpression)" />
        ///  in the All(...) case.</param>
        ///<returns></returns>
        SqlSelectStatement VisitFilterExpression
            (
                DbExpression predicate,
                DbExpressionBinding input,
                bool negatePredicate
            )
        {
            AliasOrSubquery fromAliasOrSubquery;
            SqlSelectStatement result = VisitInputCollectionExpression(input, out fromAliasOrSubquery);
            // Filter is compatible with OrderBy
            // but not with Project, another Filter or GroupBy
            if (!IsCompatibleWithCurrentSelectStatement(result, DbExpressionKind.Filter))
            {
                result = CreateNewSelectStatement(
                    result, input.VariableName, input.VariableType, out fromAliasOrSubquery);
            }
            currentSelectStatement.Set(result);
            AddAliasToFrom(result, input.VariableName, fromAliasOrSubquery);
            if (IsJunkExpression(predicate))
            {
                currentSelectStatement.Pop();
                return result;
            }
            if (negatePredicate)
            {
                result.Where.Append("NOT (");
            }
            MergeWhere(result, predicate);
            if (negatePredicate)
            {
                result.Where.Append(")");
            }
            currentSelectStatement.Pop();
            return result;
        }

        protected virtual void MergeWhere
            (
                SqlSelectStatement result
                , DbExpression predicate
            )
        {
            result.Where.Append(predicate.Accept(this));
        }

        /// <summary>
        ///   If the sql fragment for an input expression is not a SqlSelect statement
        ///   or other acceptable form (e.g. an extent as a SqlBuilder), we need
        ///   to wrap it in a form acceptable in a FROM clause.  These are
        ///   primarily the
        ///   <list type = "bullet">
        ///     <item>The set operation expressions - union all, intersect, except</item>
        ///     <item>TVFs, which are conceptually similar to tables</item>
        ///   </list>
        /// </summary>
        /// <param name = "result"></param>
        /// <param name = "sqlFragment"></param>
        /// <param name = "expressionKind"></param>
        static void WrapNonQueryExtent
            (
            SqlSelectStatement result,
            ISqlFragment sqlFragment,
            DbExpressionKind expressionKind)
        {
            switch (expressionKind)
            {
                case DbExpressionKind.Function :
                    // TVF
                    result.From.Append(sqlFragment);
                    break;

                default :
                    result.From.Append(" (");
                    result.From.Append(sqlFragment);
                    result.From.Append(")");
                    break;
            }
        }

        /// <summary>
        ///   Is this a builtin function (ie) does it have the builtinAttribute specified?
        /// </summary>
        /// <param name = "function"></param>
        /// <returns></returns>
        static bool IsBuiltinFunction(EdmFunction function)
        {
            return MetadataHelpers.TryGetValueForMetadataProperty<bool>(function, "BuiltInAttribute");
        }

        ///<summary>
        ///</summary>
        ///<param name = "function"></param>
        ///<param name = "result"></param>
        void WriteFunctionName
            (
            SqlBuilder result,
            EdmFunction function)
        {
            string storeFunctionName = MetadataHelpers.TryGetValueForMetadataProperty<string>
                (function, "StoreFunctionNameAttribute");

            if (string.IsNullOrEmpty(storeFunctionName))
            {
                storeFunctionName = function.Name;
            }

            // If the function is a builtin (ie) the BuiltIn attribute has been
            // specified, then, the function name should not be quoted; additionally,
            // no namespace should be used.
            if (IsBuiltinFunction(function))
            {
                if (function.NamespaceName == "Edm")
                {
                    switch (storeFunctionName.ToUpperInvariant())
                    {
                        default :
                            result.Append(storeFunctionName);
                            break;
                    }
                }
                else
                {
                    result.Append(storeFunctionName);
                }
            }
            else
            {
                // Should we actually support this?
                result.Append(QuoteIdentifier((string)function.MetadataProperties["Schema"].Value ?? "dbo"));
                result.Append(".");
                result.Append(QuoteIdentifier(storeFunctionName));
            }
        }

        static string ByteArrayToBinaryString(Byte[] binaryArray)
        {
            StringBuilder sb = new StringBuilder(binaryArray.Length*2);
            for (int i = 0; i < binaryArray.Length; i++)
            {
                sb.Append(hexDigits[( binaryArray[i] & 0xF0 ) >> 4]).Append(hexDigits[binaryArray[i] & 0x0F]);
            }
            return sb.ToString();
        }

        /// <summary>
        ///   Helper method for the Group By visitor
        ///   Returns true if at least one of the aggregates in the given list
        ///   has an argument that is not a <see cref = "DbPropertyExpression" /> 
        ///   over <see cref = "DbVariableReferenceExpression" />
        /// 
        /// Whenever there exists at least one aggregate with an argument 
        /// that is not simply a PropertyExpression over a VarRefExpression, 
        /// we need a nested query in which we alias the arguments to the aggregates.
        /// </summary>
        /// <param name = "aggregates">Aggregation functions</param>
        static bool IsArgumentRequiringInnerQuery(IList<DbAggregate> aggregates)
        { //TODO: It may be that they overdone this - check if it works if I always return false 
            //or return true less often.
            foreach (DbAggregate aggregate in aggregates)
            {
                Debug.Assert(aggregate.Arguments.Count == 1);
                if (!IsPropertyOverVarRef(aggregate.Arguments[0]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        ///   Determines whether the given expression is a <see cref = "DbPropertyExpression" /> 
        ///   over <see cref = "DbVariableReferenceExpression" />
        /// </summary>
        /// <param name = "expression"></param>
        /// <returns></returns>
        static bool IsPropertyOverVarRef(DbExpression expression)
        {
            DbPropertyExpression propertyExpression = expression as DbPropertyExpression;
            if (propertyExpression == null)
            {
                return false;
            }
            DbVariableReferenceExpression varRefExpression =
                propertyExpression.Instance as DbVariableReferenceExpression;
            if (varRefExpression == null)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        ///   Throws not supported exception if the server is pre-katmai
        /// </summary>
        /// <param name = "primitiveTypeKind"></param>
        void AssertKatmaiOrNewer(PrimitiveTypeKind primitiveTypeKind)
        {
            AssertKatmaiOrNewer(StoreVersion, primitiveTypeKind);
        }

        static void AssertKatmaiOrNewer
            (
            StoreVersion sqlVersion,
            PrimitiveTypeKind primitiveTypeKind)
        {
            if (sqlVersion == StoreVersion.Sql9)
            {
                throw new NotSupportedException
                    (
                    String.Format
                        (
                            "There is no store type that maps to the EDM type '{0}' on versions of SQL Server earlier than SQL Server 2008.",
                            primitiveTypeKind));
            }
        }

        /// <summary>
        ///   Throws not supported exception if the server is pre-katmai
        /// </summary>
        /// <param name = "e"></param>
        void AssertKatmaiOrNewer(DbFunctionExpression e)
        {
            if (IsPreKatmai)
            {
                throw new NotSupportedException
                    (
                    String.Format
                        (
                            "The EDM function '{0}' is not supported on versions of SQL Server earlier than SQL Server 2008.",
                            e.Function.Name));
            }
        }

        #endregion

        
        protected class DbSkipExpressionVisitor
        {
            SqlGenerator sqlGeenrator;

            protected internal DbSkipExpressionVisitor ( SqlGenerator sqlGeenrator )
            {
                this.sqlGeenrator = sqlGeenrator;
            }

            /// <summary>
            ///   For Sql9 it translates to:
            ///   SELECT Y.x1, Y.x2, ..., Y.xn
            ///   FROM (
            ///   SELECT X.x1, X.x2, ..., X.xn, row_number() OVER (ORDER BY sk1, sk2, ...) AS [row_number] 
            ///   FROM input as X 
            ///   ) as Y
            ///   WHERE Y.[row_number] > count 
            ///   ORDER BY sk1, sk2, ...
            /// </summary>
            /// <param name = "e"></param>
            /// <returns>A <see cref = "SqlBuilder" /></returns>
            public virtual ISqlFragment Visit ( DbSkipExpression e )
            {
                Debug.Assert
                    (
                        e.Count is DbConstantExpression || e.Count is DbParameterReferenceExpression,
                        "DbSkipExpression.Count is of invalid expression type" );

                //Visit the input
                AliasOrSubquery fromAliasOrSubquery;
                SqlSelectStatement input = sqlGeenrator.VisitInputCollectionExpression( e.Input, out fromAliasOrSubquery );

                // Skip is not compatible with anything that OrderBy is not compatible with, as well as with distinct
                if ( !sqlGeenrator.IsCompatibleWithCurrentSelectStatement( input, e.ExpressionKind ) )
                {
                    input = sqlGeenrator.CreateNewSelectStatement( input, e.Input.VariableName, e.Input.VariableType, out fromAliasOrSubquery );
                }

                sqlGeenrator.currentSelectStatement.Set( input );

                sqlGeenrator.AddAliasToFrom( input, e.Input.VariableName, fromAliasOrSubquery );

                AssertSelectClauseIsEmpty( input );
                List<AliasOrSubquery> inputColumns = sqlGeenrator.AddDefaultColumns( input );

                AliasOrSubquery rowNumberAliasOrSubquery = AddRowNumber( e, input );
                //The inner statement is complete, its scopes need not be valid any longer
                sqlGeenrator.currentSelectStatement.Pop();

                //Create the resulting statement 
                //See CreateNewSelectStatement, it is very similar

                SqlSelectStatement result = CreateSkipResultSelectStatement( input );
                //Create a symbol for the input
                AliasOrSubquery resultFromAliasOrSubquery = null;

                if ( input.FromExtents.Count == 1 )
                {
                    JoinAlias oldJoinAlias = input.FromExtents[0] as JoinAlias;
                    if ( oldJoinAlias != null )
                    {
                        // Note: input.FromExtents will not do, since it might
                        // just be an alias of joinSymbol, and we want an actual JoinSymbol.
                        JoinAlias newJoinAlias = new JoinAlias
                            ( e.Input.VariableName, e.Input.VariableType, oldJoinAlias.ExtentList );
                        // This indicates that the oldStatement is a blocking scope
                        // i.e. it hides/renames extent columns
                        newJoinAlias.IsNestedJoin = true;
                        newJoinAlias.ColumnList = inputColumns;
                        newJoinAlias.FlattenedExtentList = oldJoinAlias.FlattenedExtentList;

                        resultFromAliasOrSubquery = newJoinAlias;
                    }
                }

                if ( resultFromAliasOrSubquery == null )
                {
                    // This is just a simple extent/SqlSelectStatement,
                    // and we can get the column list from the type.
                    resultFromAliasOrSubquery = new AliasOrSubquery( e.Input.VariableName, e.Input.VariableType );
                }
                AddOrderBy( result, e, resultFromAliasOrSubquery, rowNumberAliasOrSubquery );

                return result;
            }

            void AddOrderBy
                (
                 SqlSelectStatement result,
                 DbSkipExpression e,
                 AliasOrSubquery resultFromAliasOrSubquery,
                 AliasOrSubquery rowNumberAliasOrSubquery
                )
            {
                //Add the ORDER BY part
                sqlGeenrator.currentSelectStatement.Set( result );

                sqlGeenrator.AddAliasToFrom( result, e.Input.VariableName, resultFromAliasOrSubquery );
                AddSkipPredicate( e, result, resultFromAliasOrSubquery, rowNumberAliasOrSubquery );
                AddSortKeys( e, result );

                sqlGeenrator.currentSelectStatement.Pop(); //Stack.Pop()
            }


            protected virtual void AssertSelectClauseIsEmpty ( SqlSelectStatement input )
            {
                Debug.Assert( input.Select.IsEmpty );
            }

            protected virtual SqlSelectStatement CreateSkipResultSelectStatement ( SqlSelectStatement input )
            {
                SqlSelectStatement result = sqlGeenrator.CreateSelectStatement();
                result.From.Append( "( " );
                result.From.Append( input );
                result.From.AppendLine();
                result.From.Append( ") " );
                return result;
            }

            protected virtual void AddSortKeys ( DbSkipExpression e,
                                 SqlSelectStatement result )
            {
                sqlGeenrator.AddSortKeys( result.OrderBy, e.SortOrder );
            }

            protected virtual void AddSkipPredicate ( DbSkipExpression e,
                                  SqlSelectStatement result,
                                  AliasOrSubquery resultFromAliasOrSubquery,
                                  AliasOrSubquery rowNumberAliasOrSubquery )
            {
                result.Where.Append( resultFromAliasOrSubquery );
                result.Where.Append( "." );
                result.Where.Append( rowNumberAliasOrSubquery );
                result.Where.Append( " > " );
                result.Where.Append( sqlGeenrator.HandleCountExpression( e.Count ) );
            }

            protected virtual AliasOrSubquery AddRowNumber ( DbSkipExpression e,
                                         SqlSelectStatement input )
            {
                input.Select.Append( ", row_number() OVER (ORDER BY " );
                sqlGeenrator.AddSortKeys( input.Select, e.SortOrder );
                input.Select.Append( ") AS " );

                AliasOrSubquery rowNumberAliasOrSubquery = new AliasOrSubquery( "row_number", null );

                input.Select.Append( rowNumberAliasOrSubquery );
                return rowNumberAliasOrSubquery;
            }

        }


    }
}