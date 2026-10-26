// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Calendars;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Abstractions.Schedules;

namespace XiHan.Framework.Scheduling.Abstractions;

/// <summary>
/// 作业、作业类型、日历与引擎的登记。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class SchedulingServiceCollectionExtensions
{
    /// <summary>
    /// 登记一个作业类型（作用域服务），键为类型全名；运行时登记的作业引用它。
    /// </summary>
    public static IServiceCollection AddJobType<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJob>(this IServiceCollection services)
        where TJob : class, IJob
        => services.AddJobType<TJob>(JobTypeRegistry.KeyOf(typeof(TJob)));

    /// <summary>
    /// 以指定键登记一个作业类型（作用域服务）。
    /// </summary>
    public static IServiceCollection AddJobType<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJob>(this IServiceCollection services, string key)
        where TJob : class, IJob
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<TJob>();
        services.GetJobTypeRegistry().Add(key, typeof(TJob));
        return services;
    }

    /// <summary>
    /// 登记一个代码定义的作业（手动计划即没有触发器）；作业是作用域服务。
    /// </summary>
    public static IServiceCollection AddJob<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TJob>(this IServiceCollection services, JobName name, JobSchedule schedule)
        where TJob : class, IJob
        => services.AddJob<TJob>(new JobDefinition(name, schedule, typeof(TJob)));

    /// <summary>
    /// 登记一个代码定义的作业；作业是作用域服务。同名重复登记是编程错误。
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

        services.AddJobType<TJob>();
        services.GetJobRegistry().Add(definition);
        return services;
    }

    /// <summary>
    /// 登记一个排除日历。
    /// </summary>
    public static IServiceCollection AddJobCalendar(this IServiceCollection services, string name, IJobCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.GetJobCalendars().Add(name, calendar);
        return services;
    }

    /// <summary>
    /// 取（没有就创建）代码作业登记表。
    /// </summary>
    public static JobRegistry GetJobRegistry(this IServiceCollection services)
        => services.GetOrAddInstance(static () => new JobRegistry());

    /// <summary>
    /// 取（没有就创建）作业类型登记表。
    /// </summary>
    public static JobTypeRegistry GetJobTypeRegistry(this IServiceCollection services)
        => services.GetOrAddInstance(static () => new JobTypeRegistry());

    /// <summary>
    /// 取（没有就创建）日历登记表。
    /// </summary>
    public static JobCalendars GetJobCalendars(this IServiceCollection services)
        => services.GetOrAddInstance(static () => new JobCalendars());

    /// <summary>
    /// 装配引擎：Provider 在注册了 <c>IJobStore</c> 与 <c>ILeaderElection</c> 后调用；引擎同时作为 <see cref="IJobScheduler"/>。
    /// </summary>
    public static IServiceCollection AddJobSchedulerEngine(this IServiceCollection services, SchedulerOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.TryAddSingleton(options);
        services.GetJobRegistry();
        services.GetJobTypeRegistry();
        services.GetJobCalendars();
        services.TryAddSingleton<JobDispatcher>();
        services.TryAddSingleton<JobSchedulerEngine>();
        services.TryAddSingleton<IJobScheduler>(static sp => sp.GetRequiredService<JobSchedulerEngine>());
        return services;
    }

    /// <summary>
    /// 登记表以实例注册，多次调用返回同一对象，特性与应用可以共同填充。
    /// </summary>
    private static T GetOrAddInstance<T>(this IServiceCollection services, Func<T> create)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(services);
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(T) && descriptor.ImplementationInstance is T existing)
            {
                return existing;
            }
        }

        var instance = create();
        services.AddSingleton(instance);
        return instance;
    }
}
