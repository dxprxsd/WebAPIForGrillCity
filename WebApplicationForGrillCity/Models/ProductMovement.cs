using System;
using System.Collections.Generic;

namespace WebApplicationForGrillCity.Models;

public partial class ProductMovement
{
    public int Id { get; set; }

    public int? ProductId { get; set; }

    public string? MovementType { get; set; }

    public int Quantity { get; set; }

    public DateOnly? MovementDate { get; set; }

    public virtual Product? Product { get; set; }
}
