// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 禁用属性注入特性
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public class DisablePropertyInjectionAttribute : Attribute;
