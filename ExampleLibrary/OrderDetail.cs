using AgileDesign.SsasEntityFrameworkProvider.Attributes;

namespace NorthwindEFModel
{
    [MeasureGroup]
    public class OrderDetail
    {
        [DimensionProperty("Orders")]
        public int OrderID { get; set; }
        [DimensionProperty("Products")]
        public int ProductID { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public float Discount { get; set; }

        public virtual Order Order { get; set; }
    }
}