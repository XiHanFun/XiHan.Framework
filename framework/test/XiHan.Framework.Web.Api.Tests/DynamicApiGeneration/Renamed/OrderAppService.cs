// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Application.Contracts.Services;

namespace XiHan.Framework.Web.Api.Tests.DynamicApiGeneration.Renamed;

/// <summary>
/// 与外层同简名、经类级 Name 改名的订单应用服务
/// </summary>
/// <remarks>
/// 与 <c>DynamicApiGeneration.OrderAppService</c> 简名相同，
/// 但通过类级 [DynamicApi(Name)] 定制控制器名，用于验证改名后不再冲突。
/// </remarks>
[DynamicApi(Name = "plugin-order")]
public class OrderAppService : IApplicationService
{
    /// <summary>
    /// 查询订单
    /// </summary>
    /// <returns></returns>
    public Task<string> GetOrderAsync() => Task.FromResult(string.Empty);
}
