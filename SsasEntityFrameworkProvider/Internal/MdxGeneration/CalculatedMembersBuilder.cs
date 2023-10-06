using System.Data.Entity.Core.Common.CommandTrees;
using AgileDesign.SsasEntityFrameworkProvider.Utilities;
using SqlEntityFrameworkProvider;

namespace AgileDesign.SsasEntityFrameworkProvider.Internal.MdxGeneration
{
    internal class CalculatedMembersBuilder
        : SqlBuilder
    {

        public void AddCalculatedMember(DbFunctionExpression e)
        {
            AddCalculatedMember(
                GetCalculatedMemberName(e)
                , GetCalculatedMemberExpression(e));
        }

        public string GetCalculatedMemberExpression(DbFunctionExpression e)
        {
            return GetArgumentValueOrName(e.Arguments[1]);
        }

        public string GetCalculatedMemberName(DbFunctionExpression e)
        {
            return GetArgumentValueOrName(e.Arguments[0]);
        }

        string GetArgumentValueOrName(DbExpression argumentExpression)
        {
            return (argumentExpression is DbConstantExpression)
                ? (((DbConstantExpression)argumentExpression).Value ?? "null").ToString()
                : argumentExpression.GetNameParameterNamedPlaceholderForHeader();
        }

        public void AddCalculatedMember
            (
                string calculatedMemberName
                , string calculatedMemberExpression
            )
        {
            Append("MEMBER ");
            AppendMemberNameOrExpression(calculatedMemberName);
            Append("\tAS ");
            AppendMemberNameOrExpression(calculatedMemberExpression);
        }

        void AppendMemberNameOrExpression(string memberNameOrExpression)
        {
            if (ShouldEnQuote(memberNameOrExpression))
            {
                Append("'");
            }
            Append(memberNameOrExpression);
            if (ShouldEnQuote(memberNameOrExpression))
            {
                Append("'");
            }
            AppendLine();
        }

        bool ShouldEnQuote(string memberNameOrExpression)
        {
            return memberNameOrExpression[0] == '<';
        }

    }
}