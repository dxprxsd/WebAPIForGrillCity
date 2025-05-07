using System;
using System.Collections.Generic;

namespace WebApplicationForGrillCity.Models;

public partial class Product
{
    public int Id { get; set; }

    public string ProductName { get; set; } = null!;

    public int? ProductTypeId { get; set; }

    public int? ProviderId { get; set; }

    public string? Photo { get; set; }

    public int? QuantityInStock { get; set; }

    public double Price { get; set; }

    public virtual ICollection<Orderproduct> Orderproducts { get; set; } = new List<Orderproduct>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<ProductMovement> ProductMovements { get; set; } = new List<ProductMovement>();

    public virtual ProductType? ProductType { get; set; }

    public virtual Provider? Provider { get; set; }
}
