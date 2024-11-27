#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DisablePropertyInjectionAttribute
// Guid:d0d7d7ae-03df-4e73-852b-fc69d31f693b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/27 21:52:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 禁用属性注入特性
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public class DisablePropertyInjectionAttribute : Attribute;