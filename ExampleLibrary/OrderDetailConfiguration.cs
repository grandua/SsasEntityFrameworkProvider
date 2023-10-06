using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NorthwindEFModel
{
    public class OrderDetailConfiguration
        : System.Data.Entity.ModelConfiguration.EntityTypeConfiguration<OrderDetail>
    {
        public OrderDetailConfiguration()
        {
            HasKey
                (
                    od => new
                    {
                        od.OrderID,
                        od.ProductID
                    }
                );
        }
    }
}
