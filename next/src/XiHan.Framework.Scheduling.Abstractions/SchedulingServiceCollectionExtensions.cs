// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions;

/// <summary>
/// 作业登记扩展。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class SchedulingServiceCollectionExtensions
{
    /// <summary>
    /// 登记作业：作业注册为作用域服务，每次运行在新作用域内解析。
    /// </summary>
    public static IServiceCollection AddJob<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJob>(this IServiceCollection services, JobName name, JobSchedule schedule)
        where TJob : class, IJob
        => services.AddJob<TJob>(new JobDefinition(name, schedule, typeof(TJob)));

    /// <summary>
    /// 以完整定义登记作业；<see cref="JobDefinition.JobType"/> 必须是 <typeparamref name="TJob"/>。
    /// </summary>
    public static IServiceCollection AddJob<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJob>(this IServiceCollection services, JobDefinition definition)
        where TJob : class, IJob
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.JobType != typeof(TJob))
        {
            throw new ArgumentException($"The definition's job type '{definition.JobType}' must be {typeof(TJob)}.", nameof(definition));
        }

        services.TryAddScoped<TJob>();
        services.GetJobRegistry().Add(definition);
        return services;
    }

    /// <summary>
    /// 取（必要时创建）作业登记表。
    /// </summary>
    public static JobRegistry GetJobRegistry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // 登记表以实例注册，多次调用返回同一对象，特性与应用可以共同填充
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(JobRegistry) && descriptor.ImplementationInstance is JobRegistry existing)
            {
                return existing;
            }
        }

        var registry = new JobRegistry();
        services.AddSingleton(registry);
        return registry;
    }
}
