// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Hosting.CommandLine.Commands;
using XiHan.Framework.Hosting.CommandLine.Runtime;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine;

/// <summary>
/// 命令行宿主的服务注册
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class CommandLineServiceCollectionExtensions
{
    /// <summary>
    /// 注册命令行宿主
    /// </summary>
    /// <remarks>
    /// 运行器、内置命令（help、graph、pipeline、operations、invoke）、本包的消息目录，并把日志改为只写 stderr（stdout 只承载命令输出，管道与重定向才安全）。启动 Banner 的选项换成缺省关闭，配置 <c>XiHan:Kernel:Banner:Enabled=true</c> 打开时写 stderr；之后再登记的选项优先。幂等
    /// </remarks>
    public static IServiceCollection AddXiHanCommandLine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<CommandLineRunner>();
        services.AddMessageCatalog(CommandLineMessages.Catalog);
        services.AddXiHanCommand<HelpCommand>()
            .AddXiHanCommand<GraphCommand>()
            .AddXiHanCommand<PipelineCommand>()
            .AddXiHanCommand<OperationsCommand>()
            .AddXiHanCommand<InvokeCommand>();
        // 通用宿主默认把控制台日志写到 stdout：这里清掉默认提供器，只留写 stderr 的控制台日志
        services.AddLogging(static logging => logging.ClearProviders().AddConsole(static console => console.LogToStandardErrorThreshold = LogLevel.Trace));
        // Banner 同理不能进 stdout（命令输出的 JSON、MCP stdio 的消息流）：先于或晚于 AddXiHan 调用都替换掉宿主包与内核登记的选项；
        // 命令每次执行都会启动一次宿主，缺省不打印，显式打开时才写 stderr
        services.RemoveAll<StartupBannerOptions>();
        services.AddSingleton(static provider =>
        {
            var defaults = new StartupBannerOptions { Enabled = false, Output = Console.Error, ApplicationVersion = EntryAssemblyVersion.Resolve() };
            return provider.GetService<IConfiguration>() is { } configuration ? StartupBannerOptions.FromConfiguration(configuration, defaults) : defaults;
        });
        return services;
    }

    /// <summary>
    /// 注册一个命令（单例）
    /// </summary>
    /// <remarks>
    /// 同一类型只注册一次
    /// </remarks>
    public static IServiceCollection AddXiHanCommand<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand>(this IServiceCollection services)
        where TCommand : class, ICommand
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ICommand, TCommand>());
        return services;
    }
}
