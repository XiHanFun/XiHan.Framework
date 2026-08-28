// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Models;

namespace XiHan.Framework.Traffic.Tests.Fakes;

/// <summary>
/// 恒定返回「未命中灰度」的规则引擎替身
/// </summary>
/// <remarks>
/// 用于验证 AddGrayRouting 的 TryAddSingleton 语义：宿主先注册的引擎实现不应被默认实现顶掉。
/// </remarks>
public sealed class FakeGrayRuleEngine : IGrayRuleEngine
{
    /// <summary>
    /// 执行灰度决策
    /// </summary>
    public Task<IGrayDecision> DecideAsync(GrayContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IGrayDecision>(GrayDecision.NotGray("替身引擎"));
    }
}
