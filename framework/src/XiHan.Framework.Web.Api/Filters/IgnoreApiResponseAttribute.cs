// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Mvc.Filters;

namespace XiHan.Framework.Web.Api.Filters;

/// <summary>
/// 忽略统一返回包装
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class IgnoreApiResponseAttribute : Attribute, IFilterMetadata
{
}
