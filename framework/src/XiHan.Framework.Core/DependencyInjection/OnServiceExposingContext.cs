#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OnServiceExposingContext
// Guid:ffda0073-f032-417b-872d-2df48528b232
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 1:40:32
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 服务暴露时上下文
/// </summary>
public class OnServiceExposingContext : IOnServiceExposingContext
{
    /// <summary>
    /// 实现类型
    /// </summary>
    public Type ImplementationType { get; }

    /// <summary>
    /// 暴露的类型
    /// </summary>
    public List<ServiceIdentifier> ExposedTypes { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="implementationType"></param>
    /// <param name="exposedTypes"></param>
    public OnServiceExposingContext(Type implementationType, List<Type> exposedTypes)
    {
        ImplementationType = CheckHelper.NotNull(implementationType, nameof(implementationType));
        ExposedTypes = CheckHelper.NotNull(exposedTypes, nameof(exposedTypes)).ConvertAll(t => new ServiceIdentifier(t));
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="implementationType"></param>
    /// <param name="exposedTypes"></param>
    public OnServiceExposingContext(Type implementationType, List<ServiceIdentifier> exposedTypes)
    {
        ImplementationType = CheckHelper.NotNull(implementationType, nameof(implementationType));
        ExposedTypes = CheckHelper.NotNull(exposedTypes, nameof(exposedTypes));
    }
}
