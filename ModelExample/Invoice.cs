using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModelExample
{
    public class Invoice : LineItemOrder
    {
        /// <summary>
        /// Optional
        /// </summary>
        public LineItemOrder PurchaseOrder { get; set; }
    }
}
