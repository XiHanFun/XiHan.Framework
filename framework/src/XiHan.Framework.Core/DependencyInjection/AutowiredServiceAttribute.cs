// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 自动注入服务特性
/// 用于标记需要自动注入的服务属性或字段。一般推荐构造函数注入，属性或字段注入仅在特殊情况下使用
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
public class AutowiredServiceAttribute : Attribute;
