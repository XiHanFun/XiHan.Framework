// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore.Authentication;

/// <summary>
/// 认证方案 <c>XiHan</c> 的选项。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InvocationAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// 接受查询串令牌（<c>access_token</c>）的路径前缀。
    /// </summary>
    /// <remarks>
    /// 相对 PathBase、按路径段匹配、不区分大小写。不能为空也不能是 <c>/</c>。只给带不了请求头的客户端（浏览器的 WebSocket 与 EventSource）用：查询串会进入访问日志与代理日志。缺省为空，即只认 <c>Authorization</c> 头。
    /// </remarks>
    public IList<PathString> QueryTokenPaths { get; } = [];

    /// <summary>
    /// 路径是否落在某个查询串令牌路径前缀下。
    /// </summary>
    public bool AcceptsQueryToken(PathString path)
    {
        foreach (var prefix in QueryTokenPaths)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public override void Validate()
    {
        base.Validate();
        foreach (var prefix in QueryTokenPaths)
        {
            if (!prefix.HasValue || prefix.Value == "/")
            {
                throw new ArgumentException($"A query token path must be a non-root path such as '/hubs', but was '{prefix}'.");
            }
        }
    }
}
