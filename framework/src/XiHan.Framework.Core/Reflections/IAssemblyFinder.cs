#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IAssemblyFinder
// Guid:0fe8f1a1-8f08-4462-9359-baa9e4529a80
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/04/24 23:02:53
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;

namespace XiHan.Framework.Core.Reflections;

/// <summary>
/// 程序集查找器接口
/// </summary>
public interface IAssemblyFinder
{
    /// <summary>
    /// 获取程序集
    /// </summary>
    IReadOnlyList<Assembly> Assemblies { get; }
}
