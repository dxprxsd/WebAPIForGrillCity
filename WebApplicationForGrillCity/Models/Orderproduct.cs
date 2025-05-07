using System;
using System.Collections.Generic;

namespace WebApplicationForGrillCity.Models;

public partial class Orderproduct
{
    public int Orderid { get; set; }

    public int Productsid { get; set; }

    public int Countinorder { get; set; }

    public virtual Myorder Order { get; set; } = null!;

    public virtual Product Products { get; set; } = null!;
}
