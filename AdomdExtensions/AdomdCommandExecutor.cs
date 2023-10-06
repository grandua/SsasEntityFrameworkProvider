using System;
using System.Collections.Generic;
using System.Data;
using AgileDesign.Utilities;
using Microsoft.AnalysisServices.AdomdClient;

namespace AgileDesign.AdomdExtensions
{
    public class AdomdCommandExecutor
    {
        MdxToEntityNameMapper mapper;

        /// <summary>
        ///   MdxToEntityNameMapper is used by default and it uses an exact property to column name match only. <br />
        ///   Complex types are not supported <br />
        ///   Set this property to instance of EntityFrameworkMdxToEntityMapper for more complex scenarios <br />
        ///   Or create your own derived class inherited from MdxToEntityNameMapper
        /// </summary>
        public MdxToEntityNameMapper Mapper
        {
            get { return Init.InitIfNull(ref mapper); }
            set
            {
                Contract.Requires<InvalidOperationException>(value != null);
                mapper = value;
            }
        }

        public IEnumerable<TEntity> ExecuteMdxCollection<TEntity>
            (
            AdomdConnection readConnection,
            string mdxQuery
            ) where TEntity : new()
        {
            Contract.Requires<ArgumentNullException>(readConnection != null);
            Contract.Requires<ArgumentNullException>(mdxQuery != null);

            var result = new List<TEntity>();
            var command = readConnection.CreateCommand();
            command.CommandText = mdxQuery;
            readConnection.Open();
            using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
            {
                if (reader.FieldCount == 0)
                {
                    //member not found - single null value returned case
                    return new List<TEntity>();
                }
                while (reader.Read())
                {
                    var entityMapper = Mapper;
                    result.Add(entityMapper.MapToEntity<TEntity>(reader));
                }
                return result;
            }
        }

        public TEntity ExecuteMdxSingleEntity<TEntity>
            (
            AdomdConnection readConnection,
            string mdxQuery
            ) where TEntity : new()
        {
            Contract.Requires<ArgumentNullException>(readConnection != null);
            Contract.Requires<ArgumentNullException>(mdxQuery != null);

            var command = readConnection.CreateCommand();
            command.CommandText = mdxQuery;
            readConnection.Open();
            using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
            {
                if (reader.FieldCount == 0)
                {
                    //member not found - single null value returned case
                    return default( TEntity );
                }
                if (reader.Read())
                {
                    return Mapper.MapToEntity<TEntity>(reader);
                }
                throw new IndexOutOfRangeException("Single entity expected in a result, but found none");
            }
        }

        public T ExecuteMdxScalar<T>
            (
            AdomdConnection readConnection,
            string mdxQuery)
        {
            Contract.Requires<ArgumentNullException>(readConnection != null);
            Contract.Requires<ArgumentNullException>(mdxQuery != null);

            using (var conn = new AdomdConnection(readConnection.ConnectionString))
            {
                var command = conn.CreateCommand();
                command.CommandText = mdxQuery;
                conn.Open();
                var cellSet = command.ExecuteCellSet();
                var cells = cellSet.Cells;
                if (cells.Count == 0)
                {
                    return GetNull<T>();
                }
                if (cells.Count == 1)
                {
                    return GetScalarValue<T>(cells);
                }
                throw CreateComplexTypesNotSupportedException();
            }
        }

        InvalidOperationException CreateComplexTypesNotSupportedException()
        {
            return new InvalidOperationException
                (
                string.Format
                    (
                        "Complex types are not supported by this overload, use '{0}()' instead",
                        NameOf.Method
                            (
                                () => ExecuteMdxSingleEntity<object>
                                          (
                                              null,
                                              null
                                            )
                            )
                    )
                );
        }

        static T GetNull<T>()
        {
            if (! IsNullable<T>())
            {
                throw new InvalidCastException("Query returned no rows, cannot return NULL for non-nullable type");
            }
            return default( T );
        }

        static T GetScalarValue<T>(CellCollection cells)
        {
            if (cells == null
                || cells.Count == 0)
            {
                return default( T );
            }
            object resultObject = cells[0].Value;
            if (resultObject == null
                && IsNullable<T>())
            {
                return default( T );
            }
            return resultObject.ConvertTo<T>();
        }

        static bool IsNullable<T>()
        {
            return typeof(T).IsNullable();
        }
    }
}