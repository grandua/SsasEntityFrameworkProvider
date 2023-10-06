using System;
using System.Collections;
using System.Data;
using System.Data.Common;

namespace AgileDesign.SsasEntityFrameworkProvider.AdomdClient
{
    /// <summary>
    /// Acts like 1 row 1 column recordset is returned whose value is null
    /// </summary>
    public class DoNothingDataReader : DbDataReader
    {
        public override void Close()
        {
        }

        public override DataTable GetSchemaTable()
        {
            return null;
        }

        public override bool NextResult()
        {
            throw new NotImplementedException();
        }

        bool firstRead = true;
        public override bool Read()
        {
            if (!firstRead) return false;

            firstRead = false;
            return true;
        }

        public override int Depth
        {
            get { return 0; }
        }

        public override bool IsClosed
        {
            get { return false; }
        }

        public override int RecordsAffected
        {
            get { return 1; }
        }

        public override bool GetBoolean(int ordinal)
        {
            return false;
        }

        public override byte GetByte(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override long GetBytes(int ordinal,
                                      long dataOffset,
                                      byte[] buffer,
                                      int bufferOffset,
                                      int length)
        {
            throw new NotImplementedException();
        }

        public override char GetChar(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override long GetChars(int ordinal,
                                      long dataOffset,
                                      char[] buffer,
                                      int bufferOffset,
                                      int length)
        {
            throw new NotImplementedException();
        }

        public override Guid GetGuid(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override short GetInt16(int ordinal)
        {
            return default(short);
        }

        public override int GetInt32(int ordinal)
        {
            return default(int);
        }

        public override long GetInt64(int ordinal)
        {
            return default(long);
        }

        public override DateTime GetDateTime(int ordinal)
        {
            return default(DateTime);
        }

        public override string GetString(int ordinal)
        {
            return null;
        }

        public override object GetValue(int ordinal)
        {
            return null;
        }

        public override int GetValues(object[] values)
        {
            values = new object[] {null};
            return 1;
        }

        public override bool IsDBNull(int ordinal)
        {
            return true;
        }

        public override int FieldCount
        {
            get { return 1; }
        }

        public override object this[int ordinal]
        {
            get { return null; }
        }

        public override object this[string name]
        {
            get { return null; }
        }

        public override bool HasRows
        {
            get { return firstRead; }
        }

        public override decimal GetDecimal(int ordinal)
        {
            return default(decimal);
        }

        public override double GetDouble(int ordinal)
        {
            return default(double);
        }

        public override float GetFloat(int ordinal)
        {
            return default(float);
        }

        public override string GetName(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override int GetOrdinal(string name)
        {
            return 0;
        }

        public override string GetDataTypeName(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override Type GetFieldType(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override IEnumerator GetEnumerator()
        {
            return new ArrayList {null}.GetEnumerator();
        }
    }
}