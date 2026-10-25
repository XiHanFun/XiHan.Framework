// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Resolution;

/// <summary>
/// Tenant 阶段的中间件：按注册顺序调用 <see cref="ITenantResolver"/>，取第一个解析结果（没有时用默认租户），
/// 校验格式，按选项核对 <see cref="ITenantStore"/>（存在且启用），然后写入安全上下文。
/// 失败以结果返回：<see cref="MultiTenancyErrors.InvalidTenantId"/>、<see cref="MultiTenancyErrors.MissingTenant"/>、
/// <see cref="MultiTenancyErrors.NotFound"/>、<see cref="MultiTenancyErrors.Disabled"/>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class TenantResolutionMiddleware(MultiTenancyOptions options) : IPipelineMiddleware
{
    /// <summary>
    /// 默认租户、是否必须带租户、是否对照存储校验。
    /// </summary>
    private readonly MultiTenancyOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);

        // 解析器都没给出结果时才退回默认租户；默认租户同样要走下面的格式与存储校验
        var tenantId = await ResolveAsync(context).ConfigureAwait(false) ?? _options.DefaultTenant;
        if (tenantId is null)
        {
            if (_options.RequireTenant)
            {
                context.SetOutcome(Error.Validation(MultiTenancyErrors.MissingTenant, MultiTenancyMessages.TenantRequired));
                return;
            }

            // 不强制带租户：无租户调用照常放行，安全上下文里租户保持为空
            await nextStep(context).ConfigureAwait(false);
            return;
        }

        // 先校验文法再查存储：不把来自外部、未经校验的 ID 直接交给 Provider
        if (!TenantIdentifier.IsValid(tenantId))
        {
            context.SetOutcome(Error.Validation(MultiTenancyErrors.InvalidTenantId, MultiTenancyMessages.InvalidTenantId, MessageArguments.Of(("tenantId", tenantId))));
            return;
        }

        TenantContext tenant;
        if (_options.ValidateAgainstStore)
        {
            var store = context.Services.GetRequiredService<ITenantStore>();
            var found = await store.FindAsync(tenantId, context.CancellationToken).ConfigureAwait(false);
            if (!found.TryGetValue(out var info))
            {
                context.SetOutcome(Error.NotFound(MultiTenancyErrors.NotFound, MultiTenancyMessages.TenantNotFound, MessageArguments.Of(("tenantId", tenantId))));
                return;
            }

            if (!info.IsEnabled)
            {
                context.SetOutcome(Error.Denied(MultiTenancyErrors.Disabled, MultiTenancyMessages.TenantDisabled, MessageArguments.Of(("tenantId", tenantId))));
                return;
            }

            // 以存储里的记录为准，显示名随之带入安全上下文
            tenant = new TenantContext(info.Id) { DisplayName = info.DisplayName };
        }
        else
        {
            // 关闭存储校验：只保证格式合法，没有显示名
            tenant = new TenantContext(tenantId);
        }

        context.SetTenant(tenant);
        await nextStep(context).ConfigureAwait(false);
    }

    /// <summary>
    /// 按注册顺序依次调用解析器，返回第一个解析出的租户 ID；都没有返回 <see langword="null"/>。
    /// </summary>
    private static async ValueTask<string?> ResolveAsync(InvocationContext context)
    {
        // 注册顺序即优先级：先注册的解析器（如已认证主体的声明）压过后注册的（如调用方自报的 Baggage）
        foreach (var resolver in context.Services.GetServices<ITenantResolver>())
        {
            var resolved = await resolver.ResolveAsync(context, context.CancellationToken).ConfigureAwait(false);
            if (resolved.TryGetValue(out var tenantId))
            {
                return tenantId;
            }
        }

        return null;
    }
}
