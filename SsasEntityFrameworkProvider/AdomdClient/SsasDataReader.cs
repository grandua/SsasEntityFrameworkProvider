using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Microsoft.AnalysisServices.AdomdClient;
using AgileDesign.AdomdExtensions;
using AgileDesign.Utilities;

namespace AgileDesign.SsasEntityFrameworkProvider.AdomdClient
{
    class SsasDataReader : DbDataReader
    { //TODO: Do not generate C1 in MDX but handle it in SsasDataReader returning "1" if mapped to C1 index
        public SsasDataReader(AdomdDataReader dataReader)
        {
            StoreDataReader = dataReader;
        }

        AdomdDataReader StoreDataReader { get; set; }

        /// <summary>
        ///   Gets a value indicating the depth of nesting for the current row.
        /// </summary>
        /// <returns>
        ///   The level of nesting.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override int Depth
        {
            get { return StoreDataReader.Depth; }
        }

        /// <summary>
        ///   Gets the number of columns in the current row.
        /// </summary>
        /// <returns>
        ///   When not positioned in a valid recordset, 0; 
        /// otherwise, the number of columns in the current record. The default is -1.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override int FieldCount
        {
            get { return StoreDataReader.FieldCount; }
        }

        /// <summary>
        ///   Gets a value that indicates whether this 
        /// <see cref = "T:System.Data.Common.DbDataReader" /> 
        /// contains one or more rows.
        /// </summary>
        /// <returns>
        ///   true if the <see cref = "T:System.Data.Common.DbDataReader" /> 
        /// contains one or more rows; otherwise false.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public override bool HasRows
        {
            //TODO: it is an assumption, cover with unit test
            get { return StoreDataReader.RecordsAffected > 0; }
        }

        /// <summary>
        ///   Gets a value indicating whether the data reader is closed.
        /// </summary>
        /// <returns>
        ///   true if the data reader is closed; otherwise, false.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override bool IsClosed
        {
            get { return StoreDataReader.IsClosed; }
        }

        /// <summary>
        ///   Gets the number of rows changed, inserted, or deleted by execution of the SQL statement.
        /// </summary>
        /// <returns>
        ///   The number of rows changed, inserted, or deleted; 
        /// 0 if no rows were affected or the statement failed; 
        /// and -1 for SELECT statements.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override int RecordsAffected
        {
            get { return StoreDataReader.RecordsAffected; }
        }

        MdxToEntityNameMapper mdxMapper;
        MdxToEntityNameMapper MdxMapper
        {
            get 
            { 
                return mdxMapper 
                    ?? ( mdxMapper = new MdxToEntityNameMapper(this) ); 
            }
        }

        IDictionary<int, int> mdxColumnsOrder;
        public IDictionary<int, int> MdxColumnsOrder
        {
            get
            {
                return mdxColumnsOrder 
                    ?? ( mdxColumnsOrder = CreateDefaultMdxColumnsOrder() );
            }
            set { mdxColumnsOrder = value; }
        }

        IDictionary<int, int> CreateDefaultMdxColumnsOrder()
        {
            var result = mdxColumnsOrder = new Dictionary<int, int>();
            for (int i = 0; i < FieldCount; i++)
            {
                result[i] = i;
            }
            return result;
        }

        /// <summary>
        ///   Gets the column located at the specified index.
        /// </summary>
        /// <returns>
        ///   The column located at the specified index as an <see cref = "T:System.Object" />.
        /// </returns>
        /// <param name = "i">The zero-based index of the column to get. </param>
        /// <exception cref = "T:System.IndexOutOfRangeException">
        /// The index passed was outside the range of 0 through 
        /// <see cref = "P:System.Data.IDataRecord.FieldCount" />. 
        /// </exception>
        /// <filterpriority>2</filterpriority>
        public override object this[int index]
        {
            get { return StoreDataReader[index]; }
        }

        /// <summary>
        ///   Gets the column with the specified name.
        /// </summary>
        /// <returns>
        ///   The column with the specified name as an <see cref = "T:System.Object" />.
        /// </returns>
        /// <param name = "name">The name of the column to find. </param>
        /// <exception cref = "T:System.IndexOutOfRangeException">
        /// No column with the specified name was found. 
        /// </exception>
        /// <filterpriority>2</filterpriority>
        public override object this[string columnName]
        {
            get { return StoreDataReader[columnName]; }
        }

        /// <summary>
        ///   Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        ///   An <see cref = "T:System.Collections.IEnumerator" /> 
        /// object that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override IEnumerator GetEnumerator()
        {
            return ( (IEnumerable)StoreDataReader ).GetEnumerator();
        }

        /// <summary>
        ///   Closes the <see cref = "T:System.Data.IDataReader" /> Object.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public override void Close()
        {
            StoreDataReader.Close();
        }

        /// <summary>
        ///   Returns a <see cref = "T:System.Data.DataTable" /> 
        /// that describes the column metadata of the <see cref = "T:System.Data.IDataReader" />.
        /// </summary>
        /// <returns>
        ///   A <see cref = "T:System.Data.DataTable" /> 
        /// that describes the column metadata.
        /// </returns>
        /// <exception cref = "T:System.InvalidOperationException">
        /// The <see cref = "T:System.Data.IDataReader" /> is closed. 
        /// </exception>
        /// <filterpriority>2</filterpriority>
        public override DataTable GetSchemaTable()
        {
            return StoreDataReader.GetSchemaTable();
        }

        /// <summary>
        ///   Advances the data reader to the next result, 
        /// when reading the results of batch SQL statements.
        /// </summary>
        /// <returns>
        ///   true if there are more rows; otherwise, false.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override bool NextResult()
        {
            return StoreDataReader.NextResult();
        }

        string tickCount = Environment.TickCount.ToString();
        int recordsReturned;
        /// <summary>
        ///   Advances the <see cref = "T:System.Data.IDataReader" /> to the next record.
        /// </summary>
        /// <returns>
        ///   true if there are more rows; otherwise, false.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override bool Read()
        {
            #region LicensingProtection
            if (PublicKeyToken != string.Format("{0}{1}{2}", "0c609", "c2d7c", "233e82")
                && StoreDataReader.FieldCount > int.Parse(tickCount.Left(1)))
            {
                if(recordsReturned >= int.Parse(tickCount.Left(2)))
                {
                    return false; //Cheat cheaters - say like there is no more records
                }
                recordsReturned++; //count only if in data corruption mode
            }
            #endregion

            return StoreDataReader.Read();
        }

        string publicKeyToken;
        string PublicKeyToken
        {
            get
            {
                return publicKeyToken 
                    ?? ( publicKeyToken = Utilities.DbExpressionExtension
                        .GetPublicKeyToken().ConvertToString() );
            }
        }

        /// <summary>
        ///   Gets the value of the specified column as a Boolean.
        /// </summary>
        /// <returns>
        ///   The value of the column.
        /// </returns>
        /// <param name = "i">The zero-based column ordinal. </param>
        /// <exception cref = "T:System.IndexOutOfRangeException">
        /// The index passed was outside the range of 0 through 
        /// <see cref = "P:System.Data.IDataRecord.FieldCount" />. 
        /// </exception>
        /// <filterpriority>2</filterpriority>
        public override bool GetBoolean(int ordinal)
        {
            return StoreDataReader.GetBoolean(MdxColumnsOrder[ordinal]);
        }

        /// <summary>
        ///   Gets the 8-bit unsigned integer value of the specified column.
        /// </summary>
        /// <returns>
        ///   The 8-bit unsigned integer value of the specified column.
        /// </returns>
        /// <param name = "i">The zero-based column ordinal. </param>
        /// <exception cref = "T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref = "P:System.Data.IDataRecord.FieldCount" />. </exception>
        /// <filterpriority>2</filterpriority>
        public override byte GetByte(int ordinal)
        {
            return StoreDataReader.GetByte(MdxColumnsOrder[ordinal]);
        }

        /// <summary>
        ///   Reads a stream of bytes from the specified column offset into the buffer as an array, 
        /// starting at the given buffer offset.
        /// </summary>
        /// <returns>
        ///   The actual number of bytes read.
        /// </returns>
        /// <param name = "i">The zero-based column ordinal. </param>
        /// <param name = "fieldOffset">
        /// The index within the field from which to start the read operation. 
        /// </param>
        /// <param name = "buffer">The buffer into which to read the stream of bytes. </param>
        /// <param name = "bufferoffset">The index for <paramref name = "buffer" /> 
        /// to start the read operation. 
        /// </param>
        /// <param name = "length">The number of bytes to read. </param>
        /// <exception cref = "T:System.IndexOutOfRangeException">
        /// The index passed was outside the range of 0 through 
        /// <see cref = "P:System.Data.IDataRecord.FieldCount" />. 
        /// </exception>
        /// <filterpriority>2</filterpriority>
        public override long GetBytes
            (
            int ordinal,
            long dataIndex,
            byte[] buffer,
            int bufferIndex,
            int length)
        {
            return StoreDataReader.GetBytes(MdxColumnsOrder[ordinal], 
                dataIndex, buffer, bufferIndex, length);
        }

        /// <summary>
        ///   Gets the character value of the specified column.
        /// </summary>
        /// <returns>
        ///   The character value of the specified column.
        /// </returns>
        /// <param name = "i">The zero-based column ordinal. </param>
        /// <exception cref = "T:System.IndexOutOfRangeException">
        /// The index passed was outside the range of 0 through 
        /// <see cref = "P:System.Data.IDataRecord.FieldCount" />. 
        /// </exception>
        /// <filterpriority>2</filterpriority>
        public override char GetChar(int ordinal)
        {
            return StoreDataReader.GetChar(MdxColumnsOrder[ordinal]);
        }

        /// <summary>
        ///   Reads a stream of characters from the specified column offset into the buffer 
        ///   as an array, starting at the given buffer offset.
        /// </summary>
        /// <returns>
        ///   The actual number of characters read.
        /// </returns>
        /// <param name = "i">The zero-based column ordinal. </param>
        /// <param name = "fieldoffset">
        /// The index within the row from which to start the read operation. </param>
        /// <param name = "buffer">
        /// The buffer into which to read the stream of bytes. 
        /// </param>
        /// <param name = "bufferoffset">
        /// The index for <paramref name = "buffer" /> to start the read operation. 
        /// </param>
        /// <param name = "length">The number of bytes to read. </param>
        /// <exception cref = "T:System.IndexOutOfRangeException">
        /// The index passed was outside the range of 0 through 
        /// <see cref = "P:System.Data.IDataRecord.FieldCount" />. 
        /// </exception>
        /// <filterpriority>2</filterpriority>
        public override long GetChars
            (
                int ordinal,
                long dataIndex,
                char[] buffer,
                int bufferIndex,
                int length
            )
        {
            return StoreDataReader.GetChars(MdxColumnsOrder[ordinal], 
                dataIndex, buffer, bufferIndex, length);
        }

        /// <summary>
        ///   Gets the data type information for the specified field.
        /// </summary>
        /// <returns>
        ///   The data type information for the specified field.
        /// </returns>
        /// <param name = "i">The index of the field to find. </param>
        /// <exception cref = "T:System.IndexOutOfRangeException">
        /// The index passed was outside the range of 0 through 
        /// <see cref = "P:System.Data.IDataRecord.FieldCount" />. 
        /// </exception>
        /// <filterpriority>2</filterpriority>
        public override string GetDataTypeName(int index)
        {
            return StoreDataReader.GetDataTypeName(MdxColumnsOrder[index]); 
            //TODO: is 'index' the same as ordinal?
        }

        /// <summary>
        ///   Gets the date and time data value of the specified field.
        /// </summary>
        /// <returns>
        ///   The date and time data value of the specified field.
        /// </returns>
        /// <param name = "i">The index of the field to find. </param>
        /// <exception cref = "T:System.IndexOutOfRangeException">
        /// The index passed was outside the range of 0 through 
        /// <see cref = "P:System.Data.IDataRecord.FieldCount" />. 
        /// </exception>
        /// <filterpriority>2</filterpriority>
        public override DateTime GetDateTime(int ordinal)
        {
            return StoreDataReader.GetDateTime(MdxColumnsOrder[ordinal]);
        }

        /// <summary>
        ///   Gets the fixed-position numeric value of the specified field.
        /// </summary>
        /// <returns>
        ///   The fixed-position numeric value of the specified field.
        /// </returns>
        /// <param name = "i">The index of the field to find. </param>
        /// <exception cref = "T:System.IndexOutOfRangeException">
        /// The index passed was outside the range of 0 through 
        /// <see cref = "P:System.Data.IDataRecord.FieldCount" />. 
        /// </exception>
        /// <filterpriority>2</filterpriority>
        public override decimal GetDecimal(int ordinal)
        {
            return StoreDataReader.GetDecimal(MdxColumnsOrder[ordinal]);
        }

        /// <summary>
        ///   Gets the double-precision floating point number of the specified field.
        /// </summary>
        /// <returns>
        ///   The double-precision floating point number of the specified field.
        /// </returns>
        /// <param name = "i">The index of the field to find. </param>
        /// <exception cref = "T:System.IndexOutOfRangeException">
        /// The index passed was outside the range of 0 through 
        /// <see cref = "P:System.Data.IDataRecord.FieldCount" />. 
        /// </exception>
        /// <filterpriority>2</filterpriority>
        public override double GetDouble(int ordinal)
        {
            return StoreDataReader.GetDouble(MdxColumnsOrder[ordinal]);
        }

        /// <summary>
        ///   Gets the <see cref = "T:System.Type" /> information corresponding to the type of 
        /// <see cref = "T:System.Object" /> 
        /// that would be returned from 
        /// <see cref = "M:System.Data.IDataRecord.GetValue(System.Int32)" />.
        /// </summary>
        /// <returns>
        ///   The <see cref = "T:System.Type" /> information corresponding to the type of 
        /// <see cref = "T:System.Object" /> 
        /// that would be returned from 
        /// <see cref = "M:System.Data.IDataRecord.GetValue(System.Int32)" />.
        /// </returns>
        /// <param name = "i">The index of the field to find. </param>
        /// <exception cref = "T:System.IndexOutOfRangeException">
        /// The index passed was outside the range of 0 through 
        /// <see cref = "P:System.Data.IDataRecord.FieldCount" />. </exception>
        /// <filterpriority>2</filterpriority>
        public override Type GetFieldType(int ordinal)
        {
            return StoreDataReader.GetFieldType(MdxColumnsOrder[ordinal]);
        }

        /// <summary>
        ///   Gets the single-precision floating point number of the specified field.
        /// </summary>
        /// <returns>
        ///   The single-precision floating point number of the specified field.
        /// </returns>
        /// <param name = "i">The index of the field to find. </param>
        /// <exception cref = "T:System.IndexOutOfRangeException">
        /// The index passed was outside the range of 0 through
        ///  <see cref = "P:System.Data.IDataRecord.FieldCount" />. 
        /// </exception>
        /// <filterpriority>2</filterpriority>
        public override float GetFloat(int ordinal)
        {
            return StoreDataReader.GetFloat(MdxColumnsOrder[ordinal]);
        }

        /// <summary>
        ///   Returns the GUID value of the specified field.
        /// </summary>
        /// <returns>
        ///   The GUID value of the specified field.
        /// </returns>
        /// <param name = "i">The index of the field to find. </param>
        /// <exception cref = "T:System.IndexOutOfRangeException">
        /// The index passed was outside the range of 0 through
        /// <see cref = "P:System.Data.IDataRecord.FieldCount" />. 
        /// </exception>
        /// <filterpriority>2</filterpriority>
        public override Guid GetGuid(int ordinal)
        {
            var value = StoreDataReader.GetValue( MdxColumnsOrder[ordinal] );
            if ( value == null )
            {
                throw new InvalidOperationException("Cannot convert null DB value into Guid!");
            }
            if ( value is string )
            {
                return Guid.Parse(value.ToString());
            }
            if ( value is Guid )
            {
                return StoreDataReader.GetGuid( MdxColumnsOrder[ordinal] );
            }
            throw new InvalidOperationException( string.Format("Cannot convert DB value of type '{0}' into Guid!", value.GetType().Name) );
        }

        /// <summary>
        ///   Gets the 16-bit signed integer value of the specified field.
        /// </summary>
        /// <returns>
        ///   The 16-bit signed integer value of the specified field.
        /// </returns>
        /// <param name = "i">The index of the field to find. </param>
        /// <exception cref = "T:System.IndexOutOfRangeException">
        /// The index passed was outside the range of 0 through 
        /// <see cref = "P:System.Data.IDataRecord.FieldCount" />.
        /// </exception>
        /// <filterpriority>2</filterpriority>
        public override short GetInt16(int ordinal)
        {
            return StoreDataReader.GetInt16(MdxColumnsOrder[ordinal]);
        }

        /// <summary>
        ///   Gets the 32-bit signed integer value of the specified field.
        /// </summary>
        /// <returns>
        ///   The 32-bit signed integer value of the specified field.
        /// </returns>
        /// <param name = "i">The index of the field to find. </param>
        /// <exception cref = "T:System.IndexOutOfRangeException">
        /// The index passed was outside the range of 0 through 
        /// <see cref = "P:System.Data.IDataRecord.FieldCount" />. 
        /// </exception>
        /// <filterpriority>2</filterpriority>
        public override int GetInt32(int ordinal)
        {
            return StoreDataReader.GetInt32(MdxColumnsOrder[ordinal]);
        }

        /// <summary>
        ///   Gets the 64-bit signed integer value of the specified field.
        /// </summary>
        /// <returns>
        ///   The 64-bit signed integer value of the specified field.
        /// </returns>
        /// <param name = "i">The index of the field to find. </param>
        /// <exception cref = "T:System.IndexOutOfRangeException">
        /// The index passed was outside the range of 0 through 
        /// <see cref = "P:System.Data.IDataRecord.FieldCount" />. 
        /// </exception>
        /// <filterpriority>2</filterpriority>
        public override long GetInt64(int ordinal)
        {
            return StoreDataReader.GetInt64(MdxColumnsOrder[ordinal]);
        }

        /// <summary>
        ///   Gets the name for the field to find.
        /// </summary>
        /// <returns>
        ///   The name of the field or the empty string (""), if there is no value to return.
        /// </returns>
        /// <param name = "i">The index of the field to find. </param>
        /// <exception cref = "T:System.IndexOutOfRangeException">
        /// The index passed was outside the range of 0 through 
        /// <see cref = "P:System.Data.IDataRecord.FieldCount" />. 
        /// </exception>
        /// <filterpriority>2</filterpriority>
        public override string GetName(int ordinal)
        {
            return StoreDataReader.GetName(MdxColumnsOrder[ordinal]);
        }

        /// <summary>
        ///   Return the index of the named field.
        /// </summary>
        /// <returns>
        ///   The index of the named field.
        /// </returns>
        /// <param name = "name">The name of the field to find. </param>
        /// <filterpriority>2</filterpriority>
        public override int GetOrdinal(string name)
        {
            return StoreDataReader.GetOrdinal(GetMdxColumnName(name));
        }

        string GetMdxColumnName(string name)
        { //TODO: (minor) improve this logic to handle a case when a classes have different property names mapped to different dimensions or measures with the same level name. We always return results for a single entity only, so this is minor

            string mdxColumnName = MdxMapper.GetMdxColumnName(name);
            if(mdxColumnName == null)
            { //EF handles this IndexOutOfRangeException and throws a nicely specified error
                throw new ArgumentException(string.Format(
                    "MDX result does not have a column matching '{0}'", name));
            }
            return mdxColumnName;
        }

        /// <summary>
        ///   Gets the string value of the specified field.
        /// </summary>
        /// <returns>
        ///   The string value of the specified field.
        /// </returns>
        /// <param name = "i">The index of the field to find. </param>
        /// <exception cref = "T:System.IndexOutOfRangeException">
        /// The index passed was outside the range of 0 through 
        /// <see cref = "P:System.Data.IDataRecord.FieldCount" />. 
        /// </exception>
        /// <filterpriority>2</filterpriority>
        public override string GetString(int ordinal)
        {
            return StoreDataReader.GetString(MdxColumnsOrder[ordinal]);
        }

        public TimeSpan GetTimeSpan(int ordinal)
        {
            return StoreDataReader.GetTimeSpan(MdxColumnsOrder[ordinal]);
        }

        /// <summary>
        ///   Return the value of the specified field.
        /// </summary>
        /// <returns>
        ///   The <see cref = "T:System.Object" /> which will contain the field value upon return.
        /// </returns>
        /// <param name = "i">The index of the field to find. </param>
        /// <exception cref = "T:System.IndexOutOfRangeException">
        /// The index passed was outside the range of 0 through 
        /// <see cref = "P:System.Data.IDataRecord.FieldCount" />. 
        /// </exception>
        /// <filterpriority>2</filterpriority>
        public override object GetValue(int ordinal)
        {
            return StoreDataReader.GetValue(MdxColumnsOrder[ordinal]);
        }

        public AdomdDataReader GetDataReader(int ordinal)
        {
            return StoreDataReader.GetDataReader(MdxColumnsOrder[ordinal]);
        }

        /// <summary>
        ///   Populates an array of objects with the column values of the current record.
        /// </summary>
        /// <returns>
        ///   The number of instances of <see cref = "T:System.Object" /> in the array.
        /// </returns>
        /// <param name = "values">An array of <see cref = "T:System.Object" /> 
        /// to copy the attribute fields into. 
        /// </param>
        /// <filterpriority>2</filterpriority>
        public override int GetValues(object[] values)
        {
            //TODO: Is this GetValues() method used?
            throw new NotImplementedException(
                @"LINQ requested to MDX column order mapping is not implemented here yet.");
            //return StoreDataReader.GetValues(values);
        }

        /// <summary>
        ///   Return whether the specified field is set to null.
        /// </summary>
        /// <returns>
        ///   true if the specified field is set to null; otherwise, false.
        /// </returns>
        /// <param name = "i">The index of the field to find. </param>
        /// <exception cref = "T:System.IndexOutOfRangeException">
        /// The index passed was outside the range of 0 through 
        /// <see cref = "P:System.Data.IDataRecord.FieldCount" />. 
        /// </exception>
        /// <filterpriority>2</filterpriority>
        public override bool IsDBNull(int ordinal)
        {
            return StoreDataReader.IsDBNull(MdxColumnsOrder[ordinal]);
        }
    }
}