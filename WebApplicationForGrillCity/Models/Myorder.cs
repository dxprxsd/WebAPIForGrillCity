using System;
using System.Collections.Generic;

namespace WebApplicationForGrillCity.Models;

public partial class Myorder
{
    public int Orderid { get; set; }

    public DateTime Dateoforder { get; set; }

    public int? Clientid { get; set; }

    public string Codefortakeproduct { get; set; } = null!;

    public int Orderstatus { get; set; }

    public virtual User? Client { get; set; }

    public virtual ICollection<Orderproduct> Orderproducts { get; set; } = new List<Orderproduct>();

    public virtual Orderstatus OrderstatusNavigation { get; set; } = null!;
}
