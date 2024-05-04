
using System;
using System.Collections.Generic;

namespace SHARED.Models
{
    public class Order
    {
        public int Id { get; set; }
        public int OrderNumber { get; set; }
        public int ClientId { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime DeliveryDate { get; set; }
        public List<OrderDetail> Details { get; set; }
    }
}
