using System.Net;
using System.Text;
using System.Text.Json;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Localization;
using NetCheck.Core.Models;
using NetCheck.Infrastructure.Storage;

namespace NetCheck.Infrastructure.Export;

public sealed class ReportExporter : IReportExporter
{
    private readonly ITextLocalizer _text;

    public ReportExporter(ITextLocalizer? text = null)
    {
        _text = text ?? InvariantTextLocalizer.Instance;
    }

    public async Task ExportAsync(
        DiagnosticReport report,
        string filePath,
        bool includeComputerName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("An export path is required.", nameof(filePath));
        }

        var redactedAdapters = report.Network.Adapters
            .Select(adapter => adapter with { MacAddress = _text.Translate("Redacted") })
            .ToArray();
        var redactedPrimary = report.Network.PrimaryAdapter is null
            ? null
            : redactedAdapters.FirstOrDefault(adapter => adapter.Id == report.Network.PrimaryAdapter.Id)
              ?? report.Network.PrimaryAdapter with { MacAddress = _text.Translate("Redacted") };
        var exportNetwork = report.Network with
        {
            MachineName = includeComputerName ? report.Network.MachineName : _text.Translate("Redacted"),
            Adapters = redactedAdapters,
            PrimaryAdapter = redactedPrimary
        };
        var exportReport = report with { Network = exportNetwork };
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var content = extension switch
        {
            ".json" => JsonSerializer.Serialize(exportReport, JsonDefaults.Options),
            ".txt" => CreateText(exportReport),
            ".html" or ".htm" => CreateHtml(exportReport),
            _ => throw new NotSupportedException(_text.Translate("NetCheck can export HTML, JSON, or plain-text reports."))
        };

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryFile = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryFile, content, Encoding.UTF8, cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporaryFile, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryFile))
            {
                File.Delete(temporaryFile);
            }
        }
    }

    private string CreateText(DiagnosticReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine(_text.Translate("NETCHECK DIAGNOSTIC REPORT"));
        builder.AppendLine(new string('=', 72));
        builder.AppendLine($"{_text.Translate("Report ID")}: {report.Id}");
        builder.AppendLine($"{_text.Translate("Completed")}: {report.CompletedAtUtc.ToLocalTime().ToString("G", _text.Culture)}");
        builder.AppendLine($"{_text.Translate("Computer")}: {report.Network.MachineName}");
        builder.AppendLine($"{_text.Translate("Outcome")}: {_text.Translate(report.Diagnosis.Outcome.ToString())}");
        builder.AppendLine($"{_text.Translate("Diagnosis")}: {report.Diagnosis.Headline}");
        builder.AppendLine(report.Diagnosis.Summary);
        builder.AppendLine();
        builder.AppendLine(_text.Translate("NETWORK"));
        builder.AppendLine($"{_text.Translate("Adapter")}: {report.Network.PrimaryAdapter?.Name ?? _text.Translate("Not available")}");
        builder.AppendLine($"{_text.Translate("IP address")}: {_text.Translate(report.Network.PrimaryIpAddress)}");
        builder.AppendLine($"{_text.Translate("Gateway")}: {_text.Translate(report.Network.PrimaryGateway)}");
        builder.AppendLine($"DNS: {report.Network.PrimaryDnsServer}");
        builder.AppendLine();
        builder.AppendLine(_text.Translate("CHECKS"));

        foreach (var check in report.Checks)
        {
            builder.AppendLine();
            builder.AppendLine($"[{_text.Translate(check.Status.ToString().ToUpperInvariant())}] {check.Title}");
            builder.AppendLine(check.Summary);
            if (!string.IsNullOrWhiteSpace(check.Detail))
            {
                builder.AppendLine(check.Detail);
            }

            foreach (var item in check.Evidence)
            {
                builder.AppendLine($"  {item.Key}: {item.Value}");
            }

            foreach (var recommendation in check.Recommendations)
            {
                builder.AppendLine($"  - {recommendation}");
            }
        }

        return builder.ToString();
    }

    private string CreateHtml(DiagnosticReport report)
    {
        static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

        var builder = new StringBuilder();
        builder.AppendLine($"<!doctype html><html lang=\"{_text.Language}\"><head><meta charset=\"utf-8\">");
        builder.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        builder.AppendLine($"<title>{Encode(_text.Translate("NetCheck report"))} {report.Id}</title>");
        builder.AppendLine("<style>body{margin:0;background:#f4f7fb;color:#172033;font:15px Segoe UI,Arial,sans-serif}.wrap{max-width:920px;margin:40px auto;padding:0 24px}.hero,.card{background:white;border:1px solid #dfe5ef;border-radius:14px;box-shadow:0 6px 24px #1720330d}.hero{padding:30px;border-top:5px solid #356ae6}.brand{font-weight:700;color:#356ae6;letter-spacing:.08em}.hero h1{margin:12px 0 8px;font-size:28px}.muted{color:#65718a}.grid{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin:18px 0}.metric,.card{padding:18px}.metric{background:#edf3ff;border-radius:10px}.metric small{display:block;color:#65718a;margin-bottom:6px}.checks{display:grid;gap:12px}.check h3{margin:0 0 7px}.status{font-size:12px;font-weight:700;letter-spacing:.05em}.Passed{color:#16835b}.Warning{color:#a96300}.Failed{color:#c83a45}.Skipped{color:#65718a}dl{display:grid;grid-template-columns:180px 1fr;gap:6px;margin:12px 0}dt{color:#65718a}dd{margin:0;word-break:break-word}li{margin:6px 0}@media(max-width:700px){.grid{grid-template-columns:1fr 1fr}dl{grid-template-columns:1fr}}</style></head><body><main class=\"wrap\">");
        builder.AppendLine($"<section class=\"hero\"><div class=\"brand\">NETCHECK</div><h1>{Encode(report.Diagnosis.Headline)}</h1><p>{Encode(report.Diagnosis.Summary)}</p><p class=\"muted\">{Encode(_text.Translate("Completed"))} {Encode(report.CompletedAtUtc.ToLocalTime().ToString("G", _text.Culture))} · {Encode(_text.Translate("Report"))} {report.Id}</p></section>");
        builder.AppendLine("<section class=\"grid\">");
        AppendMetric(builder, _text.Translate("Computer"), report.Network.MachineName);
        AppendMetric(builder, _text.Translate("Adapter"), report.Network.PrimaryAdapter?.Name ?? _text.Translate("Not available"));
        AppendMetric(builder, _text.Translate("IP address"), _text.Translate(report.Network.PrimaryIpAddress));
        AppendMetric(builder, _text.Translate("Gateway"), _text.Translate(report.Network.PrimaryGateway));
        builder.AppendLine("</section><section class=\"checks\">");

        foreach (var check in report.Checks)
        {
            builder.AppendLine($"<article class=\"card check\"><div class=\"status {check.Status}\">{Encode(_text.Translate(check.Status.ToString().ToUpperInvariant()))}</div><h3>{Encode(check.Title)}</h3><p>{Encode(check.Summary)}</p>");
            if (!string.IsNullOrWhiteSpace(check.Detail))
            {
                builder.AppendLine($"<p class=\"muted\">{Encode(check.Detail)}</p>");
            }

            if (check.Evidence.Count > 0)
            {
                builder.AppendLine("<dl>");
                foreach (var item in check.Evidence)
                {
                    builder.AppendLine($"<dt>{Encode(item.Key)}</dt><dd>{Encode(item.Value)}</dd>");
                }

                builder.AppendLine("</dl>");
            }

            if (check.Recommendations.Count > 0)
            {
                builder.AppendLine($"<strong>{Encode(_text.Translate("Recommended actions"))}</strong><ul>");
                foreach (var recommendation in check.Recommendations)
                {
                    builder.AppendLine($"<li>{Encode(recommendation)}</li>");
                }

                builder.AppendLine("</ul>");
            }

            builder.AppendLine("</article>");
        }

        builder.AppendLine("</section></main></body></html>");
        return builder.ToString();
    }

    private static void AppendMetric(StringBuilder builder, string label, string value) =>
        builder.AppendLine($"<div class=\"metric\"><small>{WebUtility.HtmlEncode(label)}</small><strong>{WebUtility.HtmlEncode(value)}</strong></div>");
}
