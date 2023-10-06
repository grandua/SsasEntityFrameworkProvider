using System;

namespace ModelExample
{
    public struct DocumentId
    {
        public DateTime DocumentDate { get; set; }
        public string DocumentNumber { get; set; }
        public override string ToString()
        { //TODO: should depend on a document culture really
            return string.Format("#{0} @{1}", DocumentNumber, DocumentDate);
        }

#region Equality

		public bool Equals(DocumentId other)
        {
            return other.DocumentDate.Equals(DocumentDate) && Equals(other.DocumentNumber, DocumentNumber);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (obj.GetType() != typeof (DocumentId)) return false;
            return Equals((DocumentId) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (DocumentDate.GetHashCode()*397) ^ (DocumentNumber != null ? DocumentNumber.GetHashCode() : 0);
            }
        }

#endregion    
    
    }
}