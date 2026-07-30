// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.MultiTenancy.Abstractions;

/// <summary>
/// 忽略多租户
/// </summary>
[AttributeUsage(AttributeTargets.All)]
public class IgnoreMultiTenancyAttribute : Attribute;
