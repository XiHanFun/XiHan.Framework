#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDependedTypesProvider
// Guid:53070883-9912-4e53-afcd-9afa34d8decc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 8:07:34
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 依赖类型提供器接口
/// </summary>
public interface IDependedTypesProvider
{
    /// <summary>
    /// 获取依赖类型
    /// </summary>
    /// <returns></returns>
    Type[] GetDependedTypes();
}