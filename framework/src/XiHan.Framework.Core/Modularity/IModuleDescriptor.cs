#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IModuleDescriptor
// Guid:05eb7550-68d6-4ff9-b6ac-3f96ea153ab1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 8:05:23
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 模块描述器接口
/// </summary>
public interface IModuleDescriptor
{
    /// <summary>
    /// 模块类
    /// </summary>
    Type Type { get; }

    /// <summary>
    /// 模块的主程序集
    /// </summary>
    Assembly Assembly { get; }

    /// <summary>
    /// 模块的所有组件
    /// 包括在模块 Type 上使用 AdditionalAssemblyAttribute 属性标记的主程序集和其他已定义的程序集
    /// </summary>
    Assembly[] AllAssemblies { get; }

    /// <summary>
    /// 曦寒模块类的实例(单例)
    /// </summary>
    IXiHanModule Instance { get; }

    /// <summary>
    /// 该模块是否作为插件加载
    /// </summary>
    bool IsLoadedAsPlugIn { get; }

    /// <summary>
    /// 此模块所依赖的模块
    /// 一个模块可以通过<see cref="DependsOnAttribute"/>属性依赖于另一个模块
    /// </summary>
    IReadOnlyList<IModuleDescriptor> Dependencies { get; }
}
