using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModelExample
{
    /// <summary>
    /// for Purchase Order, base for Fulfillment Order / Invoice
    /// </summary>
    //[AggregateRoot]
    //[Dimension]
    public class LineItemOrder : ModelBase
    {
        public DocumentId DocumentId { get; set; }
        public IList<LineItem> LineItems { get; set; }

        public Party CounterParty { get; set; }
        /// <summary>
        /// Optional
        /// </summary>
        public Agreement Agreement { get; set; }
    }

    //[Dimension]
    public class Party : ModelBase
    {
        public string LegalName { get; set; }
        public Address LegalAddress { get; set; }
    }

    public class Address
    {
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string City { get; set; }
        //[Dimension]
        public string State { get; set; }
        public string ZipCode { get; set; }
        //[Dimension]
        public string Country { get; set; }
    }
}
