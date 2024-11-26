#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IOnServiceExposingContext
// Guid:20583ae5-fee2-46cc-97ef-6f6bce9b5c99
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 1:32:04
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 服务暴露时上下文接口
/// </summary>
public interface IOnServiceExposingContext
{
    /// <summary>
    /// 服务实现类型
    /// </summary>
    Type ImplementationType { get; }

    /// <summary>
    /// 暴露的服务类型
    /// </summary>
    List<ServiceIdentifier> ExposedTypes { get; }
}