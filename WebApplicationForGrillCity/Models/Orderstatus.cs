using System;
using System.Collections.Generic;

namespace WebApplicationForGrillCity.Models;

public partial class Orderstatus
{
    public int Orderstatusid { get; set; }

    public string? Statusname { get; set; }

    public virtual ICollection<Myorder> Myorders { get; set; } = new List<Myorder>();
}
