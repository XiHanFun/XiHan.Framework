// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 服务激活上下文接口
/// </summary>
public interface IOnServiceActivatedContext
{
    /// <summary>
    /// 实例
    /// </summary>
    public object Instance { get; }
}
