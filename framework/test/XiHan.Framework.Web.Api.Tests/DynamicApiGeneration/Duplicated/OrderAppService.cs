// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Application.Contracts.Services;

namespace XiHan.Framework.Web.Api.Tests.DynamicApiGeneration.Duplicated;

/// <summary>
/// 与外层同简名、不同命名空间的订单应用服务
/// </summary>
/// <remarks>
/// 控制器名由服务简名推导且不含命名空间，本类与 <c>DynamicApiGeneration.OrderAppService</c>
/// 会推导出同一个控制器名，用于复现控制器名冲突。
/// </remarks>
public class OrderAppService : IApplicationService
{
    /// <summary>
    /// 查询订单
    /// </summary>
    /// <returns></returns>
    public Task<string> GetOrderAsync() => Task.FromResult(string.Empty);
}
