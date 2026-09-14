namespace ABCPharmacy.Models;

public record Medicine(Guid Id, string Name, string Notes, DateTime ExpiryDate, int Quantity, decimal Price, string Brand);
public record MedicineInput(string Name, string? Notes, DateTime ExpiryDate, int Quantity, decimal Price, string Brand);
public record Sale(Guid Id, Guid MedicineId, string MedicineName, int Quantity, decimal UnitPrice, decimal Total, DateTime SoldAt);
public record SaleInput(int Quantity);

public sealed class PharmacyData
{
    public List<Medicine> Medicines { get; set; } = [];
    public List<Sale> Sales { get; set; } = [];
}
