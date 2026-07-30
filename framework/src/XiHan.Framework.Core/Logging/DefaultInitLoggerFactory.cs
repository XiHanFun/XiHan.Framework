// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Core.Logging;

/// <summary>
/// 默认初始化日志工厂
/// </summary>
public sealed class DefaultInitLoggerFactory : IInitLoggerFactory
{
    private readonly Dictionary<Type, object> _cache = [];

    /// <summary>
    /// 创建初始化日志
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public IInitLogger<T> Create<T>()
    {
        return (IInitLogger<T>)_cache.GetOrAdd(typeof(T), () => new DefaultInitLogger<T>());
    }
}
