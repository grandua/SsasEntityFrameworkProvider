using System;
using System.Diagnostics;

namespace AgileDesign.Utilities
{
    /// <summary>
    /// This class wraps code contracts logic to remove dependency on code contracts.
    /// This solves a known debugging error "The type or namespace name is not valid in this scope" 
    /// caused by code contracts  (see 
    /// http://social.msdn.microsoft.com/Forums/en-US/vsdebug/thread/9e8cec67-402f-4ac6-87be-204f2441f9f0
    /// for details)
    /// </summary>
    public static class Contract
    {
        public static void Requires
            (
                bool condition,
                string errorMessage = "Provided argument is not valid"
            )
        {
            if( ! condition)
            {
                throw new ArgumentException(errorMessage);
            }
        }

        public static void Requires<T>
            (
                bool condition,
                string errorMessage = null
            )
            where T : Exception, new()
        {
            if (condition)
            {
                return;
            }
            if(errorMessage == null)
            {
                throw new T();
            }
            throw new ArgumentException(errorMessage);
        }

        public static void Invariant(bool condition)
        {
            if ( ! condition)
            {
                throw new InvalidOperationException("Invariant is violated!");
            }
        }

        public static void Assert(bool condition)
        {
            Debug.Assert(condition);
        }

        public static void Ensures(bool condition)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Post condition failed!");
            }
        }

    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ContractInvariantMethodAttribute : Attribute
    {
    }
}
