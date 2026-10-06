# Invoice Demo

A demo application: a small ASP.NET Core invoicing app that integrates
[TemplateMaster](https://templatemaster.fr) — visual template editing, PDF generation, e-mail sending — **the way a
real customer would**, through the single public NuGet package `TemplateMaster.AspNetCore`, with no reference to
TemplateMaster's own source code.

It is a concrete answer to "how do I integrate TemplateMaster into my .NET application?". Step-by-step guide:
https://templatemaster.fr/documentation/dotnet-integration

## What this shows

| File | What it shows |
|---|---|
| `InvoiceDemo.csproj` | a single dependency: `TemplateMaster.AspNetCore` (nuget.org) |
| `appsettings.json` | SQL connection, RabbitMQ, `TemplateMaster` section (SMTP) |
| `appsettings.Development.json` | demo accounts and the optional template code |
| `Program.cs` | `AddTemplateMaster()`, `UseSqlServer()`, `UseRabbitMq()`, `MapTemplateMaster()`, and the app's own authentication |
| `Invoicing/InvoiceDocuments.cs` | using `ITemplateMaster` from business code (PDF, background PDF, e-mail) |

TemplateMaster has no login of its own: it reuses the user already signed in to the application (here, a simple
cookie with two demo accounts), and exposes its editor under `/templatemaster`, reserved to the app's admins.

## 1. Prerequisites (one-time)

```bash
# RabbitMQ (:5672) + smtp4dev, to receive test e-mails (:2525, UI http://localhost:5080)
docker compose up -d

# SQL Server: a local instance (Windows authentication by default - see ConnectionStrings:TemplateMaster
# in appsettings.json; adapt it to your own instance, or point it at a SQL Server container).
```

## 2. Run

```bash
dotnet run
```

Open http://localhost:5300. On first run, the `InvoiceDemoTemplateMaster` database is created automatically
(`Database.AutoCreate` in Development).

Without a TemplateMaster license, the application works normally: generated PDFs and e-mails are simply watermarked
and free-plan limits apply — plenty to explore this demo. To test without a watermark, add your own key via user
secrets (never in `appsettings.json`):

```bash
dotnet user-secrets set "TemplateMaster:LicenseKey" "<your key>"
```

## 3. Scenarios to try

**Accounts** (defined in `appsettings.Development.json`):

- `admin@example.com` / `demo` — Admin role: invoices + TemplateMaster editor;
- `accountant@example.com` / `demo` — no Admin role: invoices only.

| # | Action | Expected result |
|---|---|---|
| 1 | Open `/invoices` while signed out | redirected to **the application's own** sign-in page (not a TemplateMaster login) |
| 2 | Sign in as admin, `/invoices` → **HTML preview** | the invoice rendered by TemplateMaster with the app's own data |
| 3 | **PDF** | the PDF downloads |
| 4 | **Generate PDF in background** | confirmation message; the document shows up in `/templatemaster/documents` (processed via RabbitMQ) |
| 5 | **E-mail me** | the e-mail arrives in smtp4dev: http://localhost:5080 |
| 6 | Open `/templatemaster/` | the TemplateMaster editor, embedded in the application |
| 7 | In the editor, edit the **invoice** template (e.g. its title), save, then redo 2 or 3 | the PDF/preview reflects the change |
| 8 | Refresh the browser (F5) on `/templatemaster/template/...` | the page reloads correctly |
| 9 | Sign in as `accountant@example.com` and open `/templatemaster/` | access denied (`TemplateMasterAdmin` policy); invoices remain accessible |
| 10 | `/templates` | the organization's list of templates (JSON) |

### Using a different template

Create a template in the editor, copy its **code** (a GUID), then set it in `appsettings.Development.json` →
`Invoices:TemplateCode`. The variables the application sends are listed in `InvoiceDocuments.Model(...)`
(`invoice_number`, `customer_name`, `items`, `total_amount`...).

### Going further

- **Multi-tenant**: in `Program.cs`, set `options.Tenancy.Mode = TemplateMasterTenancyMode.HostManaged` with a
  `TenantKeySelector` (e.g. a `tenant` claim added at sign-in): each customer then gets its own templates.
- **Without RabbitMQ**: replace `.UseRabbitMq(...)` with `.UseInMemoryMessaging()`.
- **Nothing to install for background jobs**: the embedded TemplateMaster has no Hangfire dependency at all; its
  background processing (retries, campaigns...) runs directly inside the application's own process.

## Troubleshooting

| Problem | Solution |
|---|---|
| `NU1101 Unable to find package TemplateMaster.AspNetCore` | check access to nuget.org and the version in `InvoiceDemo.csproj` |
| RabbitMQ connection error on startup | `docker compose up -d` (step 1) |
| SQL connection error on startup | check that your SQL Server instance is running and matches `ConnectionStrings:TemplateMaster` |
| First PDF is slow | one-time Chromium download (~150 MB) into `bin/` |

Full documentation: https://templatemaster.fr/documentation/dotnet-integration

## License

This demo application's code is MIT licensed (see [LICENSE](LICENSE)). TemplateMaster itself remains subject to its
own license — see https://templatemaster.fr.
