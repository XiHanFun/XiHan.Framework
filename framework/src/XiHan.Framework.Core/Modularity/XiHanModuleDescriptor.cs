#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanModuleDescriptor
// Guid:6897f6d6-0d13-4743-a31f-f7a417b38406
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 18:48:15
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;
using XiHan.Framework.Utils.Collections;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 曦寒模块描述器
/// </summary>
public class XiHanModuleDescriptor : IModuleDescriptor
{
    private readonly List<IModuleDescriptor> _dependencies;

    /// <summary>
    /// 模块类
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// 模块的主程序集
    /// </summary>
    public Assembly Assembly { get; }

    /// <summary>
    /// 模块的所有组件
    /// 包括在模块 Type 上使用 AdditionalAssemblyAttribute 属性标记的主程序集和其他已定义的程序集
    /// </summary>
    public Assembly[] AllAssemblies { get; }

    /// <summary>
    /// 曦寒模块类的实例(单例)
    /// </summary>
    public IXiHanModule Instance { get; }

    /// <summary>
    /// 该模块是否作为插件加载
    /// </summary>
    public bool IsLoadedAsPlugIn { get; }

    /// <summary>
    /// 此模块所依赖的模块
    /// 一个模块可以通过<see cref="DependsOnAttribute"/>属性依赖于另一个模块
    /// </summary>
    public IReadOnlyList<IModuleDescriptor> Dependencies => [.. _dependencies];

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="type"></param>
    /// <param name="instance"></param>
    /// <param name="isLoadedAsPlugIn"></param>
    /// <exception cref="ArgumentException"></exception>
    public XiHanModuleDescriptor(Type type, IXiHanModule instance, bool isLoadedAsPlugIn)
    {
        _ = CheckHelper.NotNull(type, nameof(type));
        _ = CheckHelper.NotNull(instance, nameof(instance));
        XiHanModuleHelper.CheckXiHanModuleType(type);

        if (!type.GetTypeInfo().IsInstanceOfType(instance))
        {
            throw new ArgumentException($"模块实例({instance.GetType().AssemblyQualifiedName})不是模块类型{type.AssemblyQualifiedName}的实例！");
        }

        Type = type;
        Assembly = type.Assembly;
        AllAssemblies = XiHanModuleHelper.GetAllAssemblies(type);
        Instance = instance;
        IsLoadedAsPlugIn = isLoadedAsPlugIn;

        _dependencies = [];
    }

    /// <summary>
    /// 添加依赖项
    /// </summary>
    /// <param name="descriptor"></param>
    public void AddDependency(IModuleDescriptor descriptor)
    {
        _ = _dependencies.AddIfNotContains(descriptor);
    }

    /// <summary>
    /// 字符串表示形式
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return $"[XiHanModuleDescriptor {Type.FullName}]";
    }
}
