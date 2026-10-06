using System.Security.Claims;
using InvoiceDemo.Invoicing;
using InvoiceDemo.Pages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TemplateMaster;

var builder = WebApplication.CreateBuilder(args);

// =====================================================================================
// 1. YOUR application's own authentication (here: a cookie + two demo accounts).
//    TemplateMaster has no login of its own: it reuses the user already signed in here.
// =====================================================================================
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/access-denied";
        options.Cookie.Name = "invoicedemo-auth";
    });

builder.Services.AddAuthorization(options =>
    options.AddPolicy("TemplateMasterAdmin", policy => policy.RequireRole("Admin")));

// =====================================================================================
// 2. TemplateMaster (NuGet package TemplateMaster.AspNetCore).
// =====================================================================================
builder.Services
    .AddTemplateMaster(options =>
    {
        options.BasePath = "/templatemaster";
        // Creates the TemplateMaster database on first run (dev only, never alters an existing database).
        options.Database.AutoCreate = builder.Environment.IsDevelopment();
        // Your admins become TemplateMaster administrators, everyone else gets read-only access.
        options.Tenancy.RoleSelector = user => user.IsInRole("Admin")
            ? TemplateMasterRole.Administrator
            : TemplateMasterRole.Viewer;
    })
    .UseSqlServer(builder.Configuration.GetConnectionString("TemplateMaster")!)
    .UseRabbitMq(options =>
    {
        options.Host = builder.Configuration["RabbitMq:Host"]!;
        options.Port = builder.Configuration.GetValue<ushort>("RabbitMq:Port");
        options.Username = builder.Configuration["RabbitMq:Username"]!;
        options.Password = builder.Configuration["RabbitMq:Password"]!;
        options.QueuePrefix = "invoicedemo-templatemaster";
    })
    // TemplateMaster license (removes the watermark and the free-plan limits).
    // Key in appsettings: "TemplateMaster": { "LicenseKey": "..." }
    // (in production: environment variable TemplateMaster__LicenseKey or a secret store).
    .AddLicenseKey(licenseKey: builder.Configuration["TemplateMaster:LicenseKey"]);

// Your own application's services.
builder.Services.AddSingleton<InvoiceRepository>();
builder.Services.AddScoped<InvoiceDocuments>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// =====================================================================================
// 3. The TemplateMaster editor under /templatemaster, reserved to your application's admins.
// =====================================================================================
app.MapTemplateMaster("/templatemaster")
   .RequireAuthorization("TemplateMasterAdmin");

// =====================================================================================
// 4. Your own application's pages.
// =====================================================================================
app.MapGet("/", (ClaimsPrincipal user) => Results.Content(Html.Home(user), "text/html"));

app.MapGet("/login", (string? returnUrl) => Results.Content(Html.Login(returnUrl, null), "text/html"));

app.MapPost("/login", async (HttpContext context, IConfiguration configuration) =>
{
    var form = await context.Request.ReadFormAsync();
    var email = form["email"].ToString().Trim();
    var account = configuration.GetSection("Users").GetChildren()
        .FirstOrDefault(u => string.Equals(u["Email"], email, StringComparison.OrdinalIgnoreCase) && u["Password"] == form["password"]);

    if (account is null)
    {
        return Results.Content(Html.Login(form["returnUrl"], "Incorrect e-mail or password."), "text/html");
    }

    var claims = new List<Claim> { new(ClaimTypes.Name, account["Email"]!), new(ClaimTypes.Email, account["Email"]!) };
    claims.AddRange(account.GetSection("Roles").Get<string[]>()?.Select(r => new Claim(ClaimTypes.Role, r)) ?? []);
    await context.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

    var returnUrl = form["returnUrl"].ToString();
    return Results.LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
});

app.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync();
    return Results.Redirect("/");
});

app.MapGet("/access-denied", () => Results.Content(
    "<p>Access denied: the TemplateMaster editor is reserved to the Admin role. <a href=/>Home</a></p>", "text/html"));

// Invoices: using TemplateMaster from C# code (ITemplateMaster, no HTTP).
var invoices = app.MapGroup("/invoices").RequireAuthorization();

invoices.MapGet("/", (ClaimsPrincipal user, InvoiceRepository repository, string? message) =>
    Results.Content(Html.Invoices(user, repository.All(), message), "text/html"));

invoices.MapGet("/{id:int}/preview", async (int id, InvoiceRepository repository, InvoiceDocuments documents, CancellationToken ct) =>
    repository.Find(id) is { } invoice
        ? Results.Content(await documents.PreviewHtmlAsync(invoice, ct), "text/html")
        : Results.NotFound());

invoices.MapGet("/{id:int}/pdf", async (int id, InvoiceRepository repository, InvoiceDocuments documents, CancellationToken ct) =>
    repository.Find(id) is { } invoice
        ? Results.File(await documents.PdfAsync(invoice, ct), "application/pdf", $"{invoice.Number}.pdf")
        : Results.NotFound());

invoices.MapPost("/{id:int}/pdf-background", async (int id, InvoiceRepository repository, InvoiceDocuments documents, CancellationToken ct) =>
{
    if (repository.Find(id) is not { } invoice) return Results.NotFound();
    var documentId = await documents.QueuePdfAsync(invoice, ct);
    return Results.Redirect($"/invoices?message={Uri.EscapeDataString($"PDF {invoice.Number} queued (document {documentId}).")}");
});

invoices.MapPost("/{id:int}/email", async (int id, ClaimsPrincipal user, InvoiceRepository repository, InvoiceDocuments documents, CancellationToken ct) =>
{
    if (repository.Find(id) is not { } invoice) return Results.NotFound();
    var emailId = await documents.SendByEmailAsync(invoice, user, ct);
    return Results.Redirect($"/invoices?message={Uri.EscapeDataString($"Invoice {invoice.Number} e-mailed to {user.Identity!.Name} (e-mail {emailId}).")}");
});

app.MapGet("/templates", async (InvoiceDocuments documents, CancellationToken ct) => Results.Ok(await documents.TemplatesAsync(ct)))
   .RequireAuthorization();

app.Run();
