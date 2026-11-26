// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using XiHan.Framework.Plugins.OutOfProcess.Runtime;

namespace XiHan.Framework.Plugins.OutOfProcess.Callbacks;

/// <summary>
/// 会话令牌 → 插件运行时
/// </summary>
/// <remarks>
/// 每次启动签发一个随机令牌，插件回调时凭它认证（<c>spec/plugin/handshake.md</c> §4）。按令牌的 SHA-256 查会话，再与签发的令牌做常数时间比较（ADR-0325）：令牌本身不作字典的键，查找的耗时与令牌有几个字符对得上无关。一个运行中的插件进程一个会话，进程结束即作废，容量随插件数
/// </remarks>
internal sealed class PluginSessions
{
    /// <summary>
    /// 令牌的 SHA-256（十六进制）→ 会话
    /// </summary>
    private readonly ConcurrentDictionary<string, Session> _sessions = new(StringComparer.Ordinal);

    /// <summary>
    /// 签发并登记一个新令牌（32 字节随机数，base64url）
    /// </summary>
    public string Issue(PluginRuntime runtime)
    {
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        _sessions[KeyOf(token)] = new Session(token, runtime);
        return token;
    }

    /// <summary>
    /// 作废令牌
    /// </summary>
    /// <remarks>
    /// 只作废原令牌对得上的会话；不认识的令牌什么都不动
    /// </remarks>
    public void Revoke(string? token)
    {
        if (token is null)
        {
            return;
        }

        // 比较删：只移除查到的那一个会话，期间同一个键被重新签发（不会发生，散列相同而令牌不同）也不误删
        var key = KeyOf(token);
        if (_sessions.TryGetValue(key, out var session) && session.Matches(token))
        {
            _sessions.TryRemove(KeyValuePair.Create(key, session));
        }
    }

    /// <summary>
    /// 按令牌找运行时
    /// </summary>
    public bool TryResolve(string? token, out PluginRuntime runtime)
    {
        // 字典按散列找候选，散列的比较泄露不了令牌：对方定不了散列的前缀；对得上的候选再与原令牌常数时间比较
        if (token is not null && _sessions.TryGetValue(KeyOf(token), out var session) && session.Matches(token))
        {
            runtime = session.Runtime;
            return true;
        }

        runtime = null!;
        return false;
    }

    /// <summary>
    /// 令牌的查找键：UTF-16 码元的 SHA-256，十六进制
    /// </summary>
    /// <remarks>
    /// 按码元取字节，不经编码：元数据里交来的任何字符串都有确定的键，孤立的代理项也不会被换成别的字符
    /// </remarks>
    private static string KeyOf(string token)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(MemoryMarshal.AsBytes(token.AsSpan()), hash);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// 一个会话：签发的令牌与它的运行时
    /// </summary>
    /// <param name="Token">签发的令牌</param>
    /// <param name="Runtime">插件运行时</param>
    private sealed record Session(string Token, PluginRuntime Runtime)
    {
        /// <summary>
        /// 交来的令牌是否就是签发的令牌
        /// </summary>
        /// <remarks>
        /// 常数时间比较：耗时只取决于长度，不取决于从哪个字符起不同（<c>handshake.md</c> §4）
        /// </remarks>
        public bool Matches(string token)
            => CryptographicOperations.FixedTimeEquals(MemoryMarshal.AsBytes(Token.AsSpan()), MemoryMarshal.AsBytes(token.AsSpan()));
    }
}
