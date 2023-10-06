using System;
using System.Data;
using System.Data.Entity.Core;
using System.Data.Entity.Core.Common;
using System.Data.Entity.Core.Metadata.Edm;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using AgileDesign.Utilities;

namespace AgileDesign.SsasEntityFrameworkProvider.Internal
{
    [SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses")]
    class SsasProviderManifest : DbXmlEnabledProviderManifest
    {
        internal const string TokenSql8 = "2000";
        internal const string TokenSql9 = "2005";
        internal const string TokenSql10 = "2008";

        internal const char LikeEscapeChar = '~';
        internal const string LikeEscapeCharToString = "~";
        const int varcharMaxSize = 8000;
        const int nvarcharMaxSize = 4000;
        const int binaryMaxSize = 8000;
        const string resourceLocationCommonPart = ".Resources.SsasProvider.";
        static readonly string providerAssemblyName 
            = typeof(SsasProviderManifest).Assembly.GetName().Name;

        //static readonly string providerNamesapce = typeof(SsasProviderManifest).Namespace + ".";
        static readonly string providerNamesapce = "AgileDesign.SsasEntityFrameworkProvider.Internal.";

        public SsasProviderManifest(string manifestToken)
            : base(GetProviderManifest())
        {
            _version = StoreVersionUtils.GetStoreVersion(manifestToken);
        }

        [SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        readonly StoreVersion _version;

        public StoreVersion Version
        {
            get { return _version; }
        }

        [ContractInvariantMethod]
        void ObjectInvariant()
        {
            Contract.Invariant(IsVersionSupported());
        }

        bool IsVersionSupported()
        {
            return _version == StoreVersion.Sql9
                   || _version == StoreVersion.Sql10;
        }

        /// <summary>
        ///   When overridden in a derived class, this method maps the specified storage type and a set of facets for that type to an EDM type.
        /// </summary>
        /// <returns>
        ///   The T:System.Data.Metadata.Edm.TypeUsage instance that describes an EDM type and a set of facets for that type.
        /// </returns>
        /// <param name = "storeType">The <see cref = "T:System.Data.Metadata.Edm.TypeUsage" /> instance that describes a storage type and a set of facets for that type to be mapped to the EDM type.</param>
        public override TypeUsage GetEdmType(TypeUsage storeType)
        {
            //TODO: move to EdmTypeDescriptor after tests are green
            Contract.Requires<ArgumentNullException>(storeType != null);
            string storeTypeName = storeType.EdmType.Name.ToLowerInvariant();
            if (! StoreTypeNameToEdmPrimitiveType.ContainsKey(storeTypeName))
            {
                throw new ArgumentException(String.Format(
                            "The underlying provider does not support the type '{0}'.",
                            storeTypeName));
            }

            var typeDescriptor = new EdmTypeDescriptor
            {
                edmPrimitiveType = StoreTypeNameToEdmPrimitiveType[storeTypeName]
            };

            switch (storeTypeName)
            { //both DbType and MS SQL types are supported 
                //to allow usage of the same class with both SSAS and MS SQL

                //TODO: move to EdmTypeDescriptor after tests are green
                // for some types we just go with simple type usage with no facets
                case "uint16" :
                case "int16" :
                case "int32" :
                case "uint32":
                case "int64" :
                case "uint64" :
                case "boolean" :
                case "byte" :
                case "sbyte" :
                case "guid" :
                case "tinyint":
                case "smallint":
                case "bigint":
                case "bit":
                case "uniqueidentifier":
                case "int":
                    return TypeUsage.CreateDefaultTypeUsage(typeDescriptor.edmPrimitiveType);
                case "varchar":
                case "ansistring":
                    typeDescriptor.newPrimitiveTypeKind = PrimitiveTypeKind.String;
                    typeDescriptor.isUnbounded = !TypeHelpers.TryGetMaxLength(storeType, out typeDescriptor.maxLength);
                    typeDescriptor.isUnicode = false;
                    typeDescriptor.isFixedLen = false;
                    return GetTypeUsage(typeDescriptor, storeTypeName);
                case "ansistringfixedlength" :
                case "char":
                    typeDescriptor.newPrimitiveTypeKind = PrimitiveTypeKind.String;
                    typeDescriptor.isUnbounded = !TypeHelpers.TryGetMaxLength(storeType, out typeDescriptor.maxLength);
                    typeDescriptor.isUnicode = false;
                    typeDescriptor.isFixedLen = true;
                    return GetTypeUsage(typeDescriptor, storeTypeName);
                case "string" :
                case "nvarchar":
                    typeDescriptor.newPrimitiveTypeKind = PrimitiveTypeKind.String;
                    typeDescriptor.isUnbounded = !TypeHelpers.TryGetMaxLength(storeType, out typeDescriptor.maxLength);
                    typeDescriptor.isUnicode = true;
                    typeDescriptor.isFixedLen = false;
                    return GetTypeUsage(typeDescriptor, storeTypeName);
                case "stringfixedlength" :
                case "nchar":
                    typeDescriptor.newPrimitiveTypeKind = PrimitiveTypeKind.String;
                    typeDescriptor.isUnbounded = !TypeHelpers.TryGetMaxLength(storeType, out typeDescriptor.maxLength);
                    typeDescriptor.isUnicode = true;
                    typeDescriptor.isFixedLen = true;
                    return GetTypeUsage(typeDescriptor, storeTypeName);
                case "varchar(max)":
                case "text":
                    typeDescriptor.newPrimitiveTypeKind = PrimitiveTypeKind.String;
                    typeDescriptor.isUnbounded = true;
                    typeDescriptor.isUnicode = false;
                    typeDescriptor.isFixedLen = false;
                    return GetTypeUsage(typeDescriptor, storeTypeName);
                case "nvarchar(max)":
                case "ntext":
                case "xml":
                    typeDescriptor.newPrimitiveTypeKind = PrimitiveTypeKind.String;
                    typeDescriptor.isUnbounded = true;
                    typeDescriptor.isUnicode = true;
                    typeDescriptor.isFixedLen = false;
                    return GetTypeUsage(typeDescriptor, storeTypeName);
                case "binary":
                    typeDescriptor.newPrimitiveTypeKind = PrimitiveTypeKind.Binary;
                    typeDescriptor.isUnbounded = !TypeHelpers.TryGetMaxLength(storeType, out typeDescriptor.maxLength);
                    typeDescriptor.isFixedLen = false;
                    return GetTypeUsage(typeDescriptor, storeTypeName);
                case "varbinary":
                    typeDescriptor.newPrimitiveTypeKind = PrimitiveTypeKind.Binary;
                    typeDescriptor.isUnbounded 
                        = ! TypeHelpers.TryGetMaxLength(storeType, out typeDescriptor.maxLength);
                    typeDescriptor.isFixedLen = false;
                    return GetTypeUsage(typeDescriptor, storeTypeName);
                case "varbinary(max)":
                case "image":
                case "object" :
                    typeDescriptor.newPrimitiveTypeKind = PrimitiveTypeKind.Binary;
                    typeDescriptor.isUnbounded = true;
                    typeDescriptor.isFixedLen = false;
                    return GetTypeUsage(typeDescriptor, storeTypeName);
                case "timestamp":
                case "rowversion":
                    return TypeUsage.CreateBinaryTypeUsage(typeDescriptor.edmPrimitiveType, true, 8);
                case "double":
                case "single":
                case "float":
                case "real":
                    return TypeUsage.CreateDefaultTypeUsage(typeDescriptor.edmPrimitiveType);
                case "varnumeric" :
                case "decimal":
                case "numeric":
                    {
                    byte precision;
                    byte scale;
                    if (TypeHelpers.TryGetPrecision(storeType, out precision)
                        && TypeHelpers.TryGetScale(storeType, out scale))
                    {
                        return TypeUsage.CreateDecimalTypeUsage(typeDescriptor.edmPrimitiveType, precision, scale);
                    }
                    return TypeUsage.CreateDecimalTypeUsage(typeDescriptor.edmPrimitiveType);
                }
                case "currency" :
                case "money":
                    return TypeUsage.CreateDecimalTypeUsage(typeDescriptor.edmPrimitiveType, 19, 4);
                case "smallmoney":
                    return TypeUsage.CreateDecimalTypeUsage(typeDescriptor.edmPrimitiveType, 10, 4);
                case "datetime":
                case "datetime2" :
                case "datetimeoffset" :
                case "time" : //TODO: should I use PrimitiveType=String for DbType.Time?
                case "smalldatetime":
                    return TypeUsage.CreateDateTimeTypeUsage(typeDescriptor.edmPrimitiveType, null);

                default :
                    throw new NotSupportedException
                        (String.Format("The underlying provider does not support the type '{0}'.", storeTypeName));
            }
        }

        TypeUsage GetTypeUsage
            (
            EdmTypeDescriptor typeDescriptor,
            string storeTypeName)
        {
            //TODO: move to EdmTypeDescriptor after tests are green
            Contract.Requires
                (
                    typeDescriptor.newPrimitiveTypeKind == PrimitiveTypeKind.String
                    || typeDescriptor.newPrimitiveTypeKind == PrimitiveTypeKind.Binary
                    ,
                    "at this point only string and binary types should be present");

            switch (typeDescriptor.newPrimitiveTypeKind)
            {
                case PrimitiveTypeKind.String :
                    if (typeDescriptor.isUnbounded)
                    {
                        return TypeUsage.CreateStringTypeUsage
                            (typeDescriptor.edmPrimitiveType, typeDescriptor.isUnicode, typeDescriptor.isFixedLen);
                    }
                    return TypeUsage.CreateStringTypeUsage
                        (
                            typeDescriptor.edmPrimitiveType,
                            typeDescriptor.isUnicode,
                            typeDescriptor.isFixedLen,
                            typeDescriptor.maxLength);

                case PrimitiveTypeKind.Binary :
                    if (typeDescriptor.isUnbounded)
                    {
                        return TypeUsage.CreateBinaryTypeUsage
                            (typeDescriptor.edmPrimitiveType, typeDescriptor.isFixedLen);
                    }
                    return TypeUsage.CreateBinaryTypeUsage
                        (
                            typeDescriptor.edmPrimitiveType,
                            typeDescriptor.isFixedLen,
                            typeDescriptor.maxLength);
                default :
                    throw new NotSupportedException
                        (String.Format("The underlying provider does not support the type '{0}'.", storeTypeName));
            }
        }

        /// <summary>
        ///   When overridden in a derived class, this method maps the specified EDM type and a set of facets for that type to a storage type.
        /// </summary>
        /// <returns>
        ///   The <see cref = "T:System.Data.Metadata.Edm.TypeUsage" /> instance that describes a storage type and a set of facets for that type.
        /// </returns>
        /// <param name = "edmType">The <see cref = "T:System.Data.Metadata.Edm.TypeUsage" />
        ///   instance that describes the EDM type and a set of facets for that type to be mapped to a storage type.
        /// </param>
        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")
        ]
        public override TypeUsage GetStoreType(TypeUsage edmType)
        {
            Contract.Requires<ArgumentNullException>(edmType != null);
            Contract.Requires<ArgumentNullException>(edmType.EdmType != null);
            Contract.Requires<ArgumentException>(edmType.EdmType is PrimitiveType);
            Contract.Requires<ArgumentException>(
                edmType.EdmType.BuiltInTypeKind == BuiltInTypeKind.PrimitiveType);

            var primitiveType = (PrimitiveType)edmType.EdmType;
            ReadOnlyMetadataCollection<Facet> facets = edmType.Facets;

            switch (primitiveType.PrimitiveTypeKind)
            {
                case PrimitiveTypeKind.Boolean : //"bit"
                    return TypeUsage.CreateDefaultTypeUsage
                        (
                            StoreTypeNameToStorePrimitiveType[GetTypeKey(
                                DbType.Boolean)]
                        );
                case PrimitiveTypeKind.Byte : //"tinyint"
                case PrimitiveTypeKind.Int16 : //"smallint"
                    return TypeUsage.CreateDefaultTypeUsage
                        (
                            StoreTypeNameToStorePrimitiveType[GetTypeKey(
                                DbType.Int16)]
                        );
                case PrimitiveTypeKind.Int32 : //"int"
                    return TypeUsage.CreateDefaultTypeUsage
                        (
                            StoreTypeNameToStorePrimitiveType[GetTypeKey(
                                DbType.Int32)]
                        );
                case PrimitiveTypeKind.Int64 : //"bigint"
                    return TypeUsage.CreateDefaultTypeUsage
                        (
                            StoreTypeNameToStorePrimitiveType[GetTypeKey(
                                DbType.Int64)]
                        );
                case PrimitiveTypeKind.Guid : //"uniqueidentifier"
                    return TypeUsage.CreateDefaultTypeUsage
                        (
                            StoreTypeNameToStorePrimitiveType[GetTypeKey(
                                DbType.Guid)]
                        );
                case PrimitiveTypeKind.Double : //"float"
                    return TypeUsage.CreateDefaultTypeUsage
                        (
                            StoreTypeNameToStorePrimitiveType[GetTypeKey(
                                DbType.Double)]
                        );
                case PrimitiveTypeKind.Single : //"real"
                    return TypeUsage.CreateDefaultTypeUsage
                        (
                            StoreTypeNameToStorePrimitiveType[GetTypeKey(
                                DbType.Single)]
                        );
                case PrimitiveTypeKind.Decimal : //"extDecimal" 
                {
                    return CreateDecimalTypeUsage(edmType);
                }
                case PrimitiveTypeKind.Binary : //"binary"
                {
                    return CreateBinaryTypeUsage(facets);
                }
                case PrimitiveTypeKind.String :
                {
                    return CreateStringTypeUsage(facets);
                }
                case PrimitiveTypeKind.DateTime: // datetime, smalldatetime
                {
                    return TypeUsage.CreateDefaultTypeUsage
                        (
                            StoreTypeNameToStorePrimitiveType[GetTypeKey(
                                DbType.DateTime)]
                        );
                }
                default :
                    throw CreateNotSupportedStoreTypeException(edmType, primitiveType);
            }
        }

        NotSupportedException CreateNotSupportedStoreTypeException(TypeUsage edmType,
                                                                   PrimitiveType primitiveType)
        {
            return new NotSupportedException
                (
                String.Format
                    (
                        "There is no store type corresponding to the EDM type '{0}' of primitive type '{1}'.",
                        edmType,
                        primitiveType.PrimitiveTypeKind));
        }

        /// <summary>
        /// decimal, numeric, smallmoney, money
        /// </summary>
        TypeUsage CreateDecimalTypeUsage(TypeUsage edmType)
        {
            byte precision;
            if (!TypeHelpers.TryGetPrecision(edmType, out precision))
            {
                precision = 18;
            }
            byte scale;
            if (!TypeHelpers.TryGetScale(edmType, out scale))
            {
                scale = 0;
            }
            return TypeUsage.CreateDecimalTypeUsage
                (
                    StoreTypeNameToStorePrimitiveType[GetTypeKey(
                        DbType.Decimal)],
                    precision,
                    scale
                );
        }

        /// <summary>
        /// String, char, nchar, varchar, nvarchar, varchar(max), nvarchar(max), ntext, text, xml
        /// </summary>
        TypeUsage CreateStringTypeUsage(ReadOnlyMetadataCollection<Facet> facets)
        {
            bool isUnicode = null == facets["Unicode"].Value || (bool)facets["Unicode"].Value;
            bool isFixedLength = null != facets["FixedLength"].Value && (bool)facets["FixedLength"].Value;
            Facet f = facets["MaxLength"];
            // maxlen is true if facet value is unbounded, the value is bigger than the limited string sizes *or* the facet
            // value is null. this is needed since functions still have maxlength facet value as null
            bool isMaxLength = f.IsUnbounded || null == f.Value || (int)f.Value > ( isUnicode
                                                                                        ? nvarcharMaxSize
                                                                                        : varcharMaxSize );
            int maxLength = !isMaxLength
                                ? (int)f.Value
                                : Int32.MinValue;

            TypeUsage tu;

            if (isUnicode)
            {
                if (isFixedLength)
                {
                    //"nchar"
                    tu = TypeUsage.CreateStringTypeUsage
                        (
                            StoreTypeNameToStorePrimitiveType[
                                DbType.StringFixedLength.ToString().ToLower()],
                            true,
                            true,
                            maxLength
                        );
                }
                else
                {
                    if (isMaxLength)
                    {
                        //"nvarchar(max)"
                        tu = TypeUsage.CreateStringTypeUsage
                            (
                                StoreTypeNameToStorePrimitiveType[
                                    GetTypeKey(DbType.String)],
                                true,
                                false
                            );
                    }
                    else
                    {
                        //"nvarchar"
                        tu = TypeUsage.CreateStringTypeUsage
                            (
                                StoreTypeNameToStorePrimitiveType[
                                    GetTypeKey(DbType.String)],
                                true,
                                false,
                                maxLength
                            );
                    }
                }
            }
            else
            {
                if (isFixedLength)
                {
                    //"char"
                    tu = TypeUsage.CreateStringTypeUsage
                        (
                            StoreTypeNameToStorePrimitiveType[
                                DbType.AnsiStringFixedLength.ToString()],
                            false,
                            true,
                            maxLength
                        );
                }
                else
                {
                    if (isMaxLength)
                    {
                        //"varchar(max)"
                        tu = TypeUsage.CreateStringTypeUsage
                            (
                                StoreTypeNameToStorePrimitiveType[
                                    DbType.AnsiString.ToString()],
                                false,
                                false
                            );
                    }
                    else
                    {
                        //"varchar"
                        tu = TypeUsage.CreateStringTypeUsage
                            (
                                StoreTypeNameToStorePrimitiveType[
                                    DbType.AnsiString.ToString()],
                                false,
                                false,
                                maxLength
                            );
                    }
                }
            }
            return tu;
        }

        /// <summary>
        /// binary, varbinary, varbinary(max), image, timestamp, rowversion
        /// </summary>
        TypeUsage CreateBinaryTypeUsage(ReadOnlyMetadataCollection<Facet> facets)
        {
            bool isFixedLength 
                = (null != facets["FixedLength"].Value 
                   && (bool)facets["FixedLength"].Value);

            Facet f = facets["MaxLength"];

            bool isMaxLength = f.IsUnbounded || null == f.Value || (int)f.Value > binaryMaxSize;
            int maxLength = !isMaxLength
                                ? (int)f.Value
                                : Int32.MinValue;

            TypeUsage tu;
            if (isFixedLength)
            {
                tu = TypeUsage.CreateBinaryTypeUsage
                    (
                        StoreTypeNameToStorePrimitiveType[GetTypeKey(
                            DbType.Binary)],
                        true,
                        maxLength);
            }
            else
            {
                if (isMaxLength)
                {
                    tu = TypeUsage.CreateBinaryTypeUsage
                        (
                            StoreTypeNameToStorePrimitiveType[GetTypeKey(
                                DbType.Binary)],
                            false
                        );
                    //"varbinary(max) is not constant!"
                    //Contract.Assert(tu.Facets["MaxLength"].Description.IsConstant);
                }
                else
                {
                    tu = TypeUsage.CreateBinaryTypeUsage
                        (
                            StoreTypeNameToStorePrimitiveType[GetTypeKey(
                                DbType.Binary)],
                            false,
                            maxLength
                        );
                }
            }
            return tu;
        }

        string GetTypeKey(DbType dbType)
        {
            return dbType.ToString().ToLower();
        }

        /// <summary>
        ///   When overridden in a derived class, this method returns provider-specific information. This method should never return null.
        /// </summary>
        /// <returns>
        ///   The <see cref = "T:System.Xml.XmlReader" /> object that contains the requested information.
        /// </returns>
        /// <param name = "informationType">The type of the information to return.</param>
        protected override XmlReader GetDbInformation(string informationType)
        {
            Contract.Requires<ArgumentNullException>(informationType != null);
            Contract.Requires<ProviderIncompatibleException>(
                    informationType == StoreSchemaDefinition
                    || informationType == StoreSchemaMapping);

            if (informationType == StoreSchemaDefinition)
            {
                return GetStoreSchemaDescription();
            }
            return GetStoreSchemaMapping();
        }

        static string GetResourceLocation()
        {
            return String.Format
                (
                    "{0}.{1}{2}"
                    ,
                    providerNamesapce.Split('.')[0]
                    ,
                    providerAssemblyName
                    ,
                    resourceLocationCommonPart);
        }

        static XmlReader GetProviderManifest()
        {
            return GetXmlResource(GetResourceLocation() + "ProviderManifest.xml");
        }

        XmlReader GetStoreSchemaMapping()
        {
            return GetXmlResource(GetResourceLocation() + "StoreSchemaMapping.msl");
        }

        XmlReader GetStoreSchemaDescription()
        {
            return GetXmlResource(GetResourceLocation() + "StoreSchemaDefinition.ssdl");
        }

        static XmlReader GetXmlResource(string resourceName)
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            Stream stream = executingAssembly.GetManifestResourceStream(resourceName);
            Contract.Assert(stream != null);
            return XmlReader.Create(stream);
        }


        #region Helpers

        static class TypeHelpers
        {
            public static bool TryGetPrecision
                (
                TypeUsage tu,
                out byte precision)
            {
                Facet f;

                precision = 0;
                if (tu.Facets.TryGetValue("Precision", false, out f))
                {
                    if (!f.IsUnbounded
                        && f.Value != null)
                    {
                        precision = (byte)f.Value;
                        return true;
                    }
                }
                return false;
            }

            public static bool TryGetMaxLength
                (
                TypeUsage tu,
                out int maxLength)
            {
                Facet f;

                maxLength = 0;
                if (tu.Facets.TryGetValue("MaxLength", false, out f))
                {
                    if (!f.IsUnbounded
                        && f.Value != null)
                    {
                        maxLength = (int)f.Value;
                        return true;
                    }
                }
                return false;
            }

            public static bool TryGetScale
                (
                TypeUsage tu,
                out byte scale)
            {
                Facet f;

                scale = 0;
                if (tu.Facets.TryGetValue("Scale", false, out f))
                {
                    if (!f.IsUnbounded
                        && f.Value != null)
                    {
                        scale = (byte)f.Value;
                        return true;
                    }
                }
                return false;
            }
        }

        #endregion


        #region Nested type: EdmTypeDescriptor

        class EdmTypeDescriptor
        {
            public PrimitiveType edmPrimitiveType;
            public bool isFixedLen;
            public bool isUnbounded = true;
            public bool isUnicode = true;
            public int maxLength;
            public PrimitiveTypeKind newPrimitiveTypeKind;
        }

        #endregion

        /// <summary>
        /// Function to detect wildcard characters %, _, [ and ^ and escape them with a preceding ~
        /// This escaping is used when StartsWith, EndsWith and Contains canonical and CLR functions
        /// are translated to their equivalent LIKE expression
        /// </summary>
        /// <param name="text">Original input as specified by the user</param>
        /// <param name="alwaysEscapeEscapeChar">escape the escape character ~ regardless whether wildcard 
        /// characters were encountered </param>
        /// <param name="usedEscapeChar">true if the escaping was performed, false if no escaping was required</param>
        /// <returns>The escaped string that can be used as pattern in a LIKE expression</returns>
        internal static string EscapeLikeText(string text, bool alwaysEscapeEscapeChar, out bool usedEscapeChar)
        {
            usedEscapeChar = false;
            if (!(text.Contains("%") || text.Contains("_") || text.Contains("[")
                || text.Contains("^") || alwaysEscapeEscapeChar && text.Contains(LikeEscapeCharToString)))
            {
                return text;
            }
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == '%' || c == '_' || c == '[' || c == '^' || c == LikeEscapeChar)
                {
                    sb.Append(LikeEscapeChar);
                    usedEscapeChar = true;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        public const string CubeNamePlaceholder = "<CubeNamePlaceholder>";
    }
}