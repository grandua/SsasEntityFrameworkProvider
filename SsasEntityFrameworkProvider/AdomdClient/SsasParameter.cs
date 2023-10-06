using System.Data;
using System.Data.Common;
using AgileDesign.Utilities;
using Microsoft.AnalysisServices.AdomdClient;

namespace AgileDesign.SsasEntityFrameworkProvider.AdomdClient
{
    public class SsasParameter : DbParameter
    {
        public SsasParameter()
        {
        }

        public SsasParameter(AdomdParameter adomdParameter)
        {
            StoreParameter = adomdParameter;
        }

        AdomdParameter storeParameter;

        internal AdomdParameter StoreParameter
        {
            get { return Init.InitIfNull(ref storeParameter); }
            set { storeParameter = value; }
        }

        /// <summary>
        ///   Gets a value indicating whether the parameter accepts null values.
        /// </summary>
        /// <returns>
        ///   true if null values are accepted; otherwise, false. The default is false.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override bool IsNullable
        {
            get { return true; }
            set { ; }
        }

        /// <summary>
        ///   Gets or sets the name of the <see cref = "T:System.Data.IDataParameter" />.
        /// </summary>
        /// <returns>
        ///   The name of the <see cref = "T:System.Data.IDataParameter" />. The default is an empty string.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ParameterName
        {
            get { return storeParameter.ParameterName; }
            set { storeParameter.ParameterName = value; }
        }

        /// <summary>
        ///   Gets or sets the name of the source column that is mapped to the <see cref = "T:System.Data.DataSet" /> and used for loading or returning the <see cref = "P:System.Data.IDataParameter.Value" />.
        /// </summary>
        /// <returns>
        ///   The name of the source column that is mapped to the <see cref = "T:System.Data.DataSet" />. The default is an empty string.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string SourceColumn
        {
            get { return storeParameter.SourceColumn; }
            set { storeParameter.SourceColumn = value; }
        }

        /// <summary>
        ///   Sets or gets a value which indicates whether the source column is nullable. This allows <see cref = "T:System.Data.Common.DbCommandBuilder" /> to correctly generate Update statements for nullable columns.
        /// </summary>
        /// <returns>
        ///   true if the source column is nullable; false if it is not.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public override bool SourceColumnNullMapping
        {
            get { return StoreParameter.IsNullable; }
            set { StoreParameter.IsNullable = value; }
        }

        /// <summary>
        ///   Gets or sets the <see cref = "T:System.Data.DataRowVersion" /> to use when loading <see cref = "P:System.Data.IDataParameter.Value" />.
        /// </summary>
        /// <returns>
        ///   One of the <see cref = "T:System.Data.DataRowVersion" /> values. The default is Current.
        /// </returns>
        /// <exception cref = "T:System.ArgumentException">The property was not set one of the <see cref = "T:System.Data.DataRowVersion" /> values. </exception>
        /// <filterpriority>2</filterpriority>
        public override DataRowVersion SourceVersion
        {
            get { return storeParameter.SourceVersion; }
            set { storeParameter.SourceVersion = value; }
        }

        /// <summary>
        ///   Gets or sets the value of the parameter.
        /// </summary>
        /// <returns>
        ///   An <see cref = "T:System.Object" /> that is the value of the parameter. The default value is null.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override object Value
        {
            get { return storeParameter.Value; }
            set { storeParameter.Value = value; }
        }

        /// <summary>
        ///   Indicates the precision of numeric parameters.
        /// </summary>
        /// <returns>
        ///   The maximum number of digits used to represent the Value property of a data provider Parameter object. The default value is 0, which indicates that a data provider sets the precision for Value.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public byte Precision
        {
            get { return storeParameter.Precision; }
            set { storeParameter.Precision = value; }
        }

        /// <summary>
        ///   Indicates the scale of numeric parameters.
        /// </summary>
        /// <returns>
        ///   The number of decimal places to which <see cref = "T:System.Data.OleDb.OleDbParameter.Value" /> is resolved. The default is 0.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public byte Scale
        {
            get { return storeParameter.Scale; }
            set { storeParameter.Scale = value; }
        }

        /// <summary>
        ///   The size of the parameter.
        /// </summary>
        /// <returns>
        ///   The maximum size, in bytes, of the data within the column. The default value is inferred from the the parameter value.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override int Size
        {
            get { return storeParameter.Size; }
            set { storeParameter.Size = value; }
        }

        /// <summary>
        ///   Gets or sets the <see cref = "T:System.Data.DbType" /> of the parameter.
        /// </summary>
        /// <returns>
        ///   One of the <see cref = "T:System.Data.DbType" /> values. The default is <see cref = "F:System.Data.DbType.String" />.
        /// </returns>
        /// <exception cref = "T:System.ArgumentException">The property is not set to a valid <see cref = "T:System.Data.DbType" />.</exception>
        /// <filterpriority>1</filterpriority>
        public override DbType DbType
        {
            get { return StoreParameter.DbType; }
            set { StoreParameter.DbType = value; }
        }

        /// <summary>
        ///   Gets or sets a value that indicates whether the parameter is input-only, output-only, bidirectional, or a stored procedure return value parameter.
        /// </summary>
        /// <returns>
        ///   One of the <see cref = "T:System.Data.ParameterDirection" /> values. The default is Input.
        /// </returns>
        /// <exception cref = "T:System.ArgumentException">The property is not set to one of the valid <see cref = "T:System.Data.ParameterDirection" /> values.</exception>
        /// <filterpriority>1</filterpriority>
        public override ParameterDirection Direction
        {
            get { return StoreParameter.Direction; }
            set { StoreParameter.Direction = value; }
        }

        /// <summary>
        ///   Resets the DbType property to its original settings.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public override void ResetDbType()
        {
            //TODO: was it string by default in AdomdParameter?
            StoreParameter.DbType = DbType.String;
        }

        internal const string NameParamerterPrefix = "<Name_";
    }
}