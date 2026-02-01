#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ErrorResponseSerializer
// Guid:2b3c4d5e-7f8a-9b0c-1d2e-3f4a5b6c7d8e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/29 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;
using System.Text.Json;
using System.Xml.Serialization;
using XiHan.Framework.Utils.Security.ErrorObfuscation.Models;

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Utilities;

/// <summary>
/// 错误响应序列化器
/// </summary>
public static class ErrorResponseSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// 将错误响应对象序列化为字符串
    /// </summary>
    public static string Serialize(object errorResponse)
    {
        return errorResponse switch
        {
            JsonErrorResponse json => SerializeJson(json),
            JsonErrorArrayResponse jsonArray => SerializeJson(jsonArray),
            PlainTextErrorResponse plainText => plainText.ToString(),
            XmlErrorResponse xml => SerializeXml(xml),
            HtmlErrorResponse html => SerializeHtml(html),
            _ => JsonSerializer.Serialize(errorResponse, JsonOptions)
        };
    }

    /// <summary>
    /// 序列化 JSON 对象
    /// </summary>
    private static string SerializeJson(object obj)
    {
        return JsonSerializer.Serialize(obj, JsonOptions);
    }

    /// <summary>
    /// 序列化 XML 对象
    /// </summary>
    private static string SerializeXml(XmlErrorResponse xml)
    {
        var serializer = new XmlSerializer(typeof(XmlErrorResponse));
        using var writer = new StringWriter();
        serializer.Serialize(writer, xml);
        return writer.ToString();
    }

    /// <summary>
    /// 序列化 HTML 对象
    /// </summary>
    private static string SerializeHtml(HtmlErrorResponse html)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"  <title>Error {html.StatusCode} - {html.ErrorType}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: #f5f5f5; margin: 0; padding: 20px; }");
        sb.AppendLine("    .container { max-width: 1200px; margin: 0 auto; background: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
        sb.AppendLine("    .header { border-bottom: 3px solid #dc3545; padding-bottom: 20px; margin-bottom: 20px; }");
        sb.AppendLine("    h1 { color: #dc3545; margin: 0; font-size: 32px; }");
        sb.AppendLine("    .error-code { color: #6c757d; font-size: 18px; margin-top: 5px; }");
        sb.AppendLine("    .section { margin: 20px 0; }");
        sb.AppendLine("    .section-title { font-weight: bold; color: #495057; margin-bottom: 10px; font-size: 18px; }");
        sb.AppendLine("    .info-grid { display: grid; grid-template-columns: 150px 1fr; gap: 10px; }");
        sb.AppendLine("    .info-label { font-weight: bold; color: #6c757d; }");
        sb.AppendLine("    .info-value { color: #212529; }");
        sb.AppendLine("    .stack-trace { background: #f8f9fa; border: 1px solid #dee2e6; border-radius: 4px; padding: 15px; overflow-x: auto; }");
        sb.AppendLine("    .stack-trace pre { margin: 0; font-family: 'Courier New', monospace; font-size: 13px; line-height: 1.5; }");
        sb.AppendLine("    .footer { margin-top: 30px; padding-top: 20px; border-top: 1px solid #dee2e6; color: #6c757d; font-size: 14px; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");
        sb.AppendLine("    <div class=\"header\">");
        sb.AppendLine($"      <h1>{html.ErrorType}</h1>");
        sb.AppendLine($"      <div class=\"error-code\">HTTP Error {html.StatusCode}</div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"section\">");
        sb.AppendLine("      <div class=\"section-title\">Error Information</div>");
        sb.AppendLine("      <div class=\"info-grid\">");
        sb.AppendLine("        <div class=\"info-label\">Message:</div>");
        sb.AppendLine($"        <div class=\"info-value\">{ErrorUtilities.EscapeHtml(html.Message)}</div>");
        sb.AppendLine("        <div class=\"info-label\">Exception:</div>");
        sb.AppendLine($"        <div class=\"info-value\">{ErrorUtilities.EscapeHtml(html.ExceptionType)}</div>");
        sb.AppendLine("        <div class=\"info-label\">Language:</div>");
        sb.AppendLine($"        <div class=\"info-value\">{html.Language}</div>");
        sb.AppendLine("        <div class=\"info-label\">Timestamp:</div>");
        sb.AppendLine($"        <div class=\"info-value\">{html.Timestamp} UTC</div>");
        sb.AppendLine("        <div class=\"info-label\">Trace ID:</div>");
        sb.AppendLine($"        <div class=\"info-value\">{html.TraceId}</div>");
        sb.AppendLine("        <div class=\"info-label\">Server:</div>");
        sb.AppendLine($"        <div class=\"info-value\">{ErrorUtilities.EscapeHtml(html.Server)}</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"section\">");
        sb.AppendLine("      <div class=\"section-title\">Stack Trace</div>");
        sb.AppendLine("      <div class=\"stack-trace\">");
        sb.AppendLine($"        <pre>{ErrorUtilities.EscapeHtml(html.StackTrace)}</pre>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"footer\">");
        sb.AppendLine($"      <p>Server: {ErrorUtilities.EscapeHtml(html.Server)} | Host: {html.Hostname}</p>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}
