// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions;

/// <summary>
/// Tenant 阶段的中间件：按注册顺序调用 <see cref="ITenantResolver"/>，取第一个解析结果（没有时用默认租户），
/// 校验格式，按选项核对 <see cref="ITenantStore"/>（存在且启用），然后写入安全上下文。
/// 失败以结果返回：<see cref="MultiTenancyErrors.InvalidTenantId"/>、<see cref="MultiTenancyErrors.MissingTenant"/>、
/// <see cref="MultiTenancyErrors.NotFound"/>、<see cref="MultiTenancyErrors.Disabled"/>。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class TenantResolutionMiddleware(MultiTenancyOptions options) : IPipelineMiddleware
{
    private readonly MultiTenancyOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);

        var tenantId = await ResolveAsync(context).ConfigureAwait(false) ?? _options.DefaultTenant;
        if (tenantId is null)
        {
            if (_options.RequireTenant)
            {
                context.SetOutcome(Error.Validation(MultiTenancyErrors.MissingTenant, "本调用必须带租户，但没有解析到租户。"));
                return;
            }

            await nextStep(context).ConfigureAwait(false);
            return;
        }

        if (!TenantIdentifier.IsValid(tenantId))
        {
            context.SetOutcome(Error.Validation(MultiTenancyErrors.InvalidTenantId, $"租户 ID '{tenantId}' 不合法。"));
            return;
        }

        TenantContext tenant;
        if (_options.ValidateAgainstStore)
        {
            var store = context.Services.GetRequiredService<ITenantStore>();
            var found = await store.FindAsync(tenantId, context.CancellationToken).ConfigureAwait(false);
            if (!found.TryGetValue(out var info))
            {
                context.SetOutcome(Error.NotFound(MultiTenancyErrors.NotFound, $"租户 '{tenantId}' 不存在。"));
                return;
            }

            if (!info.IsEnabled)
            {
                context.SetOutcome(Error.Denied(MultiTenancyErrors.Disabled, $"租户 '{tenantId}' 已禁用。"));
                return;
            }

            tenant = new TenantContext(info.Id) { DisplayName = info.DisplayName };
        }
        else
        {
            tenant = new TenantContext(tenantId);
        }

        context.SetTenant(tenant);
        await nextStep(context).ConfigureAwait(false);
    }

    private static async ValueTask<string?> ResolveAsync(InvocationContext context)
    {
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
