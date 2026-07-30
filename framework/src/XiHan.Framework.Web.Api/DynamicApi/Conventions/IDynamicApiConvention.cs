// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Api.DynamicApi.Conventions;

/// <summary>
/// 动态 API 约定接口
/// </summary>
public interface IDynamicApiConvention
{
    /// <summary>
    /// 应用约定
    /// </summary>
    /// <param name="context">约定上下文</param>
    void Apply(DynamicApiConventionContext context);
}
