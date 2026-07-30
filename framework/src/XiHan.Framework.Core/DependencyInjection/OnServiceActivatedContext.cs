// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 服务激活时的上下文
/// </summary>
public class OnServiceActivatedContext : IOnServiceActivatedContext
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="instance"></param>
    public OnServiceActivatedContext(object instance)
    {
        Instance = instance;
    }

    /// <summary>
    /// 服务实例
    /// </summary>
    public object Instance { get; }
}
