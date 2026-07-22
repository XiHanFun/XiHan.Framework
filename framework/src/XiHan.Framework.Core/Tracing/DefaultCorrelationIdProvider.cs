// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Utils.Threading;

namespace XiHan.Framework.Core.Tracing;

/// <summary>
/// 默认关联标识符提供程序
/// </summary>
public class DefaultCorrelationIdProvider : ICorrelationIdProvider, ISingletonDependency
{
    private readonly AsyncLocal<string?> _currentCorrelationId = new();

    private string? CorrelationId => _currentCorrelationId.Value;

    /// <summary>
    /// 获取当前的关联标识符
    /// </summary>
    /// <returns></returns>
    public virtual string? Get()
    {
        // 优先 W3C：与 Activity 对齐，EventBus 等传播链零改造即携带同一 trace id；无 Activity 时回退显式设置的关联 id
        var current = Activity.Current;
        if (current is not null && current.TraceId != default)
        {
            return current.TraceId.ToHexString();
        }

        return CorrelationId;
    }

    /// <summary>
    /// 临时更改关联标识符
    /// </summary>
    /// <param name="correlationId"></param>
    /// <returns></returns>
    public virtual IDisposable Change(string? correlationId)
    {
        var parent = CorrelationId;
        _currentCorrelationId.Value = correlationId;
        return new DisposeAction(() =>
        {
            _currentCorrelationId.Value = parent;
        });
    }
}
