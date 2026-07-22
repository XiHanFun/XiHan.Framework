// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
