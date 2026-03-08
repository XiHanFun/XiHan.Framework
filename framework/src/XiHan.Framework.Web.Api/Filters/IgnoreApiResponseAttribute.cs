#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IgnoreApiResponseAttribute
// Guid:0e3ad4f0-9fd2-4f1a-88f0-76b7e7ef7f1b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 21:20:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Mvc.Filters;

namespace XiHan.Framework.Web.Api.Filters;

/// <summary>
/// 忽略统一返回包装
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class IgnoreApiResponseAttribute : Attribute, IFilterMetadata
{
}
