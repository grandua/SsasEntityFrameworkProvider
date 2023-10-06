using System;
using System.ComponentModel.DataAnnotations;

namespace ModelExample
{
    public class ReportLineAllPrimitiveDataTypesTest
    {
        //Dimensions
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string WCharItemName { get; set; }
        public Guid GuidCode { get; set; }

        //Facts
        //  Not nullable
        //      Signed
        public long BigIntSum { get; set; }
        public bool BoolFact { get; set; }
        [Column(TypeName = "money")]
        public decimal CurrencySum { get; set; }
        public decimal DecimalSum { get; set; }
        public double DoubleSum { get; set; }
        public int IntSum { get; set; }
        public float SingleSum { get; set; }
        public short SmallIntSum { get; set; }
        public byte TinyIntSum { get; set; }

        /// <remarks>
        /// byte is converted to tinyint in MS SQL
        /// </remarks>
        public byte UnsignedTinyIntDoesNotExistInSsasSum { get; set; }

        //  Nullable
        //      Signed
        public long? BigIntNullableSum { get; set; }
        public bool? BoolNullableFact { get; set; }
        [Column(TypeName = "money")]
        public decimal? CurrencyNullableSum { get; set; }
        public decimal? DecimalNullableSum { get; set; }
        public double? DoubleNullableSum { get; set; }
        public int? IntNullableSum { get; set; }
        public float? SingleNullableSum { get; set; }
        public short? SmallIntNullableSum { get; set; }
        public byte? TinyIntNullableSum { get; set; }

        /// <remarks>
        /// byte is converted to tinyint in MS SQL
        /// </remarks>
        public byte? UnsignedTinyIntNullableDoesNotExistInSsasSum { get; set; }

        //decimal.MaxValue leads to overflow and cannot be used here
        const decimal moneyValue = 123456789012345.1234m;
        const decimal decimalValue = 1234567890123456.12m;

        public void SetTestValues()
        {
            Id = 42;
            //max datetime in MS SQL Server, 
            //DateTime.MaxValue is "9999-12-31 23:59:59.9999999" so it cannot be used
            Date = DateTime.Parse("9999-12-31 23:59:59.9970000"); 
            WCharItemName = "Very important line";

            BigIntSum = long.MinValue;
            BoolFact = true;
            CurrencySum = - moneyValue;
            DecimalSum = - decimalValue;
            DoubleSum = double.MinValue;
            IntSum = int.MinValue;
            SingleSum = float.MinValue;
            SmallIntSum = short.MinValue;
            TinyIntSum = byte.MaxValue;

            UnsignedTinyIntDoesNotExistInSsasSum = byte.MaxValue;

            BigIntNullableSum = long.MaxValue;
            BoolNullableFact = false;
            CurrencyNullableSum = moneyValue;
            DecimalNullableSum = decimalValue;
            DoubleNullableSum = null; //test nullability //double.MaxValue;
            IntNullableSum= int.MaxValue;
            SingleNullableSum = float.MaxValue;
            SmallIntNullableSum = short.MaxValue;
            TinyIntNullableSum = 0;
            GuidCode = Guid.Parse( "A964FE28-A6D5-4EE1-BA21-00D2E9D3DC97" );
            //leave all unsigned nullable types set to null to test their nullability
        }

    }
}