using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace ModelExample
{
    public class Product : ModelBase
    {
        [NotMapped]
        public string CustomsTariffCode { get; set; }
        public string ProductName { get; set; }
        /// <summary>
        /// Unit of measure for inventory accounting
        /// Used across all documents for inventory accounting regardless of a real packaging unit of measure
        /// </summary>
        public string UnitOfMeasure { get; set; }
    }

    public class ProductSku : Product
    {
        public string SkuNumber { get; set; }
        public Product Product { get; set; }
        /// <summary>
        /// Real unit of measure used in a specific document line item
        /// </summary>
        public string PackagingUnit { get; set; }
    }
}
