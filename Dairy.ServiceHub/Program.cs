using Dairy.Context;
using Dairy.DTO;
using Dairy.DMO;
using Common.Library.Models;
using Common.Library.DTOs;
using Common.Library.Data;
using Common.Library.Extensions;
using Common.Library.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System;
using System.Linq;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);

// DB Config
var connectionString = builder.Configuration["MongoDb:ConnectionString"] ?? "mongodb://localhost:27018";
var databaseName = builder.Configuration["MongoDb:DatabaseName"] ?? "DairyDB";

Console.WriteLine("====================================================");
Console.WriteLine($"[STARTUP] Using MongoDB Connection: {connectionString}");
Console.WriteLine($"[STARTUP] Using Database Name: {databaseName}");
Console.WriteLine("====================================================");

builder.Services.AddSingleton<DairyRepository>(sp => new DairyRepository(connectionString, databaseName));
builder.Services.AddSingleton<UserRepository>(sp => new UserRepository(connectionString, databaseName));

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "ThisIsAVerySecretKeyForJwtAuthenticationWhichNeedsToBeLongEnough";
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddCommonJwtAuthentication(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev",
        builder => builder.WithOrigins("http://localhost:4200")
                          .AllowAnyMethod()
                          .AllowAnyHeader());
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("AllowAngularDev");

app.UseAuthentication();
app.UseAuthorization();

// Login Endpoint
app.MapPost("/api/auth/login", async (LoginRequest request, UserRepository userRepo) =>
{
    var user = await userRepo.GetUserAsync(request.Username, request.Password);
    if (user == null) return Results.Unauthorized();

    var tokenHandler = new JwtSecurityTokenHandler();
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        }),
        Expires = DateTime.UtcNow.AddHours(1),
        Issuer = builder.Configuration["Jwt:Issuer"] ?? "Dairy.ServiceHub",
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
    };

    var token = tokenHandler.CreateToken(tokenDescriptor);
    return Results.Ok(new AuthResponse { Token = tokenHandler.WriteToken(token), Role = user.Role, Username = user.Username });
});

// Get Products (Accessible by both Admin and User, returning role-based fields)
app.MapGet("/api/dairy/products", async (DairyRepository repository, ClaimsPrincipal user) =>
{
    var products = await repository.GetAllProductsAsync();
    bool isAdmin = user.IsInRole("Admin") ||
                   user.HasClaim(c => (c.Type == ClaimTypes.Role || c.Type.ToLower() == "role") &&
                                      c.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase));

    if (isAdmin)
    {
        var response = products.Select(p => new DairyProductAdminResponse
        {
            ProductId = p.Id.ToString(),
            Name = p.Name ?? "Unknown Product",
            FatContent = p.FatContentPercentage,
            TemperatureRequired = string.IsNullOrEmpty(p.StorageTemperatureRange) ? "2°C - 4°C" : p.StorageTemperatureRange,
            StockQuantity = p.StockQuantity,
            IsFresh = (DateTime.UtcNow - p.PasteurizationDate).TotalDays <= 14
        }).ToList();
        return Results.Ok(response);
    }
    else
    {
        var response = products.Select(p => new DairyProductResponse
        {
            ProductId = p.Id.ToString(),
            Name = p.Name ?? "Unknown Product",
            FatContent = p.FatContentPercentage,
            IsFresh = (DateTime.UtcNow - p.PasteurizationDate).TotalDays <= 14
        }).ToList();
        return Results.Ok(response);
    }
}).AllowAnonymous();

// Add Product (Admin Only)
app.MapPost("/api/dairy/products", async (DairyProductRequest request, DairyRepository repository) =>
{
    var existingProducts = await repository.GetAllProductsAsync();
    var existing = existingProducts.FirstOrDefault(p => !string.IsNullOrEmpty(p.Name) && p.Name.Trim().Equals(request.Name?.Trim(), StringComparison.OrdinalIgnoreCase));

    if (existing != null)
    {
        existing.FatContentPercentage = request.FatContentPercentage;
        existing.StorageTemperatureRange = request.StorageTemperatureRange;
        existing.StockQuantity = request.StockQuantity;
        await repository.UpdateProductAsync(existing.Id.ToString(), existing);

        var commonUrl = builder.Configuration["ServiceUrls:CommonService"];
        _ = ProductSyncClient.SyncProductToCommonAsync(new ProductSyncPayload
        {
            OriginalId = existing.Id.ToString(),
            Name = existing.Name,
            Category = "Dairy",
            Price = (decimal)(existing.FatContentPercentage * 2.5),
            StockQuantity = existing.StockQuantity,
            SourceService = "Dairy",
            ActionType = "Update"
        }, commonUrl);

        return Results.Ok(existing);
    }

    var product = new DairyProduct
    {
        Name = request.Name?.Trim() ?? "",
        FatContentPercentage = request.FatContentPercentage,
        StorageTemperatureRange = request.StorageTemperatureRange,
        StockQuantity = request.StockQuantity,
        PasteurizationDate = DateTime.UtcNow
    };
    await repository.AddProductAsync(product);

    var commonServiceUrl = builder.Configuration["ServiceUrls:CommonService"];

    // Live Sync to Common-Service Master Inventory
    _ = ProductSyncClient.SyncProductToCommonAsync(new ProductSyncPayload
    {
        OriginalId = product.Id.ToString(),
        Name = product.Name,
        Category = "Dairy",
        Price = (decimal)(product.FatContentPercentage * 2.5),
        StockQuantity = product.StockQuantity,
        SourceService = "Dairy",
        ActionType = "Add"
    }, commonServiceUrl);

    return Results.Ok(product);
}).RequireAuthorization("AdminOnly");

// Update Product (Admin Only)
app.MapPut("/api/dairy/products/{id}", async (string id, DairyProductRequest request, DairyRepository repository) =>
{
    var commonServiceUrl = builder.Configuration["ServiceUrls:CommonService"];
    var existing = await repository.GetProductAsync(id);
    if (existing == null)
    {
        existing = new DairyProduct
        {
            Name = request.Name,
            FatContentPercentage = request.FatContentPercentage,
            StorageTemperatureRange = request.StorageTemperatureRange,
            StockQuantity = request.StockQuantity,
            PasteurizationDate = DateTime.UtcNow
        };
        await repository.AddProductAsync(existing);
    }
    else
    {
        existing.Name = request.Name;
        existing.FatContentPercentage = request.FatContentPercentage;
        existing.StorageTemperatureRange = request.StorageTemperatureRange;
        existing.StockQuantity = request.StockQuantity;
        await repository.UpdateProductAsync(id, existing);
    }

    // Live Sync Update to Common-Service Master Inventory
    _ = ProductSyncClient.SyncProductToCommonAsync(new ProductSyncPayload
    {
        OriginalId = id,
        Name = existing.Name,
        Category = "Dairy",
        Price = (decimal)(existing.FatContentPercentage * 2.5),
        StockQuantity = existing.StockQuantity,
        SourceService = "Dairy",
        ActionType = "Update"
    }, commonServiceUrl);

    return Results.Ok(existing);
}).RequireAuthorization("AdminOnly");

// Delete Product (Admin Only)
app.MapDelete("/api/dairy/products/{id}", async (string id, DairyRepository repository) =>
{
    var commonServiceUrl = builder.Configuration["ServiceUrls:CommonService"];
    var existing = await repository.GetProductAsync(id);
    if (existing == null) return Results.NotFound();

    await repository.DeleteProductAsync(id);

    // Live Sync Delete to Common-Service Master Inventory
    _ = ProductSyncClient.SyncProductToCommonAsync(new ProductSyncPayload
    {
        OriginalId = id,
        Name = existing.Name,
        Category = "Dairy",
        SourceService = "Dairy",
        ActionType = "Delete"
    }, commonServiceUrl);

    return Results.Ok(new { message = "Product deleted successfully" });
}).RequireAuthorization("AdminOnly");

app.Run();
