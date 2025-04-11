using System;
using System.Collections.Generic;

namespace WebApplicationForGrillCity.Models;

public partial class Order
{
    public int Id { get; set; }

    public int? ProductId { get; set; }

    public int? DiscountId { get; set; }

    public DateOnly? DateOfOrder { get; set; }

    public double FinalPrice { get; set; }

    public virtual Discount? Discount { get; set; }

    public virtual Product? Product { get; set; }
}
