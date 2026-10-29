// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 条件引擎的登记：属性登记项逐个进容器，登记表、编译器与求值器按单例登记一次。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class ConditionServiceCollectionExtensions
{
    /// <summary>
    /// 登记条件属性。
    /// </summary>
    /// <remarks>
    /// 并确保 <see cref="IConditionAttributeRegistry"/>、<see cref="IConditionCompiler"/>、<see cref="IConditionEvaluator"/> 已登记。各模块可各自调用；登记表在首次解析时一次建好，重名、别名冲突即抛出。不带参数时只登记三项服务。
    /// </remarks>
    public static IServiceCollection AddConditionAttributes(this IServiceCollection services, params ConditionAttributeDescriptor[] attributes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(attributes);
        foreach (var attribute in attributes)
        {
            services.AddSingleton(attribute ?? throw new ArgumentException("Condition attributes must not contain null.", nameof(attributes)));
        }

        services.TryAddSingleton<IConditionAttributeRegistry>(static sp => new ConditionAttributeRegistry(sp.GetServices<ConditionAttributeDescriptor>()));
        services.TryAddSingleton<IConditionCompiler, ConditionCompiler>();
        services.TryAddSingleton<IConditionEvaluator, ConditionEvaluator>();
        return services;
    }
}
