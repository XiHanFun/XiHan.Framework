// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Application.Attributes;

/// <summary>
/// 标记动作或控制器启用幂等保护
/// </summary>
/// <remarks>
/// 调用方须在请求头携带幂等键；相同键、相同内容的重复请求重播首次响应。
/// 仅适用于 MVC 控制器与动态 API，不适用于 Minimal API。
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true)]
public sealed class IdempotentAttribute : Attribute;
