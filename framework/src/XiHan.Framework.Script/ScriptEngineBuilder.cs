#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptEngineBuilder
// Guid:2e44c529-82d9-4d7e-89cb-954d585effc6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/31 6:27:56
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Script.Core;
using XiHan.Framework.Script.Options;

namespace XiHan.Framework.Script;

/// <summary>
/// 脚本引擎构建器
/// </summary>
public class ScriptEngineBuilder
{
    private readonly ScriptOptions _options = new();
    private readonly List<Action<IScriptEngine>> _configurations = [];

    /// <summary>
    /// 添加程序集引用
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <returns>构建器实例</returns>
    public ScriptEngineBuilder AddReference(System.Reflection.Assembly assembly)
    {
        _options.AddReference(assembly);
        return this;
    }

    /// <summary>
    /// 添加程序集引用
    /// </summary>
    /// <param name="assemblyPath">程序集路径</param>
    /// <returns>构建器实例</returns>
    public ScriptEngineBuilder AddReference(string assemblyPath)
    {
        _options.AddReference(assemblyPath);
        return this;
    }

    /// <summary>
    /// 添加类型引用
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>构建器实例</returns>
    public ScriptEngineBuilder AddReference(Type type)
    {
        _options.AddReference(type);
        return this;
    }

    /// <summary>
    /// 添加命名空间导入
    /// </summary>
    /// <param name="namespace">命名空间</param>
    /// <returns>构建器实例</returns>
    public ScriptEngineBuilder AddImport(string @namespace)
    {
        _options.AddImport(@namespace);
        return this;
    }

    /// <summary>
    /// 添加全局变量
    /// </summary>
    /// <param name="name">变量名</param>
    /// <param name="value">变量值</param>
    /// <returns>构建器实例</returns>
    public ScriptEngineBuilder AddGlobal(string name, object? value)
    {
        _options.AddGlobal(name, value);
        return this;
    }

    /// <summary>
    /// 设置超时时间
    /// </summary>
    /// <param name="timeoutMs">超时时间(毫秒)</param>
    /// <returns>构建器实例</returns>
    public ScriptEngineBuilder WithTimeout(int timeoutMs)
    {
        _options.WithTimeout(timeoutMs);
        return this;
    }

    /// <summary>
    /// 启用优化
    /// </summary>
    /// <returns>构建器实例</returns>
    public ScriptEngineBuilder WithOptimization()
    {
        _options.WithOptimization();
        return this;
    }

    /// <summary>
    /// 允许不安全代码
    /// </summary>
    /// <returns>构建器实例</returns>
    public ScriptEngineBuilder WithUnsafe()
    {
        _options.WithUnsafe();
        return this;
    }

    /// <summary>
    /// 禁用缓存
    /// </summary>
    /// <returns>构建器实例</returns>
    public ScriptEngineBuilder DisableCache()
    {
        _options.DisableCache();
        return this;
    }

    /// <summary>
    /// 配置引擎
    /// </summary>
    /// <param name="configure">配置委托</param>
    /// <returns>构建器实例</returns>
    public ScriptEngineBuilder Configure(Action<IScriptEngine> configure)
    {
        _configurations.Add(configure);
        return this;
    }

    /// <summary>
    /// 构建脚本引擎
    /// </summary>
    /// <returns>脚本引擎实例</returns>
    public IScriptEngine Build()
    {
        var engine = new ScriptEngine();

        // 应用配置
        foreach (var configure in _configurations)
        {
            configure(engine);
        }

        return engine;
    }
}
