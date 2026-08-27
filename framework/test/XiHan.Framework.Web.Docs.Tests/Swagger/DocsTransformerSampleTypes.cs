// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Application.Attributes;
using XiHan.Framework.Application.Contracts.Services;
using XiHan.Framework.Web.Api.DynamicApi.Attributes;

namespace XiHan.Framework.Web.Docs.Tests.Swagger;

/// <summary>
/// 操作转换器取样应用服务（原始方法一侧）
/// </summary>
/// <remarks>
/// 刻意不给 Group，避免污染 DynamicApiSwaggerGroupHelper 的分组扫描断言。
/// </remarks>
public class DocsTransformerAppService : IApplicationService
{
    /// <summary>
    /// 带自定义描述的取样查询
    /// </summary>
    /// <param name="id">标识</param>
    /// <returns>标识回显</returns>
    [DynamicApi(Description = "取样方法的自定义描述")]
    public Task<string> GetDescribedAsync(string id)
    {
        return Task.FromResult(id);
    }

    /// <summary>
    /// 无自定义描述的取样查询
    /// </summary>
    /// <param name="id">标识</param>
    /// <returns>标识回显</returns>
    public Task<string> GetPlainAsync(string id)
    {
        return Task.FromResult(id);
    }
}

/// <summary>
/// 模拟动态 API 生成出来的控制器（转换器读取 OriginalMethodAttribute 反查原始方法）
/// </summary>
public class DocsGeneratedController
{
    /// <summary>
    /// 指向带描述的原始方法
    /// </summary>
    /// <param name="id">标识</param>
    /// <returns>标识回显</returns>
    [OriginalMethod(typeof(DocsTransformerAppService), nameof(DocsTransformerAppService.GetDescribedAsync), new Type[] { typeof(string) })]
    public Task<string> GetDescribed(string id)
    {
        return Task.FromResult(id);
    }

    /// <summary>
    /// 指向无描述的原始方法
    /// </summary>
    /// <param name="id">标识</param>
    /// <returns>标识回显</returns>
    [OriginalMethod(typeof(DocsTransformerAppService), nameof(DocsTransformerAppService.GetPlainAsync), new Type[] { typeof(string) })]
    public Task<string> GetPlain(string id)
    {
        return Task.FromResult(id);
    }

    /// <summary>
    /// 指向一个并不存在的原始方法，用于验证反查失败被吞掉
    /// </summary>
    /// <param name="id">标识</param>
    /// <returns>标识回显</returns>
    [OriginalMethod(typeof(DocsTransformerAppService), "GetNotExistsAsync", new Type[] { typeof(string) })]
    public Task<string> GetUnresolvable(string id)
    {
        return Task.FromResult(id);
    }

    /// <summary>
    /// 未打原始方法标记的动作
    /// </summary>
    /// <param name="id">标识</param>
    /// <returns>标识回显</returns>
    public Task<string> GetWithoutMarker(string id)
    {
        return Task.FromResult(id);
    }
}
