using System;
using System.Collections.Generic;

namespace WebApplicationForGrillCity.Models;

public partial class Provider
{
    public int Id { get; set; }

    public string? ProviderName { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
