#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CacheableAttribute
// Guid:e5f6a7b8-c9d0-1234-ef01-23456789abcd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/05 05:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Caching.Attributes;

/// <summary>
/// 可缓存方法特性，标记在方法上以启用 AOP 自动缓存
/// </summary>
/// <remarks>
/// 缓存键模板支持 {paramName} 占位符，将在运行时替换为方法参数值。
/// 示例：Key = "config:{tenantId}:{key}", ExpireSeconds = 300
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class CacheableAttribute : Attribute
{
    /// <summary>
    /// 缓存键模板，支持 {paramName} 占位符
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// 缓存过期时间（秒），默认 300 秒
    /// </summary>
    public int ExpireSeconds { get; set; } = 300;
}
