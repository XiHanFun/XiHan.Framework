// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

namespace XiHan.Framework.Threading;

/// <summary>
/// 异步本地环境数据上下文
/// </summary>
public class AsyncLocalAmbientDataContext : IAmbientDataContext, ISingletonDependency
{
    private static readonly ConcurrentDictionary<string, AsyncLocal<object?>> AsyncLocalDictionary = new();

    /// <summary>
    /// 设置数据
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public void SetData(string key, object? value)
    {
        var asyncLocal = AsyncLocalDictionary.GetOrAdd(key, k => new AsyncLocal<object?>());
        asyncLocal.Value = value;
    }

    /// <summary>
    /// 获取数据
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public object? GetData(string key)
    {
        var asyncLocal = AsyncLocalDictionary.GetOrAdd(key, k => new AsyncLocal<object?>());
        return asyncLocal.Value;
    }
}
