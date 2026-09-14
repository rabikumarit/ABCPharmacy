using System.Text.Json;
using ABCPharmacy.Models;

namespace ABCPharmacy.Services;

public enum SaleResultKind { Success, NotFound, InsufficientStock }
public record SaleResult(SaleResultKind Kind, Sale? Sale)
{
    public static SaleResult NotFound => new(SaleResultKind.NotFound, null);
    public static SaleResult InsufficientStock => new(SaleResultKind.InsufficientStock, null);
}

public sealed class PharmacyStore
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _filePath;
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public PharmacyStore(IWebHostEnvironment environment)
    {
        var dataDirectory = Path.Combine(environment.ContentRootPath, "Data");
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "pharmacy.json");
    }

    public async Task<IEnumerable<Medicine>> GetMedicinesAsync(string? search)
    {
        var data = await ReadAsync();
        return data.Medicines
            .Where(m => string.IsNullOrWhiteSpace(search) || m.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.Name)
            .ToList();
    }

    public async Task<IEnumerable<Sale>> GetSalesAsync() => (await ReadAsync()).Sales.OrderByDescending(s => s.SoldAt).ToList();

    public async Task<Medicine> AddMedicineAsync(MedicineInput input)
    {
        await _lock.WaitAsync();
        try
        {
            var data = await ReadUnlockedAsync();
            var medicine = new Medicine(Guid.NewGuid(), input.Name.Trim(), input.Notes?.Trim() ?? "", input.ExpiryDate.Date, input.Quantity, decimal.Round(input.Price, 2), input.Brand.Trim());
            data.Medicines.Add(medicine);
            await WriteUnlockedAsync(data);
            return medicine;
        }
        finally { _lock.Release(); }
    }

    public async Task<SaleResult> RecordSaleAsync(Guid medicineId, int quantity)
    {
        await _lock.WaitAsync();
        try
        {
            var data = await ReadUnlockedAsync();
            var index = data.Medicines.FindIndex(m => m.Id == medicineId);
            if (index < 0) return SaleResult.NotFound;
            var medicine = data.Medicines[index];
            if (medicine.Quantity < quantity) return SaleResult.InsufficientStock;
            data.Medicines[index] = medicine with { Quantity = medicine.Quantity - quantity };
            var sale = new Sale(Guid.NewGuid(), medicine.Id, medicine.Name, quantity, medicine.Price, medicine.Price * quantity, DateTime.UtcNow);
            data.Sales.Add(sale);
            await WriteUnlockedAsync(data);
            return new SaleResult(SaleResultKind.Success, sale);
        }
        finally { _lock.Release(); }
    }

    private async Task<PharmacyData> ReadAsync()
    {
        await _lock.WaitAsync();
        try { return await ReadUnlockedAsync(); }
        finally { _lock.Release(); }
    }

    private async Task<PharmacyData> ReadUnlockedAsync()
    {
        if (!File.Exists(_filePath))
        {
            var seed = SeedData();
            await WriteUnlockedAsync(seed);
            return seed;
        }
        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<PharmacyData>(stream, _json) ?? new PharmacyData();
    }

    private async Task WriteUnlockedAsync(PharmacyData data)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, data, _json);
    }

    private static PharmacyData SeedData() => new()
    {
        Medicines = [
            new(Guid.NewGuid(), "Paracetamol 500mg", "Store in a cool, dry place.", DateTime.Today.AddDays(25), 42, 3.50m, "ABC Health"),
            new(Guid.NewGuid(), "Cetirizine 10mg", "One tablet daily as directed.", DateTime.Today.AddMonths(8), 7, 6.75m, "Wellness Labs"),
            new(Guid.NewGuid(), "Vitamin C 1000mg", "Effervescent tablets.", DateTime.Today.AddYears(1), 65, 12.00m, "NutriPlus")]
    };
}
