using System.Text.Json;
using ABCPharmacy.Models;
using ABCPharmacy.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddSingleton<PharmacyStore>();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/medicines", async (string? search, PharmacyStore store) =>
    Results.Ok(await store.GetMedicinesAsync(search)));

app.MapPost("/api/medicines", async (MedicineInput input, PharmacyStore store) =>
{
    var errors = Validate(input);
    if (errors.Count > 0) return Results.ValidationProblem(errors);
    return Results.Created($"/api/medicines/{input.Name}", await store.AddMedicineAsync(input));
});

app.MapGet("/api/sales", async (PharmacyStore store) => Results.Ok(await store.GetSalesAsync()));

app.MapPost("/api/medicines/{id:guid}/sales", async (Guid id, SaleInput sale, PharmacyStore store) =>
{
    if (sale.Quantity <= 0)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["quantity"] = ["Quantity sold must be at least 1."] });

    var result = await store.RecordSaleAsync(id, sale.Quantity);
    return result.Kind switch
    {
        SaleResultKind.NotFound => Results.NotFound(new { message = "Medicine not found." }),
        SaleResultKind.InsufficientStock => Results.BadRequest(new { message = "Not enough stock for this sale." }),
        _ => Results.Ok(result.Sale)
    };
});

app.MapFallbackToFile("index.html");
app.Run();

static Dictionary<string, string[]> Validate(MedicineInput item)
{
    var errors = new Dictionary<string, string[]>();
    if (string.IsNullOrWhiteSpace(item.Name)) errors["name"] = ["Medicine name is required."];
    if (string.IsNullOrWhiteSpace(item.Brand)) errors["brand"] = ["Brand is required."];
    if (item.ExpiryDate.Date < DateTime.Today) errors["expiryDate"] = ["Expiry date cannot be in the past."];
    if (item.Quantity < 0) errors["quantity"] = ["Quantity cannot be negative."];
    if (item.Price < 0) errors["price"] = ["Price cannot be negative."];
    return errors;
}
