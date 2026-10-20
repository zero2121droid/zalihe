using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Zalihe.Application.Alerts;
using Zalihe.Application.Common;
using Zalihe.Domain.Users;
using Zalihe.Infrastructure.Persistence;

namespace Zalihe.Infrastructure.Email;

/// <summary>Marker for the email texts (alerts and invitations): AlertEmailTexts.resx (Serbian, the default) and AlertEmailTexts.en.resx.</summary>
public class AlertEmailTexts;

/// <summary>
/// Writes the alert emails for every user of the current company in the user's language (texts from
/// resx, numbers in that language's format) and sends them. No customer data is ever in them.
/// </summary>
public class AlertEmails(
    AppDbContext db,
    ITenantContext tenant,
    IEmailSender sender,
    IStringLocalizer<AlertEmailTexts> texts,
    IOptions<EmailOptions> options) : IAlertEmails
{
    public Task SendLowStockAsync(IReadOnlyList<LowStockEmailItem> items, CancellationToken ct) =>
        SendToCompanyAsync((company, culture) => LowStock(company, items, culture), ct);

    public Task SendChannelFailureAsync(ChannelFailureEmail email, CancellationToken ct) =>
        SendToCompanyAsync((company, culture) => ChannelFailure(company, email, culture), ct);

    /// <summary>
    /// The invitation to join the current company, in the inviter's language (the invited person has none
    /// yet). <paramref name="link"/> is the app path with the token, e.g. "/invite/abc".
    /// </summary>
    public async Task SendInvitationAsync(string to, string inviterName, string inviterLanguage, string link,
        DateTimeOffset expiresAt, CancellationToken ct)
    {
        var company = await db.Tenants.Where(t => t.Id == tenant.TenantId).Select(t => t.Name).SingleAsync(ct);
        var message = InLanguage(inviterLanguage, culture =>
        {
            var until = TimeZoneInfo.ConvertTime(expiresAt, LowStockAlertService.Zone).ToString("g", culture);
            string intro = texts["Invitation_Intro", inviterName, company];
            return Frame(company, texts["Invitation_Subject", inviterName], texts["Invitation_Heading", company],
                $"<p style=\"margin:0;font-size:14px;color:#3B4033\">{E(intro)}</p>", intro,
                texts["Invitation_Open"], link, texts["Invitation_Footer", until]);
        });
        await sender.SendAsync(message with { To = to, ToName = "" }, ct);
    }

    /// <summary>Writes the email for each user of the current company in the user's language and sends it.</summary>
    private async Task SendToCompanyAsync(Func<string, CultureInfo, EmailMessage> write, CancellationToken ct)
    {
        var company = await db.Tenants.Where(t => t.Id == tenant.TenantId).Select(t => t.Name).SingleAsync(ct);
        // Users have no global query filter, so they are limited to the current company here.
        var users = await db.Users
            .Where(u => u.TenantId == tenant.TenantId && u.Email != null)
            .OrderBy(u => u.Email)
            .Select(u => new { Email = u.Email!, u.Name, u.Language })
            .ToListAsync(ct);

        foreach (var user in users)
        {
            var message = InLanguage(user.Language, culture => write(company, culture));
            await sender.SendAsync(message with { To = user.Email, ToName = user.Name }, ct);
        }
    }

    private EmailMessage ChannelFailure(string company, ChannelFailureEmail email, CultureInfo culture)
    {
        var shop = new Uri(email.ShopUrl).Host;
        var since = TimeZoneInfo.ConvertTime(email.FailingSince, LowStockAlertService.Zone).ToString("g", culture);
        var reasonKey = $"ChannelFailure_Reason_{email.ErrorCode}";
        var reason = texts[reasonKey] is { ResourceNotFound: false } known ? known.Value : texts["ChannelFailure_Reason_Unknown"].Value;
        string intro = texts["ChannelFailure_Intro", since, shop];

        var body = $"<p style=\"margin:0 0 16px;font-size:14px;color:#3B4033\">{E(intro)}</p>"
                   + $"<p style=\"margin:0;font-size:12px;color:#5C6250\">{E(texts["ChannelFailure_Reason"])}</p>"
                   + $"<p style=\"margin:2px 0 0;font-size:14px;color:#B03A22\">{E(reason)}</p>";
        var text = $"{intro}\n\n{texts["ChannelFailure_Reason"]}: {reason}";
        return Frame(company, texts["ChannelFailure_Subject", company, shop], texts["ChannelFailure_Heading"], body, text,
            texts["ChannelFailure_Open"], "/channels", texts["ChannelFailure_Footer"]);
    }

    private EmailMessage LowStock(string company, IReadOnlyList<LowStockEmailItem> items, CultureInfo culture)
    {
        var html = new StringBuilder();
        html.Append($"<p style=\"margin:0 0 16px;font-size:14px;color:#3B4033\">{E(texts["LowStock_Intro"])}</p>");
        html.Append("<table style=\"width:100%;border-collapse:collapse;font-size:14px\"><tr>");
        html.Append($"<th style=\"text-align:left;padding:8px 0;border-bottom:1px solid #D9DCCE;font-size:12px;color:#5C6250\">{E(texts["LowStock_Item"])}</th>");
        html.Append($"<th style=\"text-align:right;padding:8px 0;border-bottom:1px solid #D9DCCE;font-size:12px;color:#5C6250\">{E(texts["LowStock_Stock"])}</th>");
        html.Append($"<th style=\"text-align:right;padding:8px 0;border-bottom:1px solid #D9DCCE;font-size:12px;color:#5C6250\">{E(texts["LowStock_Min"])}</th></tr>");

        var text = new StringBuilder();
        text.AppendLine(texts["LowStock_Intro"]).AppendLine();

        foreach (var item in items)
        {
            var unit = texts[$"Unit_{item.Unit}"];
            var stock = $"{Quantity(item.Stock, culture)} {unit}";
            var min = $"{Quantity(item.MinStock, culture)} {unit}";
            var outOfStock = item.Stock <= 0;
            // The light theme's status colors: readable on white (DESIGN.md).
            var color = outOfStock ? "#B03A22" : "#94600A";
            var badge = item.IsNew
                ? $" <span style=\"font-size:11px;padding:1px 6px;border-radius:8px;background:#E1E7CF;color:#55621E\">{E(texts["LowStock_New"])}</span>"
                : "";
            var status = outOfStock
                ? $"<div style=\"font-size:12px;color:{color};font-family:'IBM Plex Sans','Segoe UI',system-ui,sans-serif\">{E(texts["LowStock_OutOfStock"])}</div>"
                : "";

            html.Append("<tr>");
            html.Append($"<td style=\"padding:10px 0;border-bottom:1px solid #E7E9DF\"><div style=\"font-weight:500\">{E(item.Name)}{badge}</div>");
            html.Append($"<div style=\"font-family:'IBM Plex Mono',ui-monospace,monospace;font-size:11px;color:#5C6250\">{E(item.Sku)}</div></td>");
            html.Append($"<td style=\"padding:10px 0;border-bottom:1px solid #E7E9DF;text-align:right;white-space:nowrap;font-family:'IBM Plex Mono',ui-monospace,monospace;color:{color}\">{E(stock)}{status}</td>");
            html.Append($"<td style=\"padding:10px 0;border-bottom:1px solid #E7E9DF;text-align:right;white-space:nowrap;font-family:'IBM Plex Mono',ui-monospace,monospace;color:#5C6250\">{E(min)}</td>");
            html.Append("</tr>");

            var marks = string.Join(", ", new[] { item.IsNew ? texts["LowStock_New"].Value : null, outOfStock ? texts["LowStock_OutOfStock"].Value : null }.OfType<string>());
            text.AppendLine($"- {item.Name} ({item.Sku}): {stock}, {texts["LowStock_Min"]} {min}{(marks.Length > 0 ? $" [{marks}]" : "")}");
        }

        html.Append("</table>");
        return Frame(company, texts["LowStock_Subject", company, items.Count.ToString("N0", culture)], texts["LowStock_Heading"],
            html.ToString(), text.ToString().TrimEnd(), texts["LowStock_Open"], "/items?status=low", texts["LowStock_Footer"]);
    }

    /// <summary>
    /// The frame every alert shares: company, heading, the body, a button into the app and a note on why
    /// the email came. Colors are written out: emails know no CSS variables (light theme of DESIGN.md).
    /// </summary>
    private EmailMessage Frame(string company, string subject, string heading, string bodyHtml, string bodyText,
        string button, string path, string footer)
    {
        var link = $"{options.Value.AppUrl?.TrimEnd('/')}{path}";
        var html = "<!doctype html><html><body style=\"margin:0;padding:24px;background:#F5F5EF;font-family:'IBM Plex Sans','Segoe UI',system-ui,sans-serif;color:#1B1E16\">"
                   + "<div style=\"max-width:600px;margin:0 auto;background:#FFFFFF;border:1px solid #D9DCCE;border-radius:6px;padding:24px\">"
                   + $"<p style=\"margin:0 0 4px;font-size:12px;color:#5C6250\">{E(company)}</p>"
                   + $"<h1 style=\"margin:0 0 8px;font-size:20px;font-weight:600\">{E(heading)}</h1>"
                   + bodyHtml
                   + $"<p style=\"margin:20px 0 0\"><a href=\"{E(link)}\" style=\"display:inline-block;background:#B4C17E;color:#11130E;text-decoration:none;font-weight:600;padding:10px 16px;border-radius:4px\">{E(button)}</a></p>"
                   + $"<p style=\"margin:20px 0 0;font-size:12px;color:#5C6250\">{E(footer)}</p>"
                   + "</div></body></html>";
        var text = $"{company}\n{heading}\n\n{bodyText}\n\n{button}: {link}\n\n{footer}\n";
        return new EmailMessage("", "", subject, html, text);
    }

    /// <summary>Runs <paramref name="write"/> with the texts and number formats of <paramref name="language"/>.</summary>
    private static EmailMessage InLanguage(string language, Func<CultureInfo, EmailMessage> write)
    {
        var culture = CultureInfo.GetCultureInfo(language == Languages.English ? "en-US" : "sr-Latn-RS");
        var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        try
        {
            return write(culture);
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
        }
    }

    /// <summary>"1.284,5" in Serbian, "1,284.5" in English, at most three decimals (Item.QuantityDecimals).</summary>
    private static string Quantity(decimal value, CultureInfo culture) =>
        value.ToString("#,0.###", culture).Replace('-', '−');

    private static string E(string value) => WebUtility.HtmlEncode(value);
}
