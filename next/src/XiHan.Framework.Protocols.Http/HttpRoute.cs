// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http;

/// <summary>
/// 一个操作在 HTTP 上的位置：方法与路由模板（ASP.NET Core 路由语法，以 <c>/</c> 开头，<c>{id}</c> 等参数绑定到请求对象的同名属性）。
/// 文本形态 <c>POST /orders:export</c> 用于配置覆盖。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record HttpRoute
{
    /// <summary>
    /// 允许的 HTTP 方法。
    /// </summary>
    public static readonly IReadOnlyList<string> Methods = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    /// <summary>
    /// 创建路由。
    /// </summary>
    /// <param name="method">HTTP 方法，不区分大小写。</param>
    /// <param name="template">路由模板，以 <c>/</c> 开头。</param>
    public HttpRoute(string method, string template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        var normalized = method.ToUpperInvariant();
        if (!Methods.Contains(normalized, StringComparer.Ordinal))
        {
            throw new ArgumentException($"HTTP method '{method}' is not supported; expected one of {string.Join(", ", Methods)}.", nameof(method));
        }

        if (!template.StartsWith('/'))
        {
            throw new ArgumentException($"Route template '{template}' must start with '/'.", nameof(template));
        }

        Method = normalized;
        Template = template;
    }

    /// <summary>
    /// HTTP 方法（大写）。
    /// </summary>
    public string Method { get; }

    /// <summary>
    /// 路由模板。
    /// </summary>
    public string Template { get; }

    /// <summary>
    /// 解析 <c>METHOD /template</c> 形态的文本。
    /// </summary>
    public static HttpRoute Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Route '{text}' is not valid; expected 'METHOD /template' such as 'POST /orders:export'.", nameof(text));
        }

        return new HttpRoute(parts[0], parts[1]);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Method} {Template}";
}
