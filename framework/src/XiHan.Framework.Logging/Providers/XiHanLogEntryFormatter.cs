// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Logging.Providers;

/// <summary>
/// 日志条目格式化器
/// </summary>
internal static partial class XiHanLogEntryFormatter
{
    /// <summary>
    /// 格式化日志条目
    /// </summary>
    public static string Format(
        string template,
        DateTimeOffset timestamp,
        LogLevel logLevel,
        string category,
        string message,
        Exception? exception,
        string? scope,
        bool includeTimestamp = true,
        bool includeLogLevel = true,
        bool includeCategory = true,
        bool singleLine = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentNullException.ThrowIfNull(category);

        var normalizedMessage = singleLine ? CollapseLineBreaks(message) : message;
        var exceptionText = exception?.ToString() ?? string.Empty;
        if (singleLine)
        {
            exceptionText = CollapseLineBreaks(exceptionText);
            scope = CollapseLineBreaks(scope);
        }

        var hasScopeToken = template.Contains("{Scope}", StringComparison.Ordinal);
        var hasExceptionToken = template.Contains("{Exception}", StringComparison.Ordinal);
        var newlineToken = string.IsNullOrEmpty(exceptionText) ? string.Empty : Environment.NewLine;

        var result = TimestampTokenRegex().Replace(template, match =>
        {
            if (!includeTimestamp)
            {
                return string.Empty;
            }

            var format = match.Groups["format"].Success
                ? match.Groups["format"].Value
                : "yyyy-MM-dd HH:mm:ss.fff";
            return timestamp.ToString(format);
        });

        result = LevelTokenRegex().Replace(result, match =>
        {
            if (!includeLogLevel)
            {
                return string.Empty;
            }

            var format = match.Groups["format"].Success ? match.Groups["format"].Value : null;
            return FormatLogLevel(logLevel, format);
        });

        result = result
            .Replace("{Category}", includeCategory ? category : string.Empty, StringComparison.Ordinal)
            .Replace("{Message}", normalizedMessage, StringComparison.Ordinal)
            .Replace("{Exception}", exceptionText, StringComparison.Ordinal)
            .Replace("{Scope}", scope ?? string.Empty, StringComparison.Ordinal)
            .Replace("{NewLine}", newlineToken, StringComparison.Ordinal)
            .TrimEnd();

        if (!string.IsNullOrWhiteSpace(scope) && !hasScopeToken)
        {
            result = $"{result} [Scope: {scope}]";
        }

        if (!string.IsNullOrWhiteSpace(exceptionText) && !hasExceptionToken)
        {
            result = $"{result}{Environment.NewLine}{exceptionText}";
        }

        return result;
    }

    /// <summary>
    /// 从作用域提供器提取作用域文本
    /// </summary>
    public static string? BuildScopeText(IExternalScopeProvider scopeProvider, bool includeScopes)
    {
        if (!includeScopes)
        {
            return null;
        }

        var scopeTexts = new List<string>();
        scopeProvider.ForEachScope((scope, state) =>
        {
            if (scope == null)
            {
                return;
            }

            var text = ScopeToString(scope);
            if (!string.IsNullOrWhiteSpace(text))
            {
                state.Add(text);
            }
        }, scopeTexts);

        return scopeTexts.Count == 0 ? null : string.Join(" => ", scopeTexts);
    }

    private static string ScopeToString(object scope)
    {
        if (scope is IEnumerable<KeyValuePair<string, object?>> keyValuePairs)
        {
            var pairs = keyValuePairs
                .Where(pair => !string.Equals(pair.Key, "{OriginalFormat}", StringComparison.Ordinal))
                .Select(pair => $"{pair.Key}={pair.Value}");
            var merged = string.Join(", ", pairs);
            if (!string.IsNullOrWhiteSpace(merged))
            {
                return merged;
            }
        }

        return scope.ToString() ?? string.Empty;
    }

    private static string CollapseLineBreaks(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal);
    }

    private static string FormatLogLevel(LogLevel logLevel, string? format)
    {
        var name = logLevel.ToString();
        if (string.IsNullOrWhiteSpace(format))
        {
            return name;
        }

        return format.ToLowerInvariant() switch
        {
            "u3" => name.Length >= 3 ? name[..3].ToUpperInvariant() : name.ToUpperInvariant(),
            "w3" => name.Length >= 3 ? name[..3].ToLowerInvariant() : name.ToLowerInvariant(),
            _ => name
        };
    }

    [GeneratedRegex(@"\{Timestamp(?::(?<format>[^}]+))?\}", RegexOptions.Compiled)]
    private static partial Regex TimestampTokenRegex();

    [GeneratedRegex(@"\{Level(?::(?<format>[^}]+))?\}", RegexOptions.Compiled)]
    private static partial Regex LevelTokenRegex();
}
