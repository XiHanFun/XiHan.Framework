#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CacheEvictAttribute
// Guid:f6a7b8c9-d0e1-2345-f012-3456789abcde
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/05 05:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Caching.Attributes;

/// <summary>
/// 缓存清除方法特性，标记在方法上以在方法执行后清除指定缓存
/// </summary>
/// <remarks>
/// 缓存键模板支持 {paramName} 占位符，将在运行时替换为方法参数值。
/// 示例：Key = "config:{tenantId}:{key}"
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class CacheEvictAttribute : Attribute
{
    /// <summary>
    /// 需要清除的缓存键模板，支持 {paramName} 占位符
    /// </summary>
    public string Key { get; set; } = default!;
}
