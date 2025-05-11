using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebApplicationForGrillCity.Models;
using Microsoft.OpenApi.Models;
using System.Reflection;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.Annotations;

var builder = WebApplication.CreateBuilder(args);

// ��������� ������� CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);

    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GrillCity API",
        Version = "v1",
        Description = "API ��� ���������� ���������� GrillCity"
    });

    // ��������� ����������� HTTP-������� � ��������
    c.DocInclusionPredicate((docName, apiDesc) =>
    {
        apiDesc.TryGetMethodInfo(out var methodInfo);
        return true;
    });

    // �������� ��������� (���� �����)
    c.EnableAnnotations();
});
builder.Services.AddDbContext<GrillcitynnContext>();

var app = builder.Build();

app.MapGet("/productTypes",
    [SwaggerOperation(
    Summary = "���� �������",
    Description = "��������� ����� ������� ��� ���������� ��������")]
(GrillcitynnContext db) =>
{
    return Results.Json(db.ProductTypes.ToList());
});


app.MapGet("/products",
    [SwaggerOperation(
    Summary = "������ �������",
    Description = "��������� ������ ������� ��� ��������")]
(GrillcitynnContext db, int? typeId) =>
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

app.MapGet("/ordersByDate",
    [SwaggerOperation(
    Summary = "������� �� ������",
    Description = "��������� ������� �� ������ � ������� ����������")]
async (GrillcitynnContext db, DateTime startDate, DateTime endDate) =>
{
    var start = DateOnly.FromDateTime(startDate);
    var end = DateOnly.FromDateTime(endDate.AddDays(1)); // ������������

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

app.MapGet("/orderStatsByProvider",
    [SwaggerOperation(
    Summary = "���������� ������� �� �����������",
    Description = "���������� ������� �� ����������� � ������� ����������")]
async (GrillcitynnContext db, DateTime startDate, DateTime endDate) =>
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
            ProviderName = g.Key ?? "����������� ���������",
            TotalRevenue = g.Sum(o => o.FinalPrice)
        })
        .OrderByDescending(x => x.TotalRevenue)
        .ToList();

    return Results.Json(stats);
});

app.MapGet("/orders",
    [SwaggerOperation(
    Summary = "������ �������",
    Description = "��������� ������� � ������� ����������")]
async (GrillcitynnContext db) =>
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


app.MapPost("/CreateOrder",
    [SwaggerOperation(
    Summary = "�������� �������",
    Description = "�������� ������� � ������� ����������")]
(
    GrillcitynnContext db,
    int productId,
    int? discountId,
    int quantity
) =>
{
    // ��������, ���������� �� �����
    var product = db.Products.FirstOrDefault(p => p.Id == productId);
    if (product == null)
    {
        return Results.BadRequest("������� �� ������.");
    }

    if (product.QuantityInStock < quantity)
    {
        return Results.BadRequest("������������ ������ �� ������.");
    }

    // �������� ������
    Discount? discount = null;
    if (discountId.HasValue)
    {
        discount = db.Discounts.FirstOrDefault(d => d.Id == discountId.Value);
        if (discount == null)
        {
            return Results.BadRequest("������ �� �������.");
        }
    }

    // ������ ��������� ����
    double basePrice = product.Price;
    if (discount != null)
    {
        basePrice *= (1 - (discount.DiscountPercent / 100.0));
    }

    double finalPrice = basePrice * quantity;

    // �������� ������ ������
    var order = new Order
    {
        ProductId = product.Id,
        DiscountId = discount?.Id,
        DateOfOrder = DateOnly.FromDateTime(DateTime.Now),
        FinalPrice = finalPrice
    };

    db.Orders.Add(order);

    // ���������� ������� ������
    product.QuantityInStock -= quantity;
    db.Products.Update(product);

    db.SaveChanges();

    return Results.Ok(new
    {
        Message = "����� ������� ������.",
        OrderId = order.Id,
        Product = product.ProductName,
        Quantity = quantity,
        FinalPrice = finalPrice
    });
});


// Add these endpoints to your Program.cs or wherever you configure your API

app.MapGet("/productss",
    [SwaggerOperation(
    Summary = "������",
    Description = "��������� ������� ��� ����������� ������")]
(GrillcitynnContext db) =>
{
    return Results.Json(db.Products.ToList());
});

app.MapGet("/discounts",
    [SwaggerOperation(
    Summary = "������",
    Description = "��������� ������ ��� ����������� ������")] 
(GrillcitynnContext db) =>
{
    return Results.Json(db.Discounts.ToList());
});



app.MapPost("/updateProductStock",
    [SwaggerOperation(
    Summary = "���������� ���������� �������",
    Description = "���������� ���������� ������� �� ������ �������� ����� �� �������")]
async (GrillcitynnContext db, int productId, int quantity) =>
{
    // ��������, ���������� �� �����
    var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId);
    if (product == null)
    {
        return Results.BadRequest("������� �� ������.");
    }

    if (quantity <= 0)
    {
        return Results.BadRequest("���������� ������ ���� ������ ����.");
    }

    // ���������� ������� ������
    product.QuantityInStock += quantity;

    db.Products.Update(product);
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        Message = "���������� ������ ������� ���������.",
        Product = product.ProductName,
        QuantityInStock = product.QuantityInStock
    });
});


app.MapGet("/getProductMovements",
    [SwaggerOperation(
    Summary = "�������� �������",
    Description = "��������� ������ � �������� ������� ��� �������� �������")]
async (GrillcitynnContext db) =>
{
    var movements = await db.ProductMovements
        .Include(m => m.Product)
        .Select(m => new
        {
            m.ProductId,  // ��������� ProductId
            m.Product.ProductName,
            m.Quantity,
            m.MovementType,
            m.MovementDate
        })
        .ToListAsync();

    return Results.Json(movements);
});

app.MapGet("/providers",
    [SwaggerOperation(
    Summary = "������ �����������",
    Description = "������ ����������� � ������� ����������")]
(GrillcitynnContext db) =>
{
    return Results.Json(db.Providers.ToList());
});

app.MapPost("/login",
    [SwaggerOperation(
    Summary = "����������� �������",
    Description = "����������� ������� � ��������� ����������")]
async (GrillcitynnContext db, string login, string password) =>
{
    // ����� ������������ �� ������ � ������
    var user = await db.Users.FirstOrDefaultAsync(u =>
        u.Userlogin == login && u.Userpassword == password);

    if (user == null)
    {
        return Results.Unauthorized(); // �������� ����� ��� ������
    }

    return Results.Ok(new
    {
        Message = "����������� �������.",
        UserId = user.Userid,
        FullName = $"{user.Sname} {user.Fname} {user.Patronumic}",
        PhoneNumber = user.Phonenumber
    });
});

app.MapPost("/CreateMobileOrder",
    [SwaggerOperation(
    Summary = "�������� ������ ��������",
    Description = "�������� ������ �������� � ��������� ����������")] 
(
    GrillcitynnContext db,
    [FromBody] CreateOrderDto dto
) =>
{
    // ��������� �������� �� DTO
    var clientId = dto.ClientId;
    var products = dto.Products;

    // �������� ������������� �������
    var client = db.Users.FirstOrDefault(c => c.Userid == clientId);
    if (client == null)
    {
        return Results.BadRequest("������ �� ������.");
    }

    // �������� ������ ��������� � ��������� �� ������� � ����������� ����������
    var productIds = products.Keys.ToList();
    var dbProducts = db.Products.Where(p => productIds.Contains(p.Id)).ToList();

    if (dbProducts.Count != products.Count)
    {
        var missingIds = productIds.Except(dbProducts.Select(p => p.Id));
        return Results.BadRequest($"�������� � ID {string.Join(", ", missingIds)} �� �������.");
    }

    // �������� ������� ������������ ���������� �� ������
    foreach (var item in products)
    {
        var product = dbProducts.First(p => p.Id == item.Key);
        if (product.QuantityInStock < item.Value)
        {
            return Results.BadRequest($"������������ ������ {product.ProductName} �� ������.");
        }
    }

    // ��������� ���� ��� ������
    var random = new Random();
    string code = random.Next(1000, 9999).ToString();

    // �������� ������
    var order = new Myorder
    {
        Clientid = clientId,
        Dateoforder = DateTime.Now,
        Codefortakeproduct = code,
        Orderstatus = 1 // ����� �����
    };

    db.Myorders.Add(order);
    db.SaveChanges(); // ���������, ����� �������� OrderId

    double totalPrice = 0;
    var orderProducts = new List<object>();

    // �������� ����� ������-��-������ � ���������� �������
    foreach (var item in products)
    {
        var product = dbProducts.First(p => p.Id == item.Key);

        // ���� ������ ��� ������
        double price = product.Price;
        double productTotal = price * item.Value;
        totalPrice += productTotal;

        // �������� ������ � ������ � ������
        var orderProduct = new Orderproduct
        {
            Orderid = order.Orderid,
            Productsid = product.Id,
            Countinorder = item.Value
        };

        db.Orderproducts.Add(orderProduct);

        // ���������� ���������� ������ �� ������
        product.QuantityInStock -= item.Value;
        db.Products.Update(product);

        orderProducts.Add(new
        {
            ProductId = product.Id,
            ProductName = product.ProductName,
            Quantity = item.Value,
            PricePerItem = price,
            Total = productTotal
        });
    }

    // ���������� ������ ����� ������ (����� ������)
    db.SaveChanges(); // ��������� ���������

    // ���������� �������� ����� � �������� ������
    return Results.Ok(new
    {
        OrderId = order.Orderid,
        CodeForTakeProduct = order.Codefortakeproduct,
        TotalPrice = totalPrice,
        Products = orderProducts
    });
});


app.MapGet("/ordersByUser",
    [SwaggerOperation(
    Summary = "������ �������",
    Description = "��������� ������� ����������� ������� � ��������� ����������")]
    async (GrillcitynnContext db, int userId) =>
    {
    var orders = await db.Myorders
        .Where(o => o.Clientid == userId)
        .Include(o => o.OrderstatusNavigation)
        .Include(o => o.Orderproducts)
            .ThenInclude(op => op.Products)
        .Select(o => new
        {
            OrderId = o.Orderid,
            Date = o.Dateoforder.ToString("dd.MM.yyyy"), // ?? ��������� ��� Kotlin UI
            Code = o.Codefortakeproduct,
            Status = o.OrderstatusNavigation.Statusname,
            Products = o.Orderproducts.Select(op => new
            {
                ProductId = op.Productsid,
                ProductName = op.Products.ProductName,
                Quantity = op.Countinorder
            })
        })
        .ToListAsync();

    return Results.Ok(orders);
    }
);

app.MapGet("/statistics",
    [SwaggerOperation(
    Summary = "���������� �� ��������",
    Description = "��������� ���������� �� �������� ������� � ��������")]
(GrillcitynnContext db) =>
{
    var orders = db.Orders
        .Include(o => o.Product)
        .ThenInclude(p => p.Provider)
        .ToList();

    var totalOrders = orders.Count;
    var totalSales = orders.Sum(o => o.FinalPrice);

    var mostPopularProduct = orders
        .Where(o => o.Product != null)
        .GroupBy(o => o.Product!.ProductName)
        .OrderByDescending(g => g.Count())
        .Select(g => g.Key)
        .FirstOrDefault() ?? "�";

    var result = new
    {
        TotalOrders = totalOrders,
        TotalSales = totalSales,
        MostPopularProduct = mostPopularProduct
    };

    return Results.Json(result);
});

app.MapPost("/register", [SwaggerOperation(
    Summary = "����������� �������",
    Description = "����������� ������ ������� � ��������� ����������")]

    async (GrillcitynnContext db,
    string login,
    string password,
    string sname,
    string fname,
    string? patronumic,
    string phonenumber) =>
{
    // �������� �� ������ ������������ ����
    if (string.IsNullOrWhiteSpace(login) ||
        string.IsNullOrWhiteSpace(password) ||
        string.IsNullOrWhiteSpace(sname) ||
        string.IsNullOrWhiteSpace(fname) ||
        string.IsNullOrWhiteSpace(phonenumber))
    {
        return Results.BadRequest("��� ������������ ���� ������ ���� ���������.");
    }

    // �������� ����� ������
    if (password.Length < 6)
    {
        return Results.BadRequest("������ ������ ��������� ������� 6 ��������.");
    }

    // �������� ������������ ������
    var loginExists = await db.Users.AnyAsync(u => u.Userlogin == login);
    if (loginExists)
    {
        return Results.Conflict("������������ � ����� ������� ��� ����������.");
    }

    // �������� ������������ ������ ��������
    var phoneExists = await db.Users.AnyAsync(u => u.Phonenumber == phonenumber);
    if (phoneExists)
    {
        return Results.Conflict("������������ � ����� ������� �������� ��� ���������������.");
    }

    // �������� ������ ������������
    var newUser = new User
    {
        Userlogin = login,
        Userpassword = password, // � �������� ���������� ������ ������ ������������!
        Sname = sname,
        Fname = fname,
        Patronumic = patronumic,
        Phonenumber = phonenumber
    };

    db.Users.Add(newUser);
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        Message = "����������� �������.",
        UserId = newUser.Userid,
        FullName = $"{newUser.Sname} {newUser.Fname} {newUser.Patronumic}",
        PhoneNumber = newUser.Phonenumber
    });
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// �������� CORS ����� UseAuthorization()
app.UseCors("AllowAll");

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
