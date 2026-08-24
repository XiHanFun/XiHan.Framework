// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace XiHan.Framework.Authentication.OAuth.Handlers;

/// <summary>
/// 微信系网页授权页的状态串搬运
/// </summary>
/// <remarks>
/// 微信公众号与企业微信应用内的网页授权页限制 state 长度，容不下受保护的认证属性，
/// 因此把真实状态挪进回调地址的查询参数，state 位上只留一个哨兵，回调时再还原。
/// </remarks>
internal static class WeixinShortState
{
    /// <summary>
    /// 状态串挪进回调地址时使用的参数名
    /// </summary>
    public const string ShortStateKey = "_oauthstate";

    private const string StateKey = "state";

    /// <summary>
    /// 把状态串挪进回调地址
    /// </summary>
    /// <param name="redirectUri">回调地址</param>
    /// <param name="state">受保护的状态串</param>
    /// <returns>改写后的回调地址与哨兵状态串</returns>
    public static (string RedirectUri, string State) Apply(string redirectUri, string state)
    {
        return (QueryHelpers.AddQueryString(redirectUri, ShortStateKey, state), ShortStateKey);
    }

    /// <summary>
    /// 把回调地址上的状态串还原到 state 参数
    /// </summary>
    /// <param name="request">回调请求</param>
    public static void Restore(HttpRequest request)
    {
        if (!request.Query.TryGetValue(ShortStateKey, out var shortState))
        {
            return;
        }

        var restored = new List<KeyValuePair<string, string?>>();
        foreach (var item in request.Query)
        {
            if (item.Key is ShortStateKey or StateKey)
            {
                continue;
            }

            restored.AddRange(item.Value.Select(value => new KeyValuePair<string, string?>(item.Key, value)));
        }

        restored.Add(new KeyValuePair<string, string?>(StateKey, shortState.ToString()));
        request.QueryString = QueryString.Create(restored);
    }
}
