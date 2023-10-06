using System.Collections.Generic;
using System.Data.Entity.Core.Common.CommandTrees;
using System.Linq;
using AgileDesign.SsasEntityFrameworkProvider.Utilities;
using AgileDesign.Utilities;
using SqlEntityFrameworkProvider;

namespace AgileDesign.SsasEntityFrameworkProvider.Internal.MdxGeneration
{
    class ColumnsAxisBuilder 
        : SqlBuilder
    {
        const string CnPrefix = "'Cn";
        internal const string MeasuresPrefix = "[Measures]";
        readonly MdxSelectStatement selectStatement;

        public ColumnsAxisBuilder(MdxSelectStatement selectStatement)
        {
            this.selectStatement = selectStatement;
        }

        public ColumnsAxisBuilder(string initialFragment, MdxSelectStatement selectStatement)
            : base(initialFragment)
        {
            this.selectStatement = selectStatement;
        }

        bool HasC1()
        {
            return ToString().Contains(MdxSelectStatement.C1);
        }

        public bool HasCustomColumns
        {
            get
            {
                return IsEmpty == false
                       && ToString() != MdxSelectStatement.C1;
            }
        }

        public bool IsDummyMemberRequired()
        {
            return (HasC1() 
                    || IsEmpty);
        }

        public void AddMeasure
            (
                DbExpression columnExpression
                , MdxGenerator mdxGenerator
            )
        {
            AddMeasure(columnExpression.GetMeasureName(mdxGenerator));
        }

        int cnPosition;
        /// <param name="e">
        /// DbFunctionExpression can be either calculated member variable/parameter 
        /// or calculated member calculated member literal expression
        /// </param>
        public void AddCnMeasure(DbFunctionExpression e)
        {
            cnPosition++;
            DbExpression argumentExpression = e.Arguments[0];
            if (argumentExpression is DbConstantExpression)
            {
                AddMeasure(GetCnTaggedExpression(argumentExpression));
                return;
            }
            AddMeasure(GetCnTaggedNameParameterPlaceholder(argumentExpression));
        }

        public void AddCnMeasure(string measureName)
        {
            cnPosition++;
            AddMeasure(GetCnTaggedExpression(measureName));
        }

        public void AddMeasure(SqlBuilder measureName)
        {
            if (measureName.ToString() == MeasuresPrefix)
            { //DbFunctionExpression are being added to COLUMNS axis by VisitDbFunctionExpression, 
                //so do not repeat. 
                //Note: I tried to remove this hack 
                //and return calculated member name from VisitDbFunctionExpression, 
                //but it was hard to do because MdxName added spaces twice. The trial is shelved.
                return;
            }
            if ((!IsEmpty))
            {
                AppendLine(", ");
            }
            Append(measureName);
            selectStatement.OnColumnAdded();
        }

        string GetCnTaggedNameParameterPlaceholder(DbExpression argumentExpression)
        {
            return string.Format("'C{0}{1}'"
                                 , cnPosition
                                 , argumentExpression.GetNameParameterNamedPlaceholderForMeasures());
        }

        SqlBuilder GetCnTaggedExpression(DbExpression argumentExpression)
        {
            return GetCnTaggedExpression
                (
                    ( ( (DbConstantExpression)argumentExpression ).Value ?? "null" ).ToString()
                );
        }

        SqlBuilder GetCnTaggedExpression(string measureName)
        {
            var result = new SqlBuilder(measureName);
            result.IsExpression = true;
            result.Alias = "C" + cnPosition;
            return result;
        }

        /// <summary>
        /// The method returns a new string where Cn are replaced with NameParameter
        /// </summary>
        /// <returns>
        /// A new string where Cn are replaced with NameParameter
        /// </returns>
        public string ReplaceCnWithNameParameters(ColumnsAxisBuilder columnAxisWithNameParameters)
        {
            if (columnAxisWithNameParameters.IsEmpty)
            { //nothing to replace - there are no name parameters
                return ToString();
            }
            string result = ToString();
            foreach (var prefixedNameParameter in columnAxisWithNameParameters.GetNameParameters())
            {
                result = result.Replace(
                    GetCn(prefixedNameParameter)
                    , GetNameParameterPart(prefixedNameParameter));
            }
            foreach (var literalExpression in columnAxisWithNameParameters.GetLiteralExpressions())
            {
                result = result.Replace(
                    GetMeasureNameFromCnAlias(literalExpression.Alias)
                    , literalExpression.ToString());
            }
            return result;
        }

        string GetNameParameterPart(string prefixedNameParameter)
        {
            return "'" + prefixedNameParameter.Substring(CnPrefix.Length);
        }

        string GetMeasureNameFromCnAlias(string columnAlias)
        {
            return string.Format("[Measures].[{0}]", columnAlias);
        }
		
		string GetCn(string prefixedNameParameter)
        { //TODO: using cnPrefix.Length makes it buggy by restricting # of allows DbFunction-s to 9
            return string.Format("[Measures].[{0}]"
                                 , prefixedNameParameter.Substring(1).Left(CnPrefix.Length - 1));
        }

        IEnumerable<string> GetNameParameters()
        {
            return SqlFragments.Where(f => f.ToString().Contains("<Name_p__linq__"))
                .Select(f => f.ToString());
        }

        IEnumerable<SqlBuilder> GetLiteralExpressions()
        {
            return SqlFragments.OfType<SqlBuilder>()
                .Where(f => f.IsExpression);
        }
    }
}