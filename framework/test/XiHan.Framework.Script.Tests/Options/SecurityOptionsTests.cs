// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Script.Options;

namespace XiHan.Framework.Script.Tests.Options;

/// <summary>
/// 脚本安全选项测试
/// </summary>
/// <remarks>
/// 这些默认值是脚本引擎唯一的安全边界：扩展名白名单决定哪些文件能被加载，
/// 禁用命名空间/类型/危险关键字三张黑名单直接驱动 <c>ValidateAssemblySecurity</c> 的抛出分支。
/// 名单一旦被静默缩小，安全闸门就会形同虚设，因此逐条锁死内容而不只断言"非空"。
/// </remarks>
public class SecurityOptionsTests
{
    /// <summary>
    /// 默认配置开启安全检查，但保持非严格模式
    /// </summary>
    [Fact]
    public void Default_EnablesChecksWithoutStrictMode()
    {
        var options = new SecurityOptions();

        Assert.True(options.EnableSecurityChecks);
        Assert.False(options.EnableStrictMode);
        Assert.True(options.AllowFileSystemAccess);
        Assert.True(options.AllowNetworkAccess);
        Assert.True(options.AllowReflectionAccess);
        Assert.False(options.AllowProcessOperations);
        Assert.False(options.AllowRegistryAccess);
        Assert.True(options.AllowEnvironmentAccess);
        Assert.Equal(10 * 1024 * 1024, options.MaxFileSize);
    }

    /// <summary>
    /// 默认允许的脚本扩展名固定为三种文本格式
    /// </summary>
    [Fact]
    public void Default_AllowsOnlyTextScriptExtensions()
    {
        var options = new SecurityOptions();

        Assert.Equal(new[] { ".cs", ".csx", ".txt" }, options.AllowedFileExtensions);
    }

    /// <summary>
    /// 默认禁用命名空间覆盖动态发射、互操作、权限与进程四类
    /// </summary>
    [Fact]
    public void Default_ForbidsHighRiskNamespaces()
    {
        var options = new SecurityOptions();

        Assert.Equal(
            new[]
            {
                "System.Reflection.Emit",
                "System.Runtime.InteropServices",
                "System.Security.Permissions",
                "System.Diagnostics.Process"
            },
            options.ForbiddenNamespaces);
    }

    /// <summary>
    /// 默认禁用类型覆盖进程、注册表与环境三类
    /// </summary>
    [Fact]
    public void Default_ForbidsHighRiskTypes()
    {
        var options = new SecurityOptions();

        Assert.Equal(
            new[] { "System.Diagnostics.Process", "Microsoft.Win32.Registry", "System.Environment" },
            options.ForbiddenTypes);
    }

    /// <summary>
    /// 默认危险关键字覆盖不安全代码、互操作与动态加载
    /// </summary>
    [Fact]
    public void Default_ListsDangerousKeywords()
    {
        var options = new SecurityOptions();

        Assert.Equal(
            new[]
            {
                "unsafe", "fixed", "stackalloc",
                "DllImport", "Marshal",
                "Assembly.Load", "Activator.CreateInstance",
                "Process.Start", "Environment.Exit"
            },
            options.DangerousKeywords);
    }

    /// <summary>
    /// 严格配置关闭全部可选权限并收紧文件上限
    /// </summary>
    [Fact]
    public void Strict_ClosesEveryOptionalPermission()
    {
        var options = SecurityOptions.Strict();

        Assert.True(options.EnableSecurityChecks);
        Assert.True(options.EnableStrictMode);
        Assert.False(options.AllowFileSystemAccess);
        Assert.False(options.AllowNetworkAccess);
        Assert.False(options.AllowReflectionAccess);
        Assert.False(options.AllowProcessOperations);
        Assert.False(options.AllowRegistryAccess);
        Assert.False(options.AllowEnvironmentAccess);
        Assert.Equal(1024 * 1024, options.MaxFileSize);
        Assert.Equal(new[] { ".cs", ".csx" }, options.AllowedFileExtensions);
    }

    /// <summary>
    /// 宽松配置放开全部权限但仍然保留安全检查
    /// </summary>
    [Fact]
    public void Permissive_OpensPermissionsButKeepsChecks()
    {
        var options = SecurityOptions.Permissive();

        Assert.True(options.EnableSecurityChecks);
        Assert.False(options.EnableStrictMode);
        Assert.True(options.AllowFileSystemAccess);
        Assert.True(options.AllowNetworkAccess);
        Assert.True(options.AllowReflectionAccess);
        Assert.True(options.AllowProcessOperations);
        Assert.True(options.AllowRegistryAccess);
        Assert.True(options.AllowEnvironmentAccess);
        Assert.Equal(50 * 1024 * 1024, options.MaxFileSize);
        Assert.Equal(new[] { ".cs", ".csx", ".txt", ".json", ".xml" }, options.AllowedFileExtensions);
    }

    /// <summary>
    /// 禁用配置只关闭总开关，黑名单内容保持默认
    /// </summary>
    [Fact]
    public void Disabled_TurnsOffMasterSwitchAndKeepsBlacklists()
    {
        var options = SecurityOptions.Disabled();

        Assert.False(options.EnableSecurityChecks);
        Assert.Equal(new[] { ".cs", ".csx", ".txt" }, options.AllowedFileExtensions);
        Assert.NotEmpty(options.ForbiddenNamespaces);
        Assert.NotEmpty(options.ForbiddenTypes);
        Assert.NotEmpty(options.DangerousKeywords);
    }

    /// <summary>
    /// 工厂方法每次返回独立实例，避免共享名单被就地修改
    /// </summary>
    [Fact]
    public void Factories_ReturnIndependentInstances()
    {
        var first = SecurityOptions.Strict();
        var second = SecurityOptions.Strict();

        Assert.NotSame(first, second);
        Assert.NotSame(first.AllowedFileExtensions, second.AllowedFileExtensions);

        first.AllowedFileExtensions.Add(".exe");

        Assert.DoesNotContain(".exe", second.AllowedFileExtensions);
    }
}
