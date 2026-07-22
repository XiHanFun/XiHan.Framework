// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 禁止常规注册特性
/// </summary>
[AttributeUsage(AttributeTargets.All)]
public class DisableConventionalRegistrationAttribute : Attribute;
