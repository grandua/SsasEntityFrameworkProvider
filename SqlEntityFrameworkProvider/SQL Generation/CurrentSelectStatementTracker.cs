using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SqlEntityFrameworkProvider
{
    internal interface ICurrentSelectStatementTracker<out T> 
        where T : SqlSelectStatement
    {
        /// <summary>
        /// No items / SelectStatement-s in a stack
        /// </summary>
        bool IsEmpty { get; }
        Action OnPop { get; set; }
        void Set(SqlSelectStatement selectStatement);
        void Pop();
        T Get();
    }

    class CurrentSelectStatementTracker 
        : ICurrentSelectStatementTracker<SqlSelectStatement>
    {
        Stack<SqlSelectStatement> currentSelectStatementStack 
            = new Stack<SqlSelectStatement>();

        NamingScopes namingScopes;

        public CurrentSelectStatementTracker(NamingScopes namingScopes)
        {
            Debug.Assert(namingScopes != null);
            this.namingScopes = namingScopes;
        }

        /// <summary>
        /// No items / SelectStatement-s in a stack
        /// </summary>
        public bool IsEmpty
        {
            get { return currentSelectStatementStack.Count == 0; }
        }

        public Action OnPop { get; set; }

        public void Set(SqlSelectStatement selectStatement)
        {
            currentSelectStatementStack.Push(selectStatement);
            namingScopes.EnterScope(); //TODO: merge namingScopes with SelectStatement
        }

        public void Pop()
        {
            if(OnPop != null)
            {
                OnPop();
            }
            namingScopes.ExitScope();
            currentSelectStatementStack.Pop();
        }

        public SqlSelectStatement Get()
        {
            return currentSelectStatementStack.Peek();
        }
    }
}