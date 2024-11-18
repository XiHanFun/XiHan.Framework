#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SimpleStateCheckerManager
// Guid:884a767a-5e97-423a-80c0-4e5575976f91
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/24 23:17:30
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Utils.Extensions.System.Collections.Generic;

namespace XiHan.Framework.Core.SimpleStateChecking;

/// <summary>
/// 简单状态检查器管理器
/// </summary>
public class SimpleStateCheckerManager<TState> : ISimpleStateCheckerManager<TState>
    where TState : IHasSimpleStateCheckers<TState>
{
    /// <summary>
    /// 服务提供程序
    /// </summary>
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 选项
    /// </summary>
    protected SimpleStateCheckerOptions<TState> Options { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="options"></param>
    public SimpleStateCheckerManager(IServiceProvider serviceProvider, IOptions<SimpleStateCheckerOptions<TState>> options)
    {
        ServiceProvider = serviceProvider;
        Options = options.Value;
    }

    /// <summary>
    /// 是否启用
    /// </summary>
    /// <param name="state"></param>
    /// <returns></returns>
    public virtual async Task<bool> IsEnabledAsync(TState state)
    {
        return await InternalIsEnabledAsync(state, true);
    }

    /// <summary>
    /// 是否启用
    /// </summary>
    /// <param name="states"></param>
    /// <returns></returns>
    public virtual async Task<SimpleStateCheckerResult<TState>> IsEnabledAsync(TState[] states)
    {
        SimpleStateCheckerResult<TState>? result = new(states);

        using var scope = ServiceProvider.CreateScope();
        var batchStateCheckers = states.SelectMany(x => x.StateCheckers)
            .Where(x => x is ISimpleBatchStateChecker<TState>)
            .Cast<ISimpleBatchStateChecker<TState>>()
            .GroupBy(x => x)
            .Select(x => x.Key);

        foreach (var stateChecker in batchStateCheckers)
        {
            SimpleBatchStateCheckerContext<TState>? context = new(
                scope.ServiceProvider.GetRequiredService<ICachedServiceProvider>(),
                states.Where(x => x.StateCheckers.Contains(stateChecker)).ToArray());

            foreach (var x in await stateChecker.IsEnabledAsync(context))
            {
                result[x.Key] = x.Value;
            }

            if (result.Values.All(x => !x))
            {
                return result;
            }
        }

        foreach (var globalStateChecker in Options.GlobalStateCheckers
            .Where(x => typeof(ISimpleBatchStateChecker<TState>).IsAssignableFrom(x))
            .Select(ServiceProvider.GetRequiredService).Cast<ISimpleBatchStateChecker<TState>>())
        {
            SimpleBatchStateCheckerContext<TState>? context = new(
                scope.ServiceProvider.GetRequiredService<ICachedServiceProvider>(),
                states.Where(x => result.Any(y => y.Key.Equals(x) && y.Value)).ToArray());

            foreach (var x in await globalStateChecker.IsEnabledAsync(context))
            {
                result[x.Key] = x.Value;
            }
        }

        foreach (var state in states)
        {
            if (result[state])
            {
                result[state] = await InternalIsEnabledAsync(state, false);
            }
        }

        return result;
    }

    /// <summary>
    /// 集成是否启用
    /// </summary>
    /// <param name="state"></param>
    /// <param name="useBatchChecker"></param>
    /// <returns></returns>
    protected virtual async Task<bool> InternalIsEnabledAsync(TState state, bool useBatchChecker)
    {
        using var scope = ServiceProvider.CreateScope();
        SimpleStateCheckerContext<TState>? context = new(scope.ServiceProvider.GetRequiredService<ICachedServiceProvider>(), state);

        foreach (var provider in state.StateCheckers.WhereIf(!useBatchChecker, x => x is not ISimpleBatchStateChecker<TState>))
        {
            if (!await provider.IsEnabledAsync(context))
            {
                return false;
            }
        }

        foreach (var provider in Options.GlobalStateCheckers
            .WhereIf(!useBatchChecker, x => !typeof(ISimpleBatchStateChecker<TState>).IsAssignableFrom(x))
            .Select(ServiceProvider.GetRequiredService).Cast<ISimpleStateChecker<TState>>())
        {
            if (!await provider.IsEnabledAsync(context))
            {
                return false;
            }
        }

        return true;
    }
}
