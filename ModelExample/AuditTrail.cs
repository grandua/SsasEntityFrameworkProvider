using System;
using System.ComponentModel.DataAnnotations;

namespace ModelExample
{
    //[ComplexType] //convert to class when I can use ComplexType - EF does not support struct
    public struct AuditTrail
    {
        //[Column("ModifiedTime")]
        public DateTime? ModifiedTime { get; set; }
        [NotMapped]
        public string ModifiedBy { get; set; }

        #region Equality

        public bool Equals(AuditTrail other)
        {
            return other.ModifiedTime.Equals(ModifiedTime) && Equals(other.ModifiedBy, ModifiedBy);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (obj.GetType() != typeof (AuditTrail)) return false;
            return Equals((AuditTrail) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (ModifiedTime.GetHashCode()*397) ^ (ModifiedBy != null ? ModifiedBy.GetHashCode() : 0);
            }
        }

        #endregion
    }
}