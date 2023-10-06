// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Data.Entity.Infrastructure;
using System.Threading;
using System.Threading.Tasks;

namespace AgileDesign.SsasEntityFrameworkProvider.Internal
{
    // <summary>
    // An <see cref="IDbExecutionStrategy"/> that doesn't affect the execution but will throw a more helpful exception if a transient failure is detected.
    // </summary>
    internal sealed class TmpDefaultSqlExecutionStrategy : IDbExecutionStrategy
    {
        public bool RetriesOnFailure
        {
            get { return false; }
        }

        public void Execute(Action operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException("operation");
            }

            Execute(
                () =>
                {
                    operation();
                    return (object) null;
                });
        }

        public TResult Execute<TResult>(Func<TResult> operation)
        {//executed
            return operation();
        }

#if !NET40

        public Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ExecuteAsyncImplementation(
                async () =>
                {
                    await operation().ConfigureAwait(continueOnCapturedContext: false);
                    return true;
                });
        }

        public Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ExecuteAsyncImplementation(operation);
        }

        private async Task<TResult> ExecuteAsyncImplementation<TResult>(Func<Task<TResult>> func)
        {
            return await func().ConfigureAwait(continueOnCapturedContext: false);
        }
#endif
    }
}
