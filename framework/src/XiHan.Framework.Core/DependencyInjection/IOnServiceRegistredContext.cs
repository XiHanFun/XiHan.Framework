#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IOnServiceRegistredContext
// Guid:ba3d16d3-8bd0-4f79-b3ec-b55006c91beb
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 1:33:51
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
    ITypeList<IInterceptor> Interceptors { get; }

    /// <summary>
    /// 实现类型
    /// </summary>
    Type ImplementationType { get; }
}