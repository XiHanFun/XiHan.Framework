#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IInjectPropertiesService
// Guid:8c7cd08f-c083-4d28-8e43-19ec3f7ed39b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/27 21:57:13
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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