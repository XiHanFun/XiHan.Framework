// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using XiHan.Framework.Script.Enums;
using XiHan.Framework.Script.Options;

namespace XiHan.Framework.Script.Tests.Options;

/// <summary>
/// 脚本选项默认值与流式配置测试
/// </summary>
/// <remarks>
/// 选项对象是可变的、并且所有 <c>With*</c>/<c>Add*</c> 方法都返回自身而不是副本，
/// 这意味着"<c>Default</c> 每次给新实例"是安全前提——一旦退化成静态单例，
/// 任何一次链式配置都会污染全局，因此这条实例隔离必须锁死。
/// </remarks>
public class ScriptOptionsTests
{
    /// <summary>
    /// 默认选项的每个字段都符合文档约定
    /// </summary>
    [Fact]
    public void Default_HasDocumentedDefaults()
    {
        var options = ScriptOptions.Default;

        Assert.Empty(options.References);
        Assert.Empty(options.ReferencePaths);
        Assert.Empty(options.Globals);
        Assert.Equal(ScriptType.Statement, options.ScriptType);
        Assert.True(options.EnableCache);
        Assert.Null(options.CacheKey);
        Assert.Equal(30000, options.TimeoutMs);
        Assert.False(options.AllowUnsafe);
        Assert.Equal(OptimizationLevel.Debug, options.OptimizationLevel);
        Assert.Equal(OutputKind.DynamicallyLinkedLibrary, options.OutputKind);
        Assert.Equal(Platform.AnyCpu, options.Platform);
        Assert.NotNull(options.CompilerOptions);
        Assert.NotNull(options.SecurityOptions);
    }

    /// <summary>
    /// 默认导入的命名空间集合固定为五个基础命名空间
    /// </summary>
    [Fact]
    public void Default_ImportsBaseNamespaces()
    {
        var options = ScriptOptions.Default;

        Assert.Equal(
            new[] { "System", "System.Collections.Generic", "System.Linq", "System.Text", "System.Threading.Tasks" },
            options.Imports);
    }

    /// <summary>
    /// 默认选项每次返回新实例，避免链式配置污染全局
    /// </summary>
    [Fact]
    public void Default_ReturnsIndependentInstances()
    {
        var first = ScriptOptions.Default;
        var second = ScriptOptions.Default;

        Assert.NotSame(first, second);
        Assert.NotSame(first.Imports, second.Imports);
        Assert.NotSame(first.SecurityOptions, second.SecurityOptions);
        Assert.NotSame(first.CompilerOptions, second.CompilerOptions);

        first.WithTimeout(1).AddImport("System.IO");

        Assert.Equal(30000, second.TimeoutMs);
        Assert.DoesNotContain("System.IO", second.Imports);
    }

    /// <summary>
    /// 添加程序集引用按程序集实例登记
    /// </summary>
    [Fact]
    public void AddReference_WithAssembly_AppendsToReferences()
    {
        var options = ScriptOptions.Default;
        var assembly = typeof(ScriptOptions).Assembly;

        var returned = options.AddReference(assembly);

        Assert.Same(options, returned);
        Assert.Same(assembly, Assert.Single(options.References));
    }

    /// <summary>
    /// 添加类型引用会登记类型所在的程序集
    /// </summary>
    [Fact]
    public void AddReference_WithType_AppendsOwningAssembly()
    {
        var options = ScriptOptions.Default;

        var returned = options.AddReference(typeof(ScriptOptions));

        Assert.Same(options, returned);
        Assert.Same(typeof(ScriptOptions).Assembly, Assert.Single(options.References));
        Assert.Empty(options.ReferencePaths);
    }

    /// <summary>
    /// 添加路径引用登记到独立的路径集合
    /// </summary>
    [Fact]
    public void AddReference_WithPath_AppendsToReferencePaths()
    {
        var options = ScriptOptions.Default;

        var returned = options.AddReference("C:/libs/demo.dll");

        Assert.Same(options, returned);
        Assert.Equal("C:/libs/demo.dll", Assert.Single(options.ReferencePaths));
        Assert.Empty(options.References);
    }

    /// <summary>
    /// 添加命名空间导入追加到默认导入之后
    /// </summary>
    [Fact]
    public void AddImport_AppendsAfterDefaults()
    {
        var options = ScriptOptions.Default;

        var returned = options.AddImport("System.IO");

        Assert.Same(options, returned);
        Assert.Equal(6, options.Imports.Count);
        Assert.Equal("System.IO", options.Imports[^1]);
    }

    /// <summary>
    /// 同名全局变量以后写入的值为准
    /// </summary>
    [Fact]
    public void AddGlobal_WithSameName_OverwritesPreviousValue()
    {
        var options = ScriptOptions.Default;

        options.AddGlobal("name", "旧值");
        var returned = options.AddGlobal("name", "新值");

        Assert.Same(options, returned);
        Assert.Equal("新值", Assert.Single(options.Globals).Value);
    }

    /// <summary>
    /// 全局变量允许写入空值
    /// </summary>
    [Fact]
    public void AddGlobal_WithNullValue_KeepsKey()
    {
        var options = ScriptOptions.Default;

        options.AddGlobal("name", null);

        Assert.True(options.Globals.ContainsKey("name"));
        Assert.Null(options.Globals["name"]);
    }

    /// <summary>
    /// 设置脚本类型就地生效并返回自身
    /// </summary>
    [Theory]
    [InlineData(ScriptType.Expression)]
    [InlineData(ScriptType.Class)]
    [InlineData(ScriptType.Method)]
    [InlineData(ScriptType.Program)]
    public void WithScriptType_AppliesInPlace(ScriptType scriptType)
    {
        var options = ScriptOptions.Default;

        var returned = options.WithScriptType(scriptType);

        Assert.Same(options, returned);
        Assert.Equal(scriptType, options.ScriptType);
    }

    /// <summary>
    /// 设置超时时间就地生效
    /// </summary>
    [Fact]
    public void WithTimeout_AppliesInPlace()
    {
        var options = ScriptOptions.Default;

        var returned = options.WithTimeout(1234);

        Assert.Same(options, returned);
        Assert.Equal(1234, options.TimeoutMs);
    }

    /// <summary>
    /// 设置缓存键就地生效
    /// </summary>
    [Fact]
    public void WithCacheKey_AppliesInPlace()
    {
        var options = ScriptOptions.Default;

        var returned = options.WithCacheKey("key");

        Assert.Same(options, returned);
        Assert.Equal("key", options.CacheKey);
    }

    /// <summary>
    /// 禁用缓存只影响缓存开关，不清空已设置的缓存键
    /// </summary>
    [Fact]
    public void DisableCache_TurnsOffCacheFlagOnly()
    {
        var options = ScriptOptions.Default.WithCacheKey("key");

        var returned = options.DisableCache();

        Assert.Same(options, returned);
        Assert.False(options.EnableCache);
        Assert.Equal("key", options.CacheKey);
    }

    /// <summary>
    /// 启用优化把优化等级切到发布级
    /// </summary>
    [Fact]
    public void WithOptimization_SwitchesToRelease()
    {
        var options = ScriptOptions.Default;

        var returned = options.WithOptimization();

        Assert.Same(options, returned);
        Assert.Equal(OptimizationLevel.Release, options.OptimizationLevel);
    }

    /// <summary>
    /// 允许不安全代码开关就地生效
    /// </summary>
    [Fact]
    public void WithUnsafe_EnablesUnsafeFlag()
    {
        var options = ScriptOptions.Default;

        var returned = options.WithUnsafe();

        Assert.Same(options, returned);
        Assert.True(options.AllowUnsafe);
    }

    /// <summary>
    /// 安全配置回调拿到的是当前实例持有的安全选项
    /// </summary>
    [Fact]
    public void WithSecurity_ConfiguresOwnSecurityOptions()
    {
        var options = ScriptOptions.Default;
        SecurityOptions? captured = null;

        var returned = options.WithSecurity(security =>
        {
            captured = security;
            security.MaxFileSize = 2048;
        });

        Assert.Same(options, returned);
        Assert.Same(options.SecurityOptions, captured);
        Assert.Equal(2048, options.SecurityOptions.MaxFileSize);
    }

    /// <summary>
    /// 严格安全模式同时收紧安全选项与不安全代码开关
    /// </summary>
    [Fact]
    public void WithStrictSecurity_TightensAllSwitches()
    {
        var options = ScriptOptions.Default.WithUnsafe();

        var returned = options.WithStrictSecurity();

        Assert.Same(options, returned);
        Assert.True(options.SecurityOptions.EnableStrictMode);
        Assert.False(options.SecurityOptions.AllowFileSystemAccess);
        Assert.False(options.SecurityOptions.AllowNetworkAccess);
        Assert.False(options.SecurityOptions.AllowReflectionAccess);
        Assert.False(options.AllowUnsafe);
        // 严格模式不负责关掉安全检查开关本身
        Assert.True(options.SecurityOptions.EnableSecurityChecks);
    }

    /// <summary>
    /// 禁用安全检查只关闭总开关，不改动细粒度权限
    /// </summary>
    [Fact]
    public void DisableSecurity_TurnsOffMasterSwitchOnly()
    {
        var options = ScriptOptions.Default;

        var returned = options.DisableSecurity();

        Assert.Same(options, returned);
        Assert.False(options.SecurityOptions.EnableSecurityChecks);
        Assert.True(options.SecurityOptions.AllowFileSystemAccess);
        Assert.True(options.SecurityOptions.AllowNetworkAccess);
    }

    /// <summary>
    /// 链式配置逐项落到同一个实例上
    /// </summary>
    [Fact]
    public void FluentChain_AppliesEveryStepToSameInstance()
    {
        var options = ScriptOptions.Default
            .WithScriptType(ScriptType.Expression)
            .WithTimeout(500)
            .WithCacheKey("chain")
            .WithOptimization()
            .DisableCache()
            .AddImport("System.IO")
            .AddGlobal("name", 1);

        Assert.Equal(ScriptType.Expression, options.ScriptType);
        Assert.Equal(500, options.TimeoutMs);
        Assert.Equal("chain", options.CacheKey);
        Assert.Equal(OptimizationLevel.Release, options.OptimizationLevel);
        Assert.False(options.EnableCache);
        Assert.Contains("System.IO", options.Imports);
        Assert.Equal(1, options.Globals["name"]);
    }
}
