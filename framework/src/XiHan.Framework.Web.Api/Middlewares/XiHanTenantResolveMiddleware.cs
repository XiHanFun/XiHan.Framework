// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using System.Text.Json;
using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Application.Contracts.Enums;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.ConfigurationStore;
using XiHan.Framework.Security.Users;
using XiHan.Framework.Utils.Extensions;
using XiHan.Framework.Web.Api.Constants;
using XiHan.Framework.Web.Api.Contexts;

namespace XiHan.Framework.Web.Api.Middlewares;

/// <summary>
/// WebApi 租户解析中间件
/// </summary>
/// <remarks>
/// <para>
/// 租户来源分两种信任级别：
/// <list type="bullet">
///   <item>令牌（<see cref="CurrentUserTenantResolveContributor"/>）：已认证请求一律以它为准，令牌已签名，不再查租户存储；</item>
///   <item>外部输入（请求头、查询参数、兜底租户等其余贡献者）：必须在 <see cref="ITenantStore"/> 查得到且处于激活状态，
///   否则直接以 400 拒绝——不拿一个未经验证的数字建租户上下文。</item>
/// </list>
/// </para>
/// <para>
/// 解析完成后把本次请求最终生效的身份（用户、租户）定型写回 <see cref="IRequestContextAccessor"/>，
/// 位于本中间件之前的访问日志、异常日志等在请求结束时据此记账。
/// </para>
/// </remarks>
public class XiHanTenantResolveMiddleware(
    RequestDelegate next,
    IOptions<XiHanTenantResolveOptions> options)
{
    /// <summary>
    /// 平台租户键：平台就是 0 号租户
    /// </summary>
    private const string PlatformTenantKey = "0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// 执行中间件
    /// </summary>
    /// <param name="httpContext">HTTP 上下文</param>
    /// <param name="currentTenant">当前租户</param>
    /// <param name="tenantStore">租户存储</param>
    /// <returns>异步任务</returns>
    public async Task InvokeAsync(HttpContext httpContext, ICurrentTenant currentTenant, ITenantStore tenantStore)
    {
        var resolveContext = new TenantResolveContext(httpContext.RequestServices);
        string? handledBy = null;
        foreach (var resolver in options.Value.TenantResolvers)
        {
            await resolver.ResolveAsync(resolveContext);
            if (resolveContext.Handled)
            {
                handledBy = resolver.Name;
                break;
            }
        }

        var tenantKey = resolveContext.TenantIdOrName;
        if (tenantKey.IsNullOrWhiteSpace() && !resolveContext.Handled)
        {
            tenantKey = options.Value.FallbackTenant;
        }

        if (tenantKey.IsNullOrWhiteSpace())
        {
            FinalizeRequestIdentity(httpContext, null);
            await next(httpContext);
            return;
        }

        tenantKey = tenantKey.Trim();

        // 平台就是 0 号租户：显式给出 0 与未给出租户等价，按平台放行（不去租户存储里找一个不存在的 0 号租户）
        if (tenantKey == PlatformTenantKey)
        {
            FinalizeRequestIdentity(httpContext, null);
            await next(httpContext);
            return;
        }

        var fromToken = handledBy == CurrentUserTenantResolveContributor.ContributorName;
        var tenantConfiguration = await ResolveTenantAsync(tenantStore, tenantKey, httpContext.RequestAborted);

        long tenantId;
        string tenantName;
        if (tenantConfiguration is not null && (fromToken || tenantConfiguration.IsActive))
        {
            tenantId = tenantConfiguration.Id;
            tenantName = tenantConfiguration.Name;
        }
        else if (fromToken && long.TryParse(tenantKey, out var tokenTenantId))
        {
            // 令牌里的租户已签名，租户存储未登记它（应用自管租户目录）时仍以令牌为准；租户的停用与到期由应用侧的会话闸门拦截
            tenantId = tokenTenantId;
            tenantName = tenantKey;
        }
        else
        {
            await RejectAsync(httpContext, tenantKey);
            return;
        }

        using (currentTenant.Change(tenantId, tenantName))
        {
            FinalizeRequestIdentity(httpContext, tenantId);
            await next(httpContext);
        }
    }

    private static async Task<TenantConfiguration?> ResolveTenantAsync(
        ITenantStore tenantStore,
        string tenantIdOrName,
        CancellationToken cancellationToken)
    {
        if (long.TryParse(tenantIdOrName, out var tenantId))
        {
            var tenantById = await tenantStore.FindAsync(tenantId, cancellationToken);
            if (tenantById is not null)
            {
                return tenantById;
            }
        }

        return await tenantStore.FindAsync(tenantIdOrName, cancellationToken);
    }

    /// <summary>
    /// 把本次请求最终生效的身份写回请求上下文（认证与租户解析都已完成）
    /// </summary>
    private static void FinalizeRequestIdentity(HttpContext httpContext, long? tenantId)
    {
        var accessor = httpContext.RequestServices.GetService<IRequestContextAccessor>();
        if (accessor?.Current is not { } requestContext)
        {
            return;
        }

        var currentUser = httpContext.RequestServices.GetService<ICurrentUser>();
        accessor.Current = requestContext with
        {
            UserId = currentUser?.UserId,
            UserName = currentUser?.UserName,
            TenantId = tenantId
        };
    }

    /// <summary>
    /// 外部输入给出的租户在租户存储里查不到或未激活时拒绝请求
    /// </summary>
    private static async Task RejectAsync(HttpContext httpContext, string tenantKey)
    {
        if (httpContext.Response.HasStarted)
        {
            return;
        }

        var traceId = httpContext.Items[XiHanWebApiConstants.TraceIdItemKey]?.ToString() ?? httpContext.TraceIdentifier;
        httpContext.Response.Clear();
        httpContext.Response.StatusCode = (int)ApiResponseCodes.BadRequest;
        httpContext.Response.ContentType = "application/json; charset=utf-8";

        var payload = new ApiResponse
        {
            Code = ApiResponseCodes.BadRequest,
            Message = $"租户「{tenantKey}」不存在或未启用。",
            TraceId = traceId
        };

        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions), httpContext.RequestAborted);
    }

    private sealed class TenantResolveContext(IServiceProvider serviceProvider) : ITenantResolveContext
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;

        public string? TenantIdOrName { get; set; }

        public bool Handled { get; set; }
    }
}
