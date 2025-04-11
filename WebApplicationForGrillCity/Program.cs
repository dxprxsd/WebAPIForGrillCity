using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebApplicationForGrillCity.Models;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<GrillcitynnContext>();

var app = builder.Build();

app.MapGet("/productTypes", (GrillcitynnContext db) =>
{
    return Results.Json(db.ProductTypes.ToList());
});


app.MapGet("/products", (GrillcitynnContext db, int? typeId) =>
{
    var query = db.Products.Include(p => p.ProductType).AsQueryable();

    if (typeId.HasValue && typeId.Value != 0)
    {
        query = query.Where(p => p.ProductTypeId == typeId.Value);
    }

    var products = query
        .ToList()
        .Select(p => new
        {
            p.Id,
            p.ProductName,
            p.ProductTypeId,
            p.ProviderId,
            p.Photo,
            p.QuantityInStock,
            p.Price,
            ProductType = p.ProductType == null
                ? null
                : new { p.ProductType.Id, p.ProductType.TypeName }
        }).ToList();

    return Results.Json(products, new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.IgnoreCycles });
});

app.MapPost("/orders", async (GrillcitynnContext db, Order order) =>
{
    db.Orders.Add(order);
    await db.SaveChangesAsync();
    return Results.Ok(order);
});

// Получение заказов за период
app.MapGet("/ordersByDate", async (GrillcitynnContext db, DateTime startDate, DateTime endDate) =>
{
    var start = DateOnly.FromDateTime(startDate);
    var end = DateOnly.FromDateTime(endDate.AddDays(1)); // Включительно

    var orders = await db.Orders
        .Include(o => o.Product)
            .ThenInclude(p => p.Provider)
        .Include(o => o.Discount)
        .Where(order => order.DateOfOrder >= start && order.DateOfOrder <= end)
        .ToListAsync();

    return Results.Json(orders, new JsonSerializerOptions
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = true
    });
});

// Статистика выручки по поставщикам
app.MapGet("/orderStatsByProvider", async (GrillcitynnContext db, DateTime startDate, DateTime endDate) =>
{
    var start = DateOnly.FromDateTime(startDate);
    var end = DateOnly.FromDateTime(endDate.AddDays(1));

    var filteredOrders = await db.Orders
        .Include(o => o.Product)
            .ThenInclude(p => p.Provider)
        .Where(o => o.DateOfOrder >= start && o.DateOfOrder <= end && o.Product.Provider != null)
        .ToListAsync();

    var stats = filteredOrders
        .GroupBy(o => o.Product.Provider.ProviderName)
        .Select(g => new
        {
            ProviderName = g.Key ?? "Неизвестный поставщик",
            TotalRevenue = g.Sum(o => o.FinalPrice)
        })
        .OrderByDescending(x => x.TotalRevenue)
        .ToList();

    return Results.Json(stats);
});

app.MapGet("/orders", async (GrillcitynnContext db) =>
{
    var orders = await db.Orders
        .Include(o => o.Product)
            .ThenInclude(p => p.Provider)
        .Include(o => o.Discount)
        .ToListAsync();

    return Results.Json(orders, new JsonSerializerOptions
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = true
    });
});


app.MapPost("/CreateOrder", (
    GrillcitynnContext db,
    int productId,
    int? discountId,
    int quantity
) =>
{
    // Проверка, существует ли товар
    var product = db.Products.FirstOrDefault(p => p.Id == productId);
    if (product == null)
    {
        return Results.BadRequest("Продукт не найден.");
    }

    if (product.QuantityInStock < quantity)
    {
        return Results.BadRequest("Недостаточно товара на складе.");
    }

    // Проверка скидки
    Discount? discount = null;
    if (discountId.HasValue)
    {
        discount = db.Discounts.FirstOrDefault(d => d.Id == discountId.Value);
        if (discount == null)
        {
            return Results.BadRequest("Скидка не найдена.");
        }
    }

    // Расчет финальной цены
    double basePrice = product.Price;
    if (discount != null)
    {
        basePrice *= (1 - (discount.DiscountPercent / 100.0));
    }

    double finalPrice = basePrice * quantity;

    // Создание нового заказа
    var order = new Order
    {
        ProductId = product.Id,
        DiscountId = discount?.Id,
        DateOfOrder = DateOnly.FromDateTime(DateTime.Now),
        FinalPrice = finalPrice
    };

    db.Orders.Add(order);

    // Обновление остатка товара
    product.QuantityInStock -= quantity;
    db.Products.Update(product);

    db.SaveChanges();

    return Results.Ok(new
    {
        Message = "Заказ успешно создан.",
        OrderId = order.Id,
        Product = product.ProductName,
        Quantity = quantity,
        FinalPrice = finalPrice
    });
});


// Add these endpoints to your Program.cs or wherever you configure your API

app.MapGet("/productss", (GrillcitynnContext db) =>
{
    return Results.Json(db.Products.ToList());
});

app.MapGet("/discounts", (GrillcitynnContext db) =>
{
    return Results.Json(db.Discounts.ToList());
});



app.MapPost("/updateProductStock", async (GrillcitynnContext db, int productId, int quantity) =>
{
    // Проверка, существует ли товар
    var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId);
    if (product == null)
    {
        return Results.BadRequest("Продукт не найден.");
    }

    if (quantity <= 0)
    {
        return Results.BadRequest("Количество должно быть больше нуля.");
    }

    // Обновление остатка товара
    product.QuantityInStock += quantity;

    db.Products.Update(product);
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        Message = "Количество товара успешно обновлено.",
        Product = product.ProductName,
        QuantityInStock = product.QuantityInStock
    });
});


app.MapGet("/getProductMovements", async (GrillcitynnContext db) =>
{
    var movements = await db.ProductMovements
        .Include(m => m.Product)
        .Select(m => new
        {
            m.ProductId,  // Добавляем ProductId
            m.Product.ProductName,
            m.Quantity,
            m.MovementType,
            m.MovementDate
        })
        .ToListAsync();

    return Results.Json(movements);
});




// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
