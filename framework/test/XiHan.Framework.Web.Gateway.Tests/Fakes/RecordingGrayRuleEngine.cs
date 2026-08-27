// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Models;

namespace XiHan.Framework.Web.Gateway.Tests;

/// <summary>
/// 记录调用参数的灰度规则引擎替身
/// </summary>
/// <remarks>
/// 网关中间件的职责只有「构建灰度上下文 -> 调用引擎 -> 把决策塞进 HttpContext」，
/// 真实规则匹配属于 Traffic 项目的契约，这里用替身把引擎输入输出完全固定下来。
/// </remarks>
public sealed class RecordingGrayRuleEngine : IGrayRuleEngine
{
    private readonly IGrayDecision _decision;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="decision">固定返回的决策；传空表示返回未命中灰度</param>
    public RecordingGrayRuleEngine(IGrayDecision? decision = null)
    {
        _decision = decision ?? GrayDecision.NotGray("测试替身默认未命中");
    }

    /// <summary>
    /// 被调用次数
    /// </summary>
    public int CallCount { get; private set; }

    /// <summary>
    /// 最近一次收到的灰度上下文
    /// </summary>
    public GrayContext? LastContext { get; private set; }

    /// <summary>
    /// 最近一次收到的取消令牌
    /// </summary>
    public CancellationToken LastCancellationToken { get; private set; }

    /// <summary>
    /// 执行灰度决策
    /// </summary>
    public Task<IGrayDecision> DecideAsync(GrayContext context, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastContext = context;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(_decision);
    }
}
