// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 属性注入服务接口
/// </summary>
public interface IInjectPropertiesService
{
    /// <summary>
    /// 注入属性
    /// </summary>
    TService InjectProperties<TService>(TService instance) where TService : notnull;

    /// <summary>
    /// 注入未设置的属性
    /// </summary>
    TService InjectUnsetProperties<TService>(TService instance) where TService : notnull;
}
