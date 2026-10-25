// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Protocol.Handshake;

/// <summary>
/// 一个本机 gRPC 端点：Unix 域套接字、回环 TCP 或 Windows 命名管道（<c>spec/plugin/handshake.md</c>）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial record PluginEndpoint
{
    /// <summary>
    /// 网络类型：Unix 域套接字。
    /// </summary>
    public const string Unix = "unix";

    /// <summary>
    /// 网络类型：回环 TCP。
    /// </summary>
    public const string Tcp = "tcp";

    /// <summary>
    /// 网络类型：Windows 命名管道。
    /// </summary>
    public const string NamedPipe = "npipe";

    /// <summary>
    /// 握手行格式版本前缀。
    /// </summary>
    public const string HandshakeLinePrefix = "xihan-plugin/1";

    /// <summary>
    /// 创建端点。
    /// </summary>
    /// <exception cref="ArgumentException">网络类型未知、地址为空，或 TCP 地址不是回环地址。</exception>
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
    /// 网络类型（<see cref="Unix"/>、<see cref="Tcp"/>、<see cref="NamedPipe"/>）。
    /// </summary>
    public string Network { get; }

    /// <summary>
    /// 地址：套接字路径、<c>host:port</c> 或管道名。
    /// </summary>
    public string Address { get; }

    /// <summary>
    /// 解析插件写到 stdout 的握手行 <c>xihan-plugin/1 &lt;network&gt; &lt;address&gt;</c>。
    /// </summary>
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
    /// 解析环境变量形式 <c>&lt;network&gt; &lt;address&gt;</c>。
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
    /// 握手行文本。
    /// </summary>
    public string ToHandshakeLine() => $"{HandshakeLinePrefix} {Network} {Address}";

    /// <inheritdoc />
    public override string ToString() => $"{Network} {Address}";

    /// <summary>
    /// 校验通过时创建端点。
    /// </summary>
    private static bool TryCreate(string network, string address, [NotNullWhen(true)] out PluginEndpoint? endpoint)
    {
        endpoint = IsValid(network, address) ? new PluginEndpoint(network, address) : null;
        return endpoint is not null;
    }

    /// <summary>
    /// 网络类型已知且地址合法；TCP 只接受带端口的回环地址，其他网络只要求地址非空。
    /// </summary>
    private static bool IsValid(string network, string address) => network switch
    {
        Unix or NamedPipe => !string.IsNullOrWhiteSpace(address),
        Tcp => IPEndPoint.TryParse(address, out var ip) && ip.Port > 0 && IPAddress.IsLoopback(ip.Address),
        _ => false,
    };

    /// <summary>
    /// 握手行文法。
    /// </summary>
    [GeneratedRegex(@"^xihan-plugin/1 (?<network>unix|tcp|npipe) (?<address>\S+)$")]
    private static partial Regex HandshakeLine();

    /// <summary>
    /// 环境变量形式的文法。
    /// </summary>
    [GeneratedRegex(@"^(?<network>unix|tcp|npipe) (?<address>\S+)$")]
    private static partial Regex EndpointText();
}
