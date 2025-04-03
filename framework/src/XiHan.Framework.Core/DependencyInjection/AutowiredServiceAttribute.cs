#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AutowiredServiceAttribute
// Guid:a122cb65-e1aa-4b1a-b45b-ef8cef1e6f43
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 2:23:42
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// AutowiredServiceAttribute
/// <example>
/// 调用示例：
/// <code>
/// // 通过属性注入 Service 实例
/// public class PropertyClass
/// {
///     [AutowiredService]
///     public IService Service { get; set; }
///
///     public PropertyClass(AutowiredServiceHandler autowiredServiceHandler)
///     {
///         autowiredServiceHandler.Autowired(this);
///     }
/// }
/// // 通过字段注入 Service 实例
/// public class FieldClass
/// {
///     [AutowiredService]
///     private IService _service;
///
///     public FieldClass(AutowiredServiceHandler autowiredServiceHandler)
///     {
///         autowiredServiceHandler.Autowired(this);
///     }
/// }
/// </code>
/// </example>
/// </summary>
/// <remarks>由此启发：<see href="https://www.cnblogs.com/loogn/p/10566510.html"/></remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class AutowiredServiceAttribute : Attribute;
