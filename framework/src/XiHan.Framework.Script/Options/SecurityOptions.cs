#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SecurityOptions
// Guid:c8d5f2e1-3b4a-4c9d-8e7f-1a2b3c4d5e6f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/2 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Script.Options;

/// <summary>
/// 脚本安全选项配置
/// </summary>
public class SecurityOptions
{
    /// <summary>
    /// 是否启用安全检查
    /// </summary>
    public bool EnableSecurityChecks { get; set; } = true;

    /// <summary>
    /// 是否启用严格模式
    /// </summary>
    public bool EnableStrictMode { get; set; } = false;

    /// <summary>
    /// 是否允许文件系统访问
    /// </summary>
    public bool AllowFileSystemAccess { get; set; } = true;

    /// <summary>
    /// 是否允许网络访问
    /// </summary>
    public bool AllowNetworkAccess { get; set; } = true;

    /// <summary>
    /// 是否允许反射访问
    /// </summary>
    public bool AllowReflectionAccess { get; set; } = true;

    /// <summary>
    /// 是否允许进程操作
    /// </summary>
    public bool AllowProcessOperations { get; set; } = false;

    /// <summary>
    /// 是否允许注册表访问
    /// </summary>
    public bool AllowRegistryAccess { get; set; } = false;

    /// <summary>
    /// 是否允许环境变量访问
    /// </summary>
    public bool AllowEnvironmentAccess { get; set; } = true;

    /// <summary>
    /// 最大文件大小限制（字节）
    /// </summary>
    public long MaxFileSize { get; set; } = 10 * 1024 * 1024; // 10MB

    /// <summary>
    /// 允许的文件扩展名
    /// </summary>
    public List<string> AllowedFileExtensions { get; set; } = [".cs", ".csx", ".txt"];

    /// <summary>
    /// 禁止的命名空间
    /// </summary>
    public List<string> ForbiddenNamespaces { get; set; } = [
        "System.Reflection.Emit",
        "System.Runtime.InteropServices",
        "System.Security.Permissions",
        "System.Diagnostics.Process"
    ];

    /// <summary>
    /// 禁止的类型
    /// </summary>
    public List<string> ForbiddenTypes { get; set; } = [
        "System.Diagnostics.Process",
        "Microsoft.Win32.Registry",
        "System.Environment"
    ];

    /// <summary>
    /// 危险关键字列表
    /// </summary>
    public List<string> DangerousKeywords { get; set; } = [
        "unsafe", "fixed", "stackalloc",
        "DllImport", "Marshal",
        "Assembly.Load", "Activator.CreateInstance",
        "Process.Start", "Environment.Exit"
    ];

    /// <summary>
    /// 创建严格安全配置
    /// </summary>
    /// <returns>严格安全配置实例</returns>
    public static SecurityOptions Strict()
    {
        return new SecurityOptions
        {
            EnableSecurityChecks = true,
            EnableStrictMode = true,
            AllowFileSystemAccess = false,
            AllowNetworkAccess = false,
            AllowReflectionAccess = false,
            AllowProcessOperations = false,
            AllowRegistryAccess = false,
            AllowEnvironmentAccess = false,
            MaxFileSize = 1024 * 1024, // 1MB
            AllowedFileExtensions = [".cs", ".csx"]
        };
    }

    /// <summary>
    /// 创建宽松安全配置
    /// </summary>
    /// <returns>宽松安全配置实例</returns>
    public static SecurityOptions Permissive()
    {
        return new SecurityOptions
        {
            EnableSecurityChecks = true,
            EnableStrictMode = false,
            AllowFileSystemAccess = true,
            AllowNetworkAccess = true,
            AllowReflectionAccess = true,
            AllowProcessOperations = true,
            AllowRegistryAccess = true,
            AllowEnvironmentAccess = true,
            MaxFileSize = 50 * 1024 * 1024, // 50MB
            AllowedFileExtensions = [".cs", ".csx", ".txt", ".json", ".xml"]
        };
    }

    /// <summary>
    /// 创建禁用安全检查的配置
    /// </summary>
    /// <returns>禁用安全检查的配置实例</returns>
    public static SecurityOptions Disabled()
    {
        return new SecurityOptions
        {
            EnableSecurityChecks = false
        };
    }
}
