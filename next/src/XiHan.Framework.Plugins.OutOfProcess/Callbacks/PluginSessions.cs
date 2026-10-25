// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using XiHan.Framework.Plugins.OutOfProcess.Runtime;

namespace XiHan.Framework.Plugins.OutOfProcess.Callbacks;

/// <summary>
/// 会话令牌 → 插件运行时：每次启动签发一个随机令牌，插件回调时凭它认证（<c>spec/plugin/handshake.md</c> §4）。
/// </summary>
internal sealed class PluginSessions
{
    /// <summary>
    /// 令牌 → 运行时。
    /// </summary>
    private readonly ConcurrentDictionary<string, PluginRuntime> _sessions = new(StringComparer.Ordinal);

    /// <summary>
    /// 签发并登记一个新令牌（32 字节随机数，base64url）。
    /// </summary>
    public string Issue(PluginRuntime runtime)
    {
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        _sessions[token] = runtime;
        return token;
    }

    /// <summary>
    /// 作废令牌。
    /// </summary>
    public void Revoke(string? token)
    {
        if (token is not null)
        {
            _sessions.TryRemove(token, out _);
        }
    }

    /// <summary>
    /// 按令牌找运行时。
    /// </summary>
    public bool TryResolve(string? token, out PluginRuntime runtime)
    {
        if (token is not null && _sessions.TryGetValue(token, out var found))
        {
            runtime = found;
            return true;
        }

        runtime = null!;
        return false;
    }
}
