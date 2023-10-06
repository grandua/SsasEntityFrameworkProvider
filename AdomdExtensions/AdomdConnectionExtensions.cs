using System;
using System.Collections.Generic;
using System.Diagnostics;
using AgileDesign.Utilities;
using Microsoft.AnalysisServices.AdomdClient;

namespace AgileDesign.AdomdExtensions
{

#if ! DEBUG
    [DebuggerStepThrough]
#endif
    public static class AdomdConnectionExtensions
    {
        static AdomdCommandExecutor commandExecutor;

        /// <summary>
        ///   MdxToEntityNameMapper is used by default and it uses an exact property to column name match only. <br />
        ///   Complex types are not supported <br />
        ///   Set this property to instance of EntityFrameworkMdxToEntityMapper for more complex scenarios <br />
        ///   Or create your own derived class inherited from MdxToEntityNameMapper
        /// </summary>
        public static MdxToEntityNameMapper Mapper
        {
            get { return CommandExecutor.Mapper; }
            set
            {
                Contract.Requires(value != null);
                CommandExecutor.Mapper = value;
            }
        }

        public static AdomdCommandExecutor CommandExecutor
        {
            get { return Init.InitIfNull(ref commandExecutor); }
        }

        public static IEnumerable<TEntity> ExecuteMdxCollection<TEntity>
            (
            this AdomdConnection readConnection,
            string mdxQuery
            ) where TEntity 
            : new()
        {
            Contract.Requires<ArgumentNullException>(readConnection != null);
            Contract.Requires<ArgumentNullException>(mdxQuery != null);

            return CommandExecutor.ExecuteMdxCollection<TEntity>(readConnection, mdxQuery);
        }

        public static TEntity ExecuteMdxSingleEntity<TEntity>
            (
            this AdomdConnection readConnection,
            string mdxQuery
            ) where TEntity : new()
        {
            Contract.Requires<ArgumentNullException>(readConnection != null);
            Contract.Requires<ArgumentNullException>(mdxQuery != null);

            return CommandExecutor.ExecuteMdxSingleEntity<TEntity>(readConnection, mdxQuery);
        }

        public static T ExecuteMdxScalar<T>
            (
            this AdomdConnection readConnection,
            string mdxQuery
            )
        {
            Contract.Requires<ArgumentNullException>(readConnection != null);
            Contract.Requires<ArgumentNullException>(mdxQuery != null);

            return CommandExecutor.ExecuteMdxScalar<T>(readConnection, mdxQuery);
        }
    }
}