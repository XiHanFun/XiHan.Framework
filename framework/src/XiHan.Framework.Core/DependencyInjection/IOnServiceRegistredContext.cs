// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Collections;
using XiHan.Framework.Core.DynamicProxy;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 服务注册上下文接口
/// </summary>
public interface IOnServiceRegistredContext
{
    /// <summary>
    /// 服务拦截器列表
    /// </summary>
    ITypeList<IXiHanInterceptor> Interceptors { get; }

    /// <summary>
    /// 实现类型
    /// </summary>
    Type ImplementationType { get; }
}
