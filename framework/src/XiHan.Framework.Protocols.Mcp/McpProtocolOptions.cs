// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.Mcp.Tools;

namespace XiHan.Framework.Protocols.Mcp;

/// <summary>
/// MCP 协议适配器选项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Protocols:Mcp</c> 显式读取：<c>ServerName</c>、<c>ServerVersion</c>、<c>Instructions</c>，<c>Tools:&lt;操作 ID&gt;</c> = tool 名显式覆盖推导的名字，HTTP 入口的 <c>AllowedOrigins</c> 与 <c>MaxRequestBodyBytes</c>（ADR-0199）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record McpProtocolOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Protocols:Mcp";

    /// <summary>
    /// <c>initialize</c> 答复里的服务端名
    /// </summary>
    /// <remarks>
    /// 空表示用应用名
    /// </remarks>
    public string? ServerName { get; init; }

    /// <summary>
    /// <c>initialize</c> 答复里的服务端版本
    /// </summary>
    /// <remarks>
    /// 默认为框架版本
    /// </remarks>
    public string ServerVersion
    {
        get => field;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    } = FrameworkInfo.Version;

    /// <summary>
    /// <c>initialize</c> 答复里给客户端模型的使用说明
    /// </summary>
    /// <remarks>
    /// 空表示不给
    /// </remarks>
    public string? Instructions { get; init; }

    /// <summary>
    /// 显式覆盖
    /// </summary>
    /// <remarks>
    /// 操作 ID → tool 名；未覆盖的操作按 <see cref="McpToolNameConvention"/> 推导
    /// </remarks>
    public IReadOnlyDictionary<string, string> Tools
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            foreach (var (id, name) in value)
            {
                OperationId.Validate(id);
                McpToolName.Validate(name);
            }

            field = value;
        }
    } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 请求体的缺省上限
    /// </summary>
    /// <remarks>
    /// 4 MiB，与 HTTP 操作端点的缺省一致
    /// </remarks>
    public const long DefaultMaxRequestBodyBytes = 4 * 1024 * 1024;

    /// <summary>
    /// HTTP 入口允许的浏览器源
    /// </summary>
    /// <remarks>
    /// 请求带 <c>Origin</c> 时必须是其中之一，否则 <see cref="McpErrors.OriginDenied"/>（HTTP 403）；不带 <c>Origin</c> 的非浏览器客户端不受影响。每项是 <c>scheme://host[:port]</c>（只收 http 与 https，没有路径、查询与片段），保存为规范形式（小写、缺省端口省略）。缺省为空：浏览器里的客户端要把它的源配进来（ADR-0199）
    /// </remarks>
    public IReadOnlyList<string> AllowedOrigins
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value.Select(static origin => NormalizeOrigin(origin) ?? throw new ArgumentException($"'{origin}' is not an origin; write it as 'scheme://host[:port]' with scheme http or https and no path.", nameof(AllowedOrigins))).Distinct(StringComparer.Ordinal).ToList();
        }
    } = [];

    /// <summary>
    /// HTTP 入口的请求体上限（字节）
    /// </summary>
    /// <remarks>
    /// 缺省 <see cref="DefaultMaxRequestBodyBytes"/>。<c>Content-Length</c> 超过时不读，没有声明长度的边读边计数，超过即 <see cref="McpErrors.BodyTooLarge"/>（HTTP 413，ADR-0199）
    /// </remarks>
    public long MaxRequestBodyBytes
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The request body limit must be positive.");
    } = DefaultMaxRequestBodyBytes;

    /// <summary>
    /// 请求的 <c>Origin</c> 是否允许
    /// </summary>
    /// <remarks>
    /// 按规范形式比较；<c>null</c> 与解析不了的值一律不允许
    /// </remarks>
    public bool IsAllowedOrigin(string origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        return NormalizeOrigin(origin) is { } normalized && AllowedOrigins.Contains(normalized, StringComparer.Ordinal);
    }

    /// <summary>
    /// 源的规范形式
    /// </summary>
    /// <remarks>
    /// 不是 http / https 的绝对地址、带路径（<c>/</c> 以外）、查询、片段或用户信息时为 <see langword="null"/>
    /// </remarks>
    private static string? NormalizeOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin) || !Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var bare = uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0 && uri.UserInfo.Length == 0;
        return bare && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant()
            : null;
    }

    /// <summary>
    /// 从配置读取
    /// </summary>
    /// <remarks>
    /// <c>AllowedOrigins</c> 有不是源的项、<c>MaxRequestBodyBytes</c> 不是正整数时抛出 <see cref="ArgumentException"/>
    /// </remarks>
    public static McpProtocolOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var tools = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var child in section.GetSection("Tools").GetChildren())
        {
            if (child.Value is { } name)
            {
                tools[child.Key] = name;
            }
        }

        return new McpProtocolOptions
        {
            ServerName = section["ServerName"],
            ServerVersion = section["ServerVersion"] ?? FrameworkInfo.Version,
            Instructions = section["Instructions"],
            Tools = tools,
            AllowedOrigins = section.GetSection("AllowedOrigins").GetChildren().Select(static child => child.Value ?? string.Empty).ToList(),
            MaxRequestBodyBytes = ReadMaxBody(section["MaxRequestBodyBytes"]),
        };
    }

    /// <summary>
    /// 读请求体上限
    /// </summary>
    /// <remarks>
    /// 缺失取缺省值，不是正整数抛出 <see cref="ArgumentException"/>
    /// </remarks>
    private static long ReadMaxBody(string? text)
    {
        if (text is null)
        {
            return DefaultMaxRequestBodyBytes;
        }

        return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var max) && max >= 1
            ? max
            : throw new ArgumentException($"{ConfigurationSection}:MaxRequestBodyBytes must be a positive integer, got '{text}'.");
    }

    /// <summary>
    /// 操作最终的 tool 名：显式覆盖优先，否则按约定推导
    /// </summary>
    public string ToolNameFor(OperationContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return Tools.TryGetValue(contract.Id, out var overridden) ? overridden : McpToolNameConvention.Derive(contract);
    }
}
