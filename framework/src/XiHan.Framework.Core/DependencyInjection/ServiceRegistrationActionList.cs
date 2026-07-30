// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 注册服务时的操作列表
/// </summary>
public class ServiceRegistrationActionList : List<Action<IOnServiceRegistredContext>>
{
    /// <summary>
    /// 是否禁用类拦截器
    /// </summary>
    public bool IsClassInterceptorsDisabled { get; set; }
}
