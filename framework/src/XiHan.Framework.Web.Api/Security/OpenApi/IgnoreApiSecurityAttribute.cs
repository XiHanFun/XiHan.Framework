#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IgnoreApiSecurityAttribute
// Guid:d5e6a3dc-df7a-4f0a-b5dc-b9dce390bd54
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/13 23:35:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Mvc.Filters;

namespace XiHan.Framework.Web.Api.Security.OpenApi;

/// <summary>
/// 忽略 OpenApi 安全校验
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class IgnoreApiSecurityAttribute : Attribute, IFilterMetadata
{
}
