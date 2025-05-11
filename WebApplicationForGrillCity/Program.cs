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

// Добавляем сервисы CORS
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
        Description = "API для мобильного приложения GrillCity"
    });

    // Добавляем отображение HTTP-методов и описаний
    c.DocInclusionPredicate((docName, apiDesc) =>
    {
        apiDesc.TryGetMethodInfo(out var methodInfo);
        return true;
    });

    // Включаем аннотации (если нужно)
    c.EnableAnnotations();
});
builder.Services.AddDbContext<GrillcitynnContext>();

var app = builder.Build();

app.MapGet("/productTypes",
    [SwaggerOperation(
    Summary = "Типы товаров",
    Description = "Получение типов товаров для фильтрации каталога")]
(GrillcitynnContext db) =>
{
    return Results.Json(db.ProductTypes.ToList());
});


app.MapGet("/products",
    [SwaggerOperation(
    Summary = "Список товаров",
    Description = "Получение списка товаров для каталога")]
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
    Summary = "Продажи за период",
    Description = "Получение заказов за период в декстоп приложении")]
async (GrillcitynnContext db, DateTime startDate, DateTime endDate) =>
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

app.MapGet("/orderStatsByProvider",
    [SwaggerOperation(
    Summary = "Статистика выручки по поставщикам",
    Description = "Статистика выручки по поставщикам в декстоп приложении")]
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
            ProviderName = g.Key ?? "Неизвестный поставщик",
            TotalRevenue = g.Sum(o => o.FinalPrice)
        })
        .OrderByDescending(x => x.TotalRevenue)
        .ToList();

    return Results.Json(stats);
});

app.MapGet("/orders",
    [SwaggerOperation(
    Summary = "Список покупок",
    Description = "Выведение покупок в декстоп приложении")]
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
    Summary = "Создание продажи",
    Description = "Создание продажи в декстоп приложении")]
(
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

app.MapGet("/productss",
    [SwaggerOperation(
    Summary = "Товары",
    Description = "Выведение товаров для выпадающего списка")]
(GrillcitynnContext db) =>
{
    return Results.Json(db.Products.ToList());
});

app.MapGet("/discounts",
    [SwaggerOperation(
    Summary = "Скидка",
    Description = "Выведение скидки для выпадающего списка")] 
(GrillcitynnContext db) =>
{
    return Results.Json(db.Discounts.ToList());
});



app.MapPost("/updateProductStock",
    [SwaggerOperation(
    Summary = "Обновление количества товаров",
    Description = "Обновление количества товаров на складе магазина после их прихода")]
async (GrillcitynnContext db, int productId, int quantity) =>
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


app.MapGet("/getProductMovements",
    [SwaggerOperation(
    Summary = "Движение товаров",
    Description = "Получение данных о движении товаров для создания отчетов")]
async (GrillcitynnContext db) =>
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

app.MapGet("/providers",
    [SwaggerOperation(
    Summary = "Список поставщиков",
    Description = "Список поставщиков в декстоп приложении")]
(GrillcitynnContext db) =>
{
    return Results.Json(db.Providers.ToList());
});

app.MapPost("/login",
    [SwaggerOperation(
    Summary = "Авторизация клиента",
    Description = "Авторизация клиента в мобильном приложении")]
async (GrillcitynnContext db, string login, string password) =>
{
    // Поиск пользователя по логину и паролю
    var user = await db.Users.FirstOrDefaultAsync(u =>
        u.Userlogin == login && u.Userpassword == password);

    if (user == null)
    {
        return Results.Unauthorized(); // Неверный логин или пароль
    }

    return Results.Ok(new
    {
        Message = "Авторизация успешна.",
        UserId = user.Userid,
        FullName = $"{user.Sname} {user.Fname} {user.Patronumic}",
        PhoneNumber = user.Phonenumber
    });
});

app.MapPost("/CreateMobileOrder",
    [SwaggerOperation(
    Summary = "Создание заказа клиентом",
    Description = "Создание заказа клиентом в мобильном приложении")] 
(
    GrillcitynnContext db,
    [FromBody] CreateOrderDto dto
) =>
{
    // Извлекаем значения из DTO
    var clientId = dto.ClientId;
    var products = dto.Products;

    // Проверка существования клиента
    var client = db.Users.FirstOrDefault(c => c.Userid == clientId);
    if (client == null)
    {
        return Results.BadRequest("Клиент не найден.");
    }

    // Получаем список продуктов и проверяем их наличие и достаточное количество
    var productIds = products.Keys.ToList();
    var dbProducts = db.Products.Where(p => productIds.Contains(p.Id)).ToList();

    if (dbProducts.Count != products.Count)
    {
        var missingIds = productIds.Except(dbProducts.Select(p => p.Id));
        return Results.BadRequest($"Продукты с ID {string.Join(", ", missingIds)} не найдены.");
    }

    // Проверка наличия достаточного количества на складе
    foreach (var item in products)
    {
        var product = dbProducts.First(p => p.Id == item.Key);
        if (product.QuantityInStock < item.Value)
        {
            return Results.BadRequest($"Недостаточно товара {product.ProductName} на складе.");
        }
    }

    // Генерация кода для заказа
    var random = new Random();
    string code = random.Next(1000, 9999).ToString();

    // Создание заказа
    var order = new Myorder
    {
        Clientid = clientId,
        Dateoforder = DateTime.Now,
        Codefortakeproduct = code,
        Orderstatus = 1 // Новый заказ
    };

    db.Myorders.Add(order);
    db.SaveChanges(); // Сохраняем, чтобы получить OrderId

    double totalPrice = 0;
    var orderProducts = new List<object>();

    // Создание связи многие-ко-многим и обновление товаров
    foreach (var item in products)
    {
        var product = dbProducts.First(p => p.Id == item.Key);

        // Цена товара без скидки
        double price = product.Price;
        double productTotal = price * item.Value;
        totalPrice += productTotal;

        // Создание записи о товаре в заказе
        var orderProduct = new Orderproduct
        {
            Orderid = order.Orderid,
            Productsid = product.Id,
            Countinorder = item.Value
        };

        db.Orderproducts.Add(orderProduct);

        // Обновление количества товара на складе
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

    // Обновление общего итога заказа (сумма заказа)
    db.SaveChanges(); // Сохраняем изменения

    // Возвращаем успешный ответ с деталями заказа
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
    Summary = "Заказы клиента",
    Description = "Выведение заказов конкретного клиента в мобильном приложении")]
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
            Date = o.Dateoforder.ToString("dd.MM.yyyy"), // ?? безопасно для Kotlin UI
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
    Summary = "Статистика по продажам",
    Description = "Выведение статистики по продажам товаров в магазине")]
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
        .FirstOrDefault() ?? "—";

    var result = new
    {
        TotalOrders = totalOrders,
        TotalSales = totalSales,
        MostPopularProduct = mostPopularProduct
    };

    return Results.Json(result);
});

app.MapPost("/register", [SwaggerOperation(
    Summary = "Регистрация клиента",
    Description = "Регистрация нового клиента в мобильном приложении")]

    async (GrillcitynnContext db,
    string login,
    string password,
    string sname,
    string fname,
    string? patronumic,
    string phonenumber) =>
{
    // Проверка на пустые обязательные поля
    if (string.IsNullOrWhiteSpace(login) ||
        string.IsNullOrWhiteSpace(password) ||
        string.IsNullOrWhiteSpace(sname) ||
        string.IsNullOrWhiteSpace(fname) ||
        string.IsNullOrWhiteSpace(phonenumber))
    {
        return Results.BadRequest("Все обязательные поля должны быть заполнены.");
    }

    // Проверка длины пароля
    if (password.Length < 6)
    {
        return Results.BadRequest("Пароль должен содержать минимум 6 символов.");
    }

    // Проверка уникальности логина
    var loginExists = await db.Users.AnyAsync(u => u.Userlogin == login);
    if (loginExists)
    {
        return Results.Conflict("Пользователь с таким логином уже существует.");
    }

    // Проверка уникальности номера телефона
    var phoneExists = await db.Users.AnyAsync(u => u.Phonenumber == phonenumber);
    if (phoneExists)
    {
        return Results.Conflict("Пользователь с таким номером телефона уже зарегистрирован.");
    }

    // Создание нового пользователя
    var newUser = new User
    {
        Userlogin = login,
        Userpassword = password, // В реальном приложении пароль должен хешироваться!
        Sname = sname,
        Fname = fname,
        Patronumic = patronumic,
        Phonenumber = phonenumber
    };

    db.Users.Add(newUser);
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        Message = "Регистрация успешна.",
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

// Включаем CORS перед UseAuthorization()
app.UseCors("AllowAll");

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
