namespace InvoiceDemo.Invoicing;

// The client application's own "business" domain: invoices, which have nothing to do with TemplateMaster.

public sealed record InvoiceLine(string Description, int Quantity, decimal UnitPrice)
{
    public decimal Total => Quantity * UnitPrice;
}

public sealed record Invoice(int Id, string Number, string Customer, DateOnly Date, IReadOnlyList<InvoiceLine> Lines)
{
    public const decimal TaxRate = 20m;

    public decimal SubTotal => Lines.Sum(l => l.Total);
    public decimal Tax => Math.Round(SubTotal * TaxRate / 100m, 2);
    public decimal Total => SubTotal + Tax;
}

/// <summary>In-memory storage for the demo (in a real application: your own database, your own ORM...).</summary>
public sealed class InvoiceRepository
{
    private readonly List<Invoice> _invoices =
    [
        new(1, "INV-2026-001", "Riverside Bakery", new DateOnly(2026, 9, 1),
            [new("Showcase website", 1, 1200m), new("Hosting (12 months)", 12, 15m)]),
        new(2, "INV-2026-002", "Westside Garage", new DateOnly(2026, 9, 15),
            [new("Appointment booking app", 1, 3500m), new("Training", 2, 400m)]),
        new(3, "INV-2026-003", "Harper Consulting", new DateOnly(2026, 10, 1),
            [new("Monthly maintenance", 3, 250m)]),
    ];

    public IReadOnlyList<Invoice> All() => _invoices;

    public Invoice? Find(int id) => _invoices.FirstOrDefault(i => i.Id == id);
}
