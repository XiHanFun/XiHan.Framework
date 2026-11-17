// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Protocol.Handshake;

/// <summary>
/// 一个本机 gRPC 端点
/// </summary>
/// <remarks>
/// Unix 域套接字、回环 TCP 或 Windows 命名管道（<c>spec/plugin/handshake.md</c> §2）。地址是一个或多个非空白字符，空白与 .NET 正则的 <c>\s</c> 相同；TCP 的地址只认 <c>127.0.0.0/8</c> 的点分十进制（四段、不带前导零）或 <c>[::1]</c>，加 1–65535 的端口（不带正负号与前导零）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial record PluginEndpoint
{
    /// <summary>
    /// 网络类型：Unix 域套接字
    /// </summary>
    public const string Unix = "unix";

    /// <summary>
    /// 网络类型：回环 TCP
    /// </summary>
    public const string Tcp = "tcp";

    /// <summary>
    /// 网络类型：Windows 命名管道
    /// </summary>
    public const string NamedPipe = "npipe";

    /// <summary>
    /// 握手行格式版本前缀
    /// </summary>
    public const string HandshakeLinePrefix = "xihan-plugin/1";

    /// <summary>
    /// 端点列表的分隔符
    /// </summary>
    /// <remarks>
    /// <c>XIHAN_PLUGIN_HOST_ADDRESS</c> 的各项之间（<c>spec/plugin/handshake.md</c> §4）；列表里的地址不得含它
    /// </remarks>
    public const char ListSeparator = ';';

    /// <summary>
    /// IPv6 回环地址的唯一写法
    /// </summary>
    private const string Ipv6Loopback = "[::1]";

    /// <summary>
    /// 创建端点
    /// </summary>
    /// <exception cref="ArgumentException">网络类型未知、地址为空或含空白，或 TCP 地址不是规范写法的回环地址加端口</exception>
    public PluginEndpoint(string network, string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(network);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        if (!IsValid(network, address))
        {
            throw new ArgumentException($"'{network} {address}' is not a valid plugin endpoint; expected 'unix <path>', 'tcp <loopback>:<port>' or 'npipe <name>'.", nameof(address));
        }

        Network = network;
        Address = address;
    }

    /// <summary>
    /// 网络类型（<see cref="Unix"/>、<see cref="Tcp"/>、<see cref="NamedPipe"/>）
    /// </summary>
    public string Network { get; }

    /// <summary>
    /// 地址：套接字路径、<c>host:port</c> 或管道名
    /// </summary>
    public string Address { get; }

    /// <summary>
    /// 解析插件写到 stdout 的握手行 <c>xihan-plugin/1 &lt;network&gt; &lt;address&gt;</c>
    /// </summary>
    /// <remarks>
    /// <paramref name="line"/> 是去掉行尾之后的行；按整串匹配，结尾的换行同样不合法
    /// </remarks>
    public static bool TryParseHandshakeLine(string? line, [NotNullWhen(true)] out PluginEndpoint? endpoint)
    {
        endpoint = null;
        if (line is null || HandshakeLine().Match(line) is not { Success: true } match)
        {
            return false;
        }

        return TryCreate(match.Groups["network"].Value, match.Groups["address"].Value, out endpoint);
    }

    /// <summary>
    /// 解析单个端点 <c>&lt;network&gt; &lt;address&gt;</c>
    /// </summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out PluginEndpoint? endpoint)
    {
        endpoint = null;
        if (text is null || EndpointText().Match(text) is not { Success: true } match)
        {
            return false;
        }

        return TryCreate(match.Groups["network"].Value, match.Groups["address"].Value, out endpoint);
    }

    /// <summary>
    /// 解析宿主回调端点列表 <c>&lt;network&gt; &lt;address&gt;[;&lt;network&gt; &lt;address&gt;]…</c>
    /// </summary>
    /// <remarks>
    /// <c>XIHAN_PLUGIN_HOST_ADDRESS</c> 的写法（<c>spec/plugin/handshake.md</c> §4）：至少一项，按宿主的偏好排序；每项同 <see cref="TryParse"/>，地址另不得含 <see cref="ListSeparator"/>。任何一项不合法整个值就不合法，空串与 <see langword="null"/> 同样不合法
    /// </remarks>
    public static bool TryParseList(string? text, [NotNullWhen(true)] out IReadOnlyList<PluginEndpoint>? endpoints)
    {
        endpoints = null;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var items = text.Split(ListSeparator);
        var parsed = new PluginEndpoint[items.Length];
        for (var index = 0; index < items.Length; index++)
        {
            // 分隔符只按 ';' 切，项本身仍按单个端点的文法整串匹配：前后的空白、空项都在这里被拒
            if (!TryParse(items[index], out var endpoint))
            {
                return false;
            }

            parsed[index] = endpoint;
        }

        endpoints = parsed;
        return true;
    }

    /// <summary>
    /// 写出宿主回调端点列表
    /// </summary>
    /// <remarks>
    /// 与 <see cref="TryParseList"/> 互逆，顺序即偏好
    /// </remarks>
    /// <exception cref="ArgumentException">列表为空，或某个地址含 <see cref="ListSeparator"/></exception>
    public static string FormatList(IReadOnlyList<PluginEndpoint> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (endpoints.Count == 0)
        {
            throw new ArgumentException("A host address list needs at least one endpoint.", nameof(endpoints));
        }

        foreach (var endpoint in endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoint, nameof(endpoints));
            if (endpoint.Address.Contains(ListSeparator, StringComparison.Ordinal))
            {
                throw new ArgumentException($"The address '{endpoint.Address}' contains ';' and cannot be listed in a host address list.", nameof(endpoints));
            }
        }

        return string.Join(ListSeparator, endpoints.Select(static e => e.ToString()));
    }

    /// <summary>
    /// 握手行文本
    /// </summary>
    public string ToHandshakeLine() => $"{HandshakeLinePrefix} {Network} {Address}";

    /// <inheritdoc />
    public override string ToString() => $"{Network} {Address}";

    /// <summary>
    /// 校验通过时创建端点
    /// </summary>
    private static bool TryCreate(string network, string address, [NotNullWhen(true)] out PluginEndpoint? endpoint)
    {
        endpoint = IsValid(network, address) ? new PluginEndpoint(network, address) : null;
        return endpoint is not null;
    }

    /// <summary>
    /// 网络类型已知且地址合法
    /// </summary>
    /// <remarks>
    /// 地址一律是一个或多个非空白字符；TCP 另要求规范写法的回环地址加端口，其他网络只要求非空
    /// </remarks>
    private static bool IsValid(string network, string address)
    {
        if (address.Length == 0 || address.Any(char.IsWhiteSpace))
        {
            return false;
        }

        return network switch
        {
            Unix or NamedPipe => true,
            Tcp => IsLoopbackTcp(address),
            _ => false,
        };
    }

    /// <summary>
    /// 回环地址加端口的规范写法
    /// </summary>
    /// <remarks>
    /// 不用 <c>IPEndPoint.TryParse</c>：它接受 <c>127.1</c>、<c>0177.0.0.1</c>、<c>[0:0:0:0:0:0:0:1]</c> 与前导零的端口，别的语言写不出同样的宽松规则（ADR-0237）
    /// </remarks>
    private static bool IsLoopbackTcp(string address)
    {
        var colon = address.LastIndexOf(':');
        if (colon <= 0 || !IsPort(address.AsSpan(colon + 1)))
        {
            return false;
        }

        var host = address.AsSpan(0, colon);
        if (host.SequenceEqual(Ipv6Loopback))
        {
            return true;
        }

        // IPv4：四段点分十进制，第一段是 127
        var segments = 0;
        foreach (var range in host.Split('.'))
        {
            var segment = host[range];
            if (++segments > 4 || !IsDecimal(segment, 255) || (segments == 1 && !segment.SequenceEqual("127")))
            {
                return false;
            }
        }

        return segments == 4;
    }

    /// <summary>
    /// 1–65535 的十进制端口
    /// </summary>
    private static bool IsPort(ReadOnlySpan<char> text) => IsDecimal(text, 65535) && !text.SequenceEqual("0");

    /// <summary>
    /// 不带正负号与前导零的十进制数，不超过 <paramref name="max"/>
    /// </summary>
    /// <remarks>
    /// 只认 ASCII 数字：<see cref="char.IsDigit(char)"/> 与 <c>\d</c> 都认其他文字的数字
    /// </remarks>
    private static bool IsDecimal(ReadOnlySpan<char> text, int max)
    {
        if (text.IsEmpty || text.Length > 5 || (text.Length > 1 && text[0] == '0'))
        {
            return false;
        }

        var value = 0;
        foreach (var character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }

            value = (value * 10) + (character - '0');
        }

        return value <= max;
    }

    /// <summary>
    /// 握手行文法
    /// </summary>
    /// <remarks>
    /// <c>\S</c> 即规范的「非空白」：.NET 的 <c>\s</c> 是 U+0009–U+000D、U+0085 与 Unicode 分隔符类（Z），与 <c>handshake.md</c> §2 列出的集合相同
    /// </remarks>
    [GeneratedRegex(@"^xihan-plugin/1 (?<network>unix|tcp|npipe) (?<address>\S+)\z")]
    private static partial Regex HandshakeLine();

    /// <summary>
    /// 单个端点的文法
    /// </summary>
    [GeneratedRegex(@"^(?<network>unix|tcp|npipe) (?<address>\S+)\z")]
    private static partial Regex EndpointText();
}
