using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModelExample.Repository
{
    /// <remarks>
    /// I intentionally do not use IQueryable 
    /// to make sure code works with any IEnumerable 
    /// if it is possible to cast to IQueryable
    /// </remarks>
    public class ModelExampleRepository //: Session
    {
        public IList<Product> Products { get; set; }
        public IList<Agreement> Agreements { get; set; }
        public IList<LineItemOrder> PurchaseOrders { get; set; }
        public IList<Invoice> Invoices { get; set; }
    }
}
