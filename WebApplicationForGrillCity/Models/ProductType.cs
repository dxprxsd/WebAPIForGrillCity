using System;
using System.Collections.Generic;

namespace WebApplicationForGrillCity.Models;

public partial class ProductType
{
    public int Id { get; set; }

    public string? TypeName { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
