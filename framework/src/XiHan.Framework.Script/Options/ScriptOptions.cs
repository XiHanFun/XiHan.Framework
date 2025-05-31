#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptOptions
// Guid:b090a641-b57e-494b-bd01-a354f23815c6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/31 6:08:31
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.CodeAnalysis;
using System.Reflection;
using XiHan.Framework.Script.Enums;

namespace XiHan.Framework.Script.Options;

/// <summary>
/// 脚本选项配置
/// </summary>
public class ScriptOptions
{
    /// <summary>
    /// 默认脚本选项
    /// </summary>
    public static ScriptOptions Default => new();

    /// <summary>
    /// 引用的程序集
    /// </summary>
    public List<Assembly> References { get; set; } = [];

    /// <summary>
    /// 引用的程序集路径
    /// </summary>
    public List<string> ReferencePaths { get; set; } = [];

    /// <summary>
    /// 导入的命名空间
    /// </summary>
    public List<string> Imports { get; set; } = [
        "System",
        "System.Collections.Generic",
        "System.Linq",
        "System.Text",
        "System.Threading.Tasks"
    ];

    /// <summary>
    /// 全局变量
    /// </summary>
    public Dictionary<string, object?> Globals { get; set; } = [];

    /// <summary>
    /// 脚本类型
    /// </summary>
    public ScriptType ScriptType { get; set; } = ScriptType.Statement;

    /// <summary>
    /// 编译器选项
    /// </summary>
    public CompilerOptions CompilerOptions { get; set; } = new();

    /// <summary>
    /// 是否启用缓存
    /// </summary>
    public bool EnableCache { get; set; } = true;

    /// <summary>
    /// 缓存键
    /// </summary>
    public string? CacheKey { get; set; }

    /// <summary>
    /// 超时时间（毫秒）
    /// </summary>
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>
    /// 是否允许不安全代码
    /// </summary>
    public bool AllowUnsafe { get; set; } = false;

    /// <summary>
    /// 优化等级
    /// </summary>
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Debug;

    /// <summary>
    /// 输出类型
    /// </summary>
    public OutputKind OutputKind { get; set; } = OutputKind.DynamicallyLinkedLibrary;

    /// <summary>
    /// 平台目标
    /// </summary>
    public Platform Platform { get; set; } = Platform.AnyCpu;

    /// <summary>
    /// 安全选项
    /// </summary>
    public SecurityOptions SecurityOptions { get; set; } = new();

    /// <summary>
    /// 添加程序集引用
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <returns>当前选项实例</returns>
    public ScriptOptions AddReference(Assembly assembly)
    {
        References.Add(assembly);
        return this;
    }

    /// <summary>
    /// 添加程序集引用
    /// </summary>
    /// <param name="assemblyPath">程序集路径</param>
    /// <returns>当前选项实例</returns>
    public ScriptOptions AddReference(string assemblyPath)
    {
        ReferencePaths.Add(assemblyPath);
        return this;
    }

    /// <summary>
    /// 添加类型引用
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>当前选项实例</returns>
    public ScriptOptions AddReference(Type type)
    {
        References.Add(type.Assembly);
        return this;
    }

    /// <summary>
    /// 添加命名空间导入
    /// </summary>
    /// <param name="namespace">命名空间</param>
    /// <returns>当前选项实例</returns>
    public ScriptOptions AddImport(string @namespace)
    {
        Imports.Add(@namespace);
        return this;
    }

    /// <summary>
    /// 添加全局变量
    /// </summary>
    /// <param name="name">变量名</param>
    /// <param name="value">变量值</param>
    /// <returns>当前选项实例</returns>
    public ScriptOptions AddGlobal(string name, object? value)
    {
        Globals[name] = value;
        return this;
    }

    /// <summary>
    /// 设置脚本类型
    /// </summary>
    /// <param name="scriptType">脚本类型</param>
    /// <returns>当前选项实例</returns>
    public ScriptOptions WithScriptType(ScriptType scriptType)
    {
        ScriptType = scriptType;
        return this;
    }

    /// <summary>
    /// 设置超时时间
    /// </summary>
    /// <param name="timeoutMs">超时时间（毫秒）</param>
    /// <returns>当前选项实例</returns>
    public ScriptOptions WithTimeout(int timeoutMs)
    {
        TimeoutMs = timeoutMs;
        return this;
    }

    /// <summary>
    /// 设置缓存键
    /// </summary>
    /// <param name="cacheKey">缓存键</param>
    /// <returns>当前选项实例</returns>
    public ScriptOptions WithCacheKey(string cacheKey)
    {
        CacheKey = cacheKey;
        return this;
    }

    /// <summary>
    /// 禁用缓存
    /// </summary>
    /// <returns>当前选项实例</returns>
    public ScriptOptions DisableCache()
    {
        EnableCache = false;
        return this;
    }

    /// <summary>
    /// 启用优化
    /// </summary>
    /// <returns>当前选项实例</returns>
    public ScriptOptions WithOptimization()
    {
        OptimizationLevel = OptimizationLevel.Release;
        return this;
    }

    /// <summary>
    /// 启用不安全代码
    /// </summary>
    public ScriptOptions WithUnsafe()
    {
        AllowUnsafe = true;
        return this;
    }

    /// <summary>
    /// 配置安全选项
    /// </summary>
    /// <param name="configure">安全选项配置</param>
    public ScriptOptions WithSecurity(Action<SecurityOptions> configure)
    {
        configure(SecurityOptions);
        return this;
    }

    /// <summary>
    /// 启用严格安全模式
    /// </summary>
    public ScriptOptions WithStrictSecurity()
    {
        SecurityOptions.EnableStrictMode = true;
        SecurityOptions.AllowFileSystemAccess = false;
        SecurityOptions.AllowNetworkAccess = false;
        SecurityOptions.AllowReflectionAccess = false;
        AllowUnsafe = false;
        return this;
    }

    /// <summary>
    /// 禁用安全检查
    /// </summary>
    public ScriptOptions DisableSecurity()
    {
        SecurityOptions.EnableSecurityChecks = false;
        return this;
    }
}
