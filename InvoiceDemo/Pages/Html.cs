using System.Globalization;
using System.Net;
using System.Security.Claims;
using InvoiceDemo.Invoicing;
using TemplateMaster;

namespace InvoiceDemo.Pages;

/// <summary>Minimalistic HTML pages for the application (deliberately without a front-end framework).</summary>
public static class Html
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-IE"); // English wording, € currency.

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");

    public static string Home(ClaimsPrincipal user) => Page("Invoice Demo", $"""
        <h1>Invoice Demo</h1>
        <p>An ASP.NET Core "client" application integrating TemplateMaster via the NuGet package <code>TemplateMaster.AspNetCore</code>.</p>
        {Session(user)}
        <ul>
          <li><a href="/invoices">My invoices</a> - PDF generation / preview / e-mail with <code>ITemplateMaster</code></li>
          <li><a href="/templatemaster/">TemplateMaster</a> - the embedded template editor (Admin role required)</li>
          <li><a href="/templates">Available templates</a> (JSON)</li>
        </ul>
        """);

    public static string Login(string? returnUrl, string? error) => Page("Sign in", $"""
        <h1>Sign in</h1>
        {(error is null ? "" : $"<p class=err>{E(error)}</p>")}
        <form method="post" action="/login">
          <input type="hidden" name="returnUrl" value="{E(returnUrl ?? "/")}">
          <p><label>E-mail<br><input name="email" type="email" value="admin@example.com" required></label></p>
          <p><label>Password<br><input name="password" type="password" value="demo" required></label></p>
          <button type="submit">Sign in</button>
        </form>
        <p class=muted>Demo accounts (appsettings.json, <code>Users</code> section):<br>
        <b>admin@example.com</b> / demo - Admin role (access to the TemplateMaster editor)<br>
        <b>accountant@example.com</b> / demo - no Admin role (invoices only)</p>
        """);

    public static string Invoices(ClaimsPrincipal user, IReadOnlyList<Invoice> invoices, string? message) => Page("Invoices", $"""
        <h1>Invoices</h1>
        {Session(user)}
        {(message is null ? "" : $"<p class=ok>{E(message)}</p>")}
        <table>
          <tr><th>Number</th><th>Customer</th><th>Date</th><th>Total</th><th>Actions (via ITemplateMaster)</th></tr>
          {string.Join("", invoices.Select(i => $"""
            <tr>
              <td>{E(i.Number)}</td><td>{E(i.Customer)}</td><td>{i.Date:yyyy-MM-dd}</td><td>{i.Total.ToString("C", Culture)}</td>
              <td>
                <a href="/invoices/{i.Id}/preview" target="_blank">HTML preview</a> ·
                <a href="/invoices/{i.Id}/pdf">PDF</a> ·
                <form method="post" action="/invoices/{i.Id}/pdf-background"><button>Generate PDF in background</button></form> ·
                <form method="post" action="/invoices/{i.Id}/email"><button>E-mail me</button></form>
              </td>
            </tr>
            """))}
        </table>
        <p class=muted>Generated PDFs show up in <a href="/templatemaster/documents">TemplateMaster → Documents</a>,
        e-mails in <a href="/templatemaster/emails">TemplateMaster → Emails</a> and in smtp4dev
        (<a href="http://localhost:5080" target="_blank">http://localhost:5080</a>).</p>
        <p><a href="/">← Home</a></p>
        """);

    private static string Session(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true
        ? $"<p>Signed in as: <b>{E(user.Identity.Name)}</b>{(user.IsInRole("Admin") ? " (Admin)" : "")} <form method=post action=/logout><button>Sign out</button></form></p>"
        : "<p><a href=/login>Sign in</a></p>";

    private static string Page(string title, string body) => $$"""
        <!doctype html>
        <html lang="en"><head><meta charset="utf-8"><title>{{E(title)}}</title>
        <style>
          body { font-family: system-ui, sans-serif; max-width: 980px; margin: 32px auto; padding: 0 16px; color: #1f2937 }
          table { border-collapse: collapse; width: 100% } td, th { border-bottom: 1px solid #e5e7eb; padding: 8px; text-align: left }
          form { display: inline } button { cursor: pointer } .muted { color: #6b7280 } .ok { color: #047857 } .err { color: #b91c1c }
        </style></head>
        <body>{{body}}</body></html>
        """;
}
