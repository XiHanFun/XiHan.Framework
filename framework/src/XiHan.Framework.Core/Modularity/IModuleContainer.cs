#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IModuleContainer
// Guid:86a629bf-19b3-4975-a0dc-c6428499dbb7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 18:55:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 模块容器接口
/// </summary>
public interface IModuleContainer
{
    /// <summary>
    /// 模块列表
    /// </summary>
    IReadOnlyList<IModuleDescriptor> Modules { get; }
}
