using System.Security.Claims;
using TemplateMaster;

namespace InvoiceDemo.Invoicing;

/// <summary>
/// Everything the application asks TemplateMaster for, via the C# API <see cref="ITemplateMaster"/> (in-process, no HTTP).
/// </summary>
public sealed class InvoiceDocuments
{
    private readonly ITemplateMaster _templateMaster;
    private readonly IConfiguration _configuration;

    public InvoiceDocuments(ITemplateMaster templateMaster, IConfiguration configuration)
    {
        _templateMaster = templateMaster;
        _configuration = configuration;
    }

    public async Task<string> PreviewHtmlAsync(Invoice invoice, CancellationToken ct)
        => await _templateMaster.RenderHtmlAsync(await InvoiceTemplateCodeAsync(ct), Model(invoice), cancellationToken: ct);

    public async Task<byte[]> PdfAsync(Invoice invoice, CancellationToken ct)
        => await _templateMaster.GeneratePdfAsync(await InvoiceTemplateCodeAsync(ct), Model(invoice), fileName: $"{invoice.Number}.pdf", cancellationToken: ct);

    public async Task<string> QueuePdfAsync(Invoice invoice, CancellationToken ct)
        => await _templateMaster.QueueDocumentAsync(await InvoiceTemplateCodeAsync(ct), Model(invoice), fileName: $"{invoice.Number}.pdf", cancellationToken: ct);

    /// <summary>Sends the e-mail on behalf of the signed-in user (their TemplateMaster organization).</summary>
    public async Task<string> SendByEmailAsync(Invoice invoice, ClaimsPrincipal user, CancellationToken ct)
    {
        var templateMaster = _templateMaster.ForUser(user);
        var emailTemplate = (await templateMaster.GetTemplatesAsync(TemplateMasterTemplateType.Email, ct)).First();

        return await templateMaster.SendEmailAsync(new TemplateMasterEmail
        {
            TemplateCode = emailTemplate.Code,
            To = user.FindFirstValue(ClaimTypes.Email)!,
            Subject = $"Your invoice {invoice.Number}",
            Model = Model(invoice)
        }, ct);
    }

    public Task<IReadOnlyList<TemplateMasterTemplate>> TemplatesAsync(CancellationToken ct)
        => _templateMaster.GetTemplatesAsync(cancellationToken: ct);

    /// <summary>
    /// Invoice template code: "Invoices:TemplateCode" in appsettings.json if set (copied from the editor),
    /// otherwise the "invoice" template TemplateMaster creates in every new organization.
    /// </summary>
    private async Task<string> InvoiceTemplateCodeAsync(CancellationToken ct)
    {
        var code = _configuration["Invoices:TemplateCode"];
        if (!string.IsNullOrWhiteSpace(code))
        {
            return code;
        }

        var templates = await _templateMaster.GetTemplatesAsync(TemplateMasterTemplateType.Document, ct);
        return (templates.FirstOrDefault(t => t.Name == "invoice") ?? templates.First()).Code;
    }

    /// <summary>
    /// Data sent to the template. Property names = the template's variables
    /// ({{invoice_number}}, {{customer_name}}, {{#each items}}...).
    /// </summary>
    private static object Model(Invoice invoice) => new
    {
        invoice_number = invoice.Number,
        invoice_date = invoice.Date.ToString("yyyy-MM-dd"),
        due_date = invoice.Date.AddDays(30).ToString("yyyy-MM-dd"),
        customer_name = invoice.Customer,
        store_name = "Acme Invoicing",
        store_address = "123 Integration Street, Springfield",
        store_phone = "+1 555-0100",
        store_email = "invoices@example.com",
        items = invoice.Lines.Select(l => new
        {
            description = l.Description,
            quantity = l.Quantity,
            unit_price = l.UnitPrice,
            total_price = l.Total
        }),
        sub_total = invoice.SubTotal,
        tax_rate = Invoice.TaxRate,
        tax_amount = invoice.Tax,
        total_amount = invoice.Total
    };
}
