#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ServiceRegistrationActionList
// Guid:fbf7e46c-a2a8-4cae-bf7d-305c8394eebb
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 01:38:41
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
