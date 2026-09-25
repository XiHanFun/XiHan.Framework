// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Timing.Extensions.DependencyInjection;

namespace XiHan.Framework.Timing.Tests.Extensions.DependencyInjection;

/// <summary>
/// 时间服务注册扩展测试
/// </summary>
/// <remarks>
/// 生命周期在这里是硬契约：时钟、时区提供器与当前时区提供器都是单例。
/// 当前时区按异步流存放，隔离边界是异步流而不是实例，
/// 所以它与单例时钟共用同一个实例；并发请求不串时区由末尾的请求流用例锁死。
/// </remarks>
public class ServiceCollectionExtensionsTests
{
    private const string ShanghaiTimeZone = "Asia/Shanghai";
    private const string TokyoTimeZone = "Asia/Tokyo";

    /// <summary>
    /// 换算样本：2024-03-15 02:00 UTC（上海与东京均无夏令时，偏移恒为 +8 / +9）
    /// </summary>
    private static readonly DateTime UtcInstant = new(2024, 3, 15, 2, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 扩展方法返回同一个服务集合，支持链式调用
    /// </summary>
    [Fact]
    public void AddXiHanTiming_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddXiHanTiming();

        Assert.Same(services, returned);
    }

    /// <summary>
    /// 时钟注册为单例，实现为默认时钟
    /// </summary>
    [Fact]
    public void AddXiHanTiming_RegistersClockAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddXiHanTiming();

        var descriptor = services.Single(item => item.ServiceType == typeof(IClock));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(Clock), descriptor.ImplementationType);
    }

    /// <summary>
    /// 时区提供器注册为单例，实现为 TimeZoneConverter 封装
    /// </summary>
    [Fact]
    public void AddXiHanTiming_RegistersTimezoneProviderAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddXiHanTiming();

        var descriptor = services.Single(item => item.ServiceType == typeof(ITimezoneProvider));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(TZConvertTimezoneProvider), descriptor.ImplementationType);
    }

    /// <summary>
    /// 当前时区提供器注册为单例，与单例时钟共用同一个实例
    /// </summary>
    [Fact]
    public void AddXiHanTiming_RegistersCurrentTimezoneProviderAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddXiHanTiming();

        var descriptor = services.Single(item => item.ServiceType == typeof(ICurrentTimezoneProvider));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(CurrentTimezoneProvider), descriptor.ImplementationType);
    }

    /// <summary>
    /// 注册后三个服务都能解析出来，且生命周期与声明一致
    /// </summary>
    [Fact]
    public void AddXiHanTiming_ResolvesServicesWithDeclaredLifetimes()
    {
        var services = new ServiceCollection();
        services.AddXiHanTiming();

        using var provider = services.BuildServiceProvider();

        var clock = provider.GetRequiredService<IClock>();
        var timezoneProvider = provider.GetRequiredService<ITimezoneProvider>();
        var currentTimezoneProvider = provider.GetRequiredService<ICurrentTimezoneProvider>();
        using var scope = provider.CreateScope();

        Assert.IsType<Clock>(clock);
        Assert.IsType<TZConvertTimezoneProvider>(timezoneProvider);
        Assert.IsType<CurrentTimezoneProvider>(currentTimezoneProvider);
        Assert.Same(clock, provider.GetRequiredService<IClock>());
        Assert.Same(timezoneProvider, provider.GetRequiredService<ITimezoneProvider>());
        Assert.Same(currentTimezoneProvider, provider.GetRequiredService<ICurrentTimezoneProvider>());
        Assert.Same(currentTimezoneProvider, scope.ServiceProvider.GetRequiredService<ICurrentTimezoneProvider>());
    }

    /// <summary>
    /// 容器在开启作用域校验时也能构建，说明单例时钟没有俘获作用域依赖
    /// </summary>
    [Fact]
    public void AddXiHanTiming_BuildsWithScopeValidationEnabled()
    {
        var services = new ServiceCollection();
        services.AddXiHanTiming();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        Assert.IsType<Clock>(provider.GetRequiredService<IClock>());
    }

    /// <summary>
    /// 未额外配置时，时钟选项保持未指定，时钟不宣称支持多时区
    /// </summary>
    [Fact]
    public void AddXiHanTiming_WithoutConfiguration_LeavesClockKindUnspecified()
    {
        var services = new ServiceCollection();
        services.AddXiHanTiming();

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<XiHanClockOptions>>();
        var clock = provider.GetRequiredService<IClock>();

        Assert.Equal(DateTimeKind.Unspecified, options.Value.Kind);
        Assert.Equal(DateTimeKind.Unspecified, clock.Kind);
        Assert.False(clock.SupportsMultipleTimezone);
    }

    /// <summary>
    /// 配置为 UTC 后，解析出的时钟按 UTC 语义工作
    /// </summary>
    [Fact]
    public void AddXiHanTiming_WhenKindConfiguredAsUtc_ClockHonoursConfiguredKind()
    {
        var services = new ServiceCollection();
        services.AddXiHanTiming();
        services.Configure<XiHanClockOptions>(options => options.Kind = DateTimeKind.Utc);

        using var provider = services.BuildServiceProvider();

        var clock = provider.GetRequiredService<IClock>();

        Assert.Equal(DateTimeKind.Utc, clock.Kind);
        Assert.True(clock.SupportsMultipleTimezone);
        Assert.Equal(DateTimeKind.Utc, clock.Now.Kind);
    }

    /// <summary>
    /// 时钟选项在扩展方法之前配置同样生效，注册顺序不影响结果
    /// </summary>
    [Fact]
    public void AddXiHanTiming_WhenKindConfiguredBeforeRegistration_StillHonoursConfiguredKind()
    {
        var services = new ServiceCollection();
        services.Configure<XiHanClockOptions>(options => options.Kind = DateTimeKind.Local);
        services.AddXiHanTiming();

        using var provider = services.BuildServiceProvider();

        var clock = provider.GetRequiredService<IClock>();

        Assert.Equal(DateTimeKind.Local, clock.Kind);
        Assert.False(clock.SupportsMultipleTimezone);
    }

    /// <summary>
    /// 请求内给注入的当前时区提供器赋值后，单例时钟按该时区换算
    /// </summary>
    /// <remarks>
    /// 这是请求级时区的基本用法：业务侧与时钟从容器拿到的是否同一个实例不应影响结果，
    /// 时区必须按当前异步流共享，否则单例时钟读不到赋值，把 UTC 时间原样返回。
    /// </remarks>
    [Fact]
    public async Task AddXiHanTiming_WhenInjectedCurrentTimezoneAssigned_ClockConvertsIntoAssignedTimezone()
    {
        using var provider = BuildUtcClockServiceProvider();
        var token = TestContext.Current.CancellationToken;

        var assigned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var converted = await ConvertInRequestFlowAsync(provider, ShanghaiTimeZone, assigned, Task.CompletedTask, token);

        Assert.Equal(new DateTime(2024, 3, 15, 10, 0, 0), converted);
    }

    /// <summary>
    /// 并发请求各自的时区互不串值，也不回流到发起方
    /// </summary>
    /// <remarks>
    /// 两个请求流先各自写入时区，互相等到对方也写完之后才换算，保证两次赋值在时间上重叠；
    /// 若时区被存成进程级共享状态，其中一个请求会读到另一个请求的时区。
    /// </remarks>
    [Fact]
    public async Task AddXiHanTiming_AcrossConcurrentRequestFlows_ClockUsesEachFlowsOwnTimezone()
    {
        using var provider = BuildUtcClockServiceProvider();
        var token = TestContext.Current.CancellationToken;
        var shanghaiAssigned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tokyoAssigned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var shanghai = ConvertInRequestFlowAsync(provider, ShanghaiTimeZone, shanghaiAssigned, tokyoAssigned.Task, token);
        var tokyo = ConvertInRequestFlowAsync(provider, TokyoTimeZone, tokyoAssigned, shanghaiAssigned.Task, token);

        Assert.Equal(new DateTime(2024, 3, 15, 10, 0, 0), await shanghai);
        Assert.Equal(new DateTime(2024, 3, 15, 11, 0, 0), await tokyo);
        Assert.Null(provider.GetRequiredService<ICurrentTimezoneProvider>().TimeZone);
        Assert.Equal(UtcInstant, provider.GetRequiredService<IClock>().ConvertToUserTime(UtcInstant));
    }

    /// <summary>
    /// 构造按 UTC 存储、启用多时区换算的默认服务容器
    /// </summary>
    /// <returns>服务容器</returns>
    private static ServiceProvider BuildUtcClockServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddXiHanTiming();
        services.Configure<XiHanClockOptions>(options => options.Kind = DateTimeKind.Utc);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    /// <summary>
    /// 在独立异步流中模拟一次请求：开请求作用域，给注入的当前时区提供器赋值，再用时钟换算样本时间
    /// </summary>
    /// <param name="root">根容器</param>
    /// <param name="timeZone">本次请求的时区</param>
    /// <param name="assigned">写入时区后发出的信号</param>
    /// <param name="convertAfter">换算前需等待的信号</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户时间</returns>
    private static Task<DateTime> ConvertInRequestFlowAsync(
        IServiceProvider root,
        string timeZone,
        TaskCompletionSource assigned,
        Task convertAfter,
        CancellationToken cancellationToken)
    {
        return Task.Run(
            async () =>
            {
                using var scope = root.CreateScope();
                scope.ServiceProvider.GetRequiredService<ICurrentTimezoneProvider>().TimeZone = timeZone;
                assigned.SetResult();

                await convertAfter.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

                return scope.ServiceProvider.GetRequiredService<IClock>().ConvertToUserTime(UtcInstant);
            },
            cancellationToken);
    }
}
