// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Authorization;

namespace XiHan.Framework.Hosting.AspNetCore.Endpoints;

/// <summary>
/// 核对端点元数据上的授权要求引用的策略都存在。
/// </summary>
/// <remarks>
/// <c>UseXiHan()</c> 在宿主建请求管道时调用一次（ADR-0080）：此刻路由已全部映射（<c>UseEndpoints</c> 已把数据源交给路由），容器里的策略也都已注册。<c>RequireXiHanPermission</c> 的权限码未登记、<c>RequireXiHanPolicy</c> 的策略未注册，都抛出携带 <see cref="SecurityErrors.UnregisteredPolicy"/> 的 <see cref="FrameworkException"/>，一次列出全部，宿主启动失败，而不是等到调用时以 <see cref="SecurityErrors.PolicyNotFound"/> 拒绝。没有装配授权能力（容器里没有 <see cref="PolicyCatalog"/>）时 Authorize 阶段不判定任何要求，这里也不核对。
/// </remarks>
internal static class EndpointRequirementCheck
{
    /// <summary>
    /// 核对全部端点的授权要求。
    /// </summary>
    /// <param name="app">宿主的应用构建器；是 <see cref="IEndpointRouteBuilder"/>（<c>WebApplication</c>）时连同它自己的数据源一起看。</param>
    public static void Verify(IApplicationBuilder app)
    {
        var services = app.ApplicationServices;
        if (services.GetService<PolicyCatalog>() is not { } catalog)
        {
            return;
        }

        // 路由的全局数据源（UseEndpoints 交进来的）是主要来源；WebApplication 自己的数据源也看一遍，
        // 应对没有经过 UseEndpoints 的装配方式。同一端点可能从两处各读到一次，按文字去重
        var sources = new List<EndpointDataSource>();
        if (services.GetService<EndpointDataSource>() is { } global)
        {
            sources.Add(global);
        }

        if (app is IEndpointRouteBuilder routes)
        {
            sources.AddRange(routes.DataSources);
        }

        var violations = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var endpoint in sources.SelectMany(static s => s.Endpoints))
        {
            if (endpoint.Metadata.GetMetadata<AuthorizationRequirement>() is { } requirement && !catalog.Find(requirement.Policy).HasValue)
            {
                var name = endpoint.DisplayName ?? (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? endpoint.ToString() ?? string.Empty;
                violations.Add(PermissionPolicy.CodeOf(requirement.Policy) is { } code
                    ? $"'{name}' requires permission '{code}', which is not registered"
                    : $"'{name}' requires policy '{requirement.Policy}', which is not registered");
            }
        }

        if (violations.Count > 0)
        {
            throw new FrameworkException(SecurityErrors.UnregisteredPolicy, $"Endpoints reference policies that do not exist: {string.Join("; ", violations)}. Register permission codes with 'FeatureContext.AddPermission' and policies as 'IPolicy' services.");
        }
    }
}
