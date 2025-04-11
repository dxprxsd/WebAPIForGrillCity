using System;
using System.Collections.Generic;

namespace WebApplicationForGrillCity.Models;

public partial class Discount
{
    public int Id { get; set; }

    public double DiscountPercent { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
