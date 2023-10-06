using System;
using System.Diagnostics.Contracts;

namespace AgileDesign.Utilities
{
    public static class ConvertExtensions
    {
        public static T ConvertTo<T>(this object value)
        {
            return (T)value.ConvertTo(typeof(T));
        }

        public static object ConvertTo
            (
            this object value,
            Type type
            )
        {
            Contract.Requires<ArgumentNullException>(type != null);

            if (! type.IsGenericType)
            {
                return Convert.ChangeType(value, type);
            }
            var genericArgs = type.GetGenericArguments();
            if (genericArgs.Length != 1)
            {
                throw new NotSupportedException
                    ("Generics with multiple arguments are not supported");
            }
            return Convert.ChangeType(value, genericArgs[0]);
        }
    }
}