// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Bot.WeCom.Tests;

/// <summary>
/// 企业微信出站请求测试集合
/// </summary>
/// <remarks>
/// 出站请求经由 <c>StringHttpExtensions</c> 的进程级静态服务定位器分发，
/// HTTP 替身与其记录的 URL/请求体是进程共享状态；并行执行会互相覆盖记录导致随机失败，
/// 因此凡是会发出站请求的测试类都归入本集合，保证类间串行。
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public class WeComHttpCollection
{
    /// <summary>
    /// 集合名称
    /// </summary>
    public const string Name = "WeComHttp";
}
