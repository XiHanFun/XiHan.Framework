// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Pipeline;

namespace XiHan.Framework.Bot.Tests.Fakes;

/// <summary>
/// 记录进出顺序的管道替身
/// </summary>
/// <remarks>
/// 用于验证 <c>BotDispatcher</c> 的管道包裹顺序：注册顺序即外层到内层的顺序。
/// </remarks>
public sealed class RecordingPipeline : IBotPipeline
{
    private readonly string _name;
    private readonly List<string> _trace;
    private readonly bool _shortCircuit;

    /// <summary>
    /// 创建记录管道
    /// </summary>
    /// <param name="name">管道名称</param>
    /// <param name="trace">共享的调用轨迹</param>
    /// <param name="shortCircuit">是否短路（置 IsSkipped 且不调用 next）</param>
    public RecordingPipeline(string name, List<string> trace, bool shortCircuit = false)
    {
        _name = name;
        _trace = trace;
        _shortCircuit = shortCircuit;
    }

    /// <summary>
    /// 执行管道
    /// </summary>
    /// <param name="context">调度上下文</param>
    /// <param name="next">下一个环节</param>
    public async Task InvokeAsync(BotContext context, Func<Task> next)
    {
        _trace.Add(_name + ":enter");

        if (_shortCircuit)
        {
            context.IsSkipped = true;
            _trace.Add(_name + ":skip");
            return;
        }

        await next();
        _trace.Add(_name + ":exit");
    }
}
