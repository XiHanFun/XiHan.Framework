#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IAdditionalModuleAssemblyProvider
// Guid:2d0f89f0-2041-490f-a318-0fa77357cd6a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 8:42:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 附加模块组装提供器接口
/// </summary>
public interface IAdditionalModuleAssemblyProvider
{
    /// <summary>
    /// 获取程序集
    /// </summary>
    /// <returns></returns>
    Assembly[] GetAssemblies();
}
