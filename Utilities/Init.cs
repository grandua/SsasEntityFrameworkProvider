using System;
using System.Diagnostics;

namespace AgileDesign.Utilities
{
    public static class Init
    {
        /// <typeparam name="TConcrete">
        /// Concrete type to instantiate
        /// </typeparam>
        /// <typeparam name="TBase">
        /// Type of field (may be an interface or base class for TConcrete)
        /// </typeparam>
        /// <param name="field">
        /// Field to instantiate
        /// </param>
        /// <param name="locker">
        /// Any object to lock
        /// </param>
        /// <returns>
        /// New instance of TConcrete, this instance is assigned to the field
        /// </returns>
        public static TBase InitIfNullLocking<TConcrete, TBase>
            (
            ref TBase field,
            object locker
            ) 
            where TBase : class
            where TConcrete : TBase, new()
        {
            if (field != null)
            {
                return field;
            }
            lock (locker)
            {
                if (field == null)
                {
                    ValidateInput(field, locker);
                    field = new TConcrete();
                }
                return field;
            }
        }

        public static T InitIfNullLocking<T>
            (
            ref T field,
            object locker
            ) 
            where T : class, new()
        {
            return InitIfNullLocking<T, T>(ref field, locker);
        }

        public static T InitIfNull<T>(ref T field)
            where T : class, new()
        {
            return field ?? (field = new T());
        }

        [Conditional("DEBUG")]
        static void ValidateInput<T>
            (
            T field,
            object locker)
        {
            if (locker == null)
            {
                throw new ArgumentNullException(NameOf.Member(() => locker));
            }
            if (locker.GetType()
                != typeof(object))
            {
                throw new ArgumentException
                    ("Argument must be an object", NameOf.Member(() => locker));
            }
            if (typeof(T)
                == typeof(string))
            {
                throw new ArgumentException
                    ("Argument must be a reference type", NameOf.Member(() => field));
            }
        }
    }
}