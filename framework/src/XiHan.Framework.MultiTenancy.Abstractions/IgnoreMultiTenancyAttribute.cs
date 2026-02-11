#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IgnoreMultiTenancyAttribute
// Guid:98a810cb-5d65-4648-a25b-0682b059f994
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 06:19:53
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.MultiTenancy.Abstractions;

/// <summary>
/// 忽略多租户
/// </summary>
[AttributeUsage(AttributeTargets.All)]
public class IgnoreMultiTenancyAttribute : Attribute;
