// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Authentication;

namespace XiHan.Framework.Hosting.AspNetCore;

/// <summary>
/// 401 响应的 <c>WWW-Authenticate</c> 质询（RFC 7235 §3.1 要求 401 至少带一个）。
/// 凭据无效（<see cref="SecurityErrors.InvalidCredential"/>）时只质询被拒的那个方案：Bearer 带 <c>error="invalid_token"</c>（RFC 6750 §3.1，过期、签名不符、格式错都归此类，不细分原因），其他方案只写方案名；
/// 未认证（<see cref="SecurityErrors.NotAuthenticated"/>，请求没有凭据）时按已装配认证处理器的方案逐个质询，不带错误码（RFC 6750 §3）。
/// </summary>
internal static class AuthenticationChallenge
{
    /// <summary>
    /// Bearer 方案名。
    /// </summary>
    private const string BearerScheme = "Bearer";

    /// <summary>
    /// 计算质询；不是认证失败、或没有可质询的方案时为空。
    /// </summary>
    /// <param name="error">响应的错误。</param>
    /// <param name="presented">请求出示的凭据；没有为无值。</param>
    /// <param name="schemes">已装配认证处理器的方案名，按登记顺序。</param>
    public static IReadOnlyList<string> For(Error error, Maybe<AuthenticationCredential> presented, IEnumerable<string> schemes)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(schemes);
        if (!ProblemResponse.IsAuthenticationFailure(error))
        {
            return [];
        }

        // 方案名不区分大小写：按处理器登记的写法输出（出示的凭据可能写成 bearer）
        var registered = schemes.Where(static s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (string.Equals(error.Code, SecurityErrors.InvalidCredential, StringComparison.Ordinal) && presented.TryGetValue(out var credential))
        {
            var scheme = registered.FirstOrDefault(s => string.Equals(s, credential.Scheme, StringComparison.OrdinalIgnoreCase)) ?? credential.Scheme;
            return [string.Equals(scheme, BearerScheme, StringComparison.OrdinalIgnoreCase) ? $"{scheme} error=\"invalid_token\"" : scheme];
        }

        return registered;
    }
}
