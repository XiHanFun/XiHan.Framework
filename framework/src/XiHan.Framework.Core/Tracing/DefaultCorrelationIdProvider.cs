#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultCorrelationIdProvider
// Guid:6f4cd27e-f2de-4e60-a06a-5f7a4674101d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 08:04:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
