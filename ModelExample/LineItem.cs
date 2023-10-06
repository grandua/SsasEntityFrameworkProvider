
namespace ModelExample
{
    public class LineItem : ModelBase
    {
        public ProductSku Product { get; set; } //TODO: do I need productId?

        public double Quantity { get; set; } //TODO: play with Quantity class
        /// <summary>
        /// Per unit
        /// </summary>
        public double Price { get; set; } //TODO: play with Money
        /// <summary>
        /// = Price * Quantity 
        /// Before discounts
        /// </summary>
        public double TotalPrice
        { //TODO: should I use Math.Round() ? Can it be calculated?
            get { return Price*Quantity; }
        }
        /// <summary>
        /// TotalPrice after discounts
        /// </summary>
        public double ExtendedPrice { get; set; } //TODO: play with Money
        /// <summary>
        /// For both Sales Tax and Value Added Tax, likely only one can be used at a time
        /// </summary>
        public double Tax { get; set; }
        /// <summary>
        /// Line total
        /// = ExtendedPrice + Tax, 
        /// but it may include a rounding error, can it be a calculated field?
        /// </summary>
        public double Total
        {
            get { return ExtendedPrice + Tax; }
        }
    }
}