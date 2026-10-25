// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Cli.Commands;
using XiHan.Framework.Hosting.CommandLine;
using XiHan.Framework.Hosting.CommandLine.Commands;
using XiHan.Framework.Hosting.CommandLine.Parsing;
using XiHan.Framework.Hosting.CommandLine.Runtime;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli;

/// <summary>
/// 组装工具的服务与命令并执行一条命令行。工具不是 XiHan 应用：没有能力图谱与管道，只借用命令行宿主的解析、帮助、失败输出与退出码。
/// </summary>
internal static class CliHost
{
    /// <summary>
    /// 执行。
    /// </summary>
    public static ValueTask<int> RunAsync(IReadOnlyList<string> args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
        => RunAsync(args, output, error, new ProcessLauncher(), cancellationToken);

    /// <summary>
    /// 以指定的子进程启动器执行（测试用）。
    /// </summary>
    public static async ValueTask<int> RunAsync(IReadOnlyList<string> args, TextWriter output, TextWriter error, IProcessLauncher launcher, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(launcher);
        var services = new ServiceCollection();
        // 文案按 --locale 或当前 UI 文化协商；工具自己的目录加上命令行宿主与内核的目录，失败文案才能翻译
        var localization = new LocalizationOptions();
        var requested = CommandLineParser.Parse(args).Option("locale").TryGetValue(out var locale) ? locale : CultureInfo.CurrentUICulture.Name;
        localization = localization with { DefaultLocale = localization.Negotiate([requested]) };
        var localizer = new MessageLocalizer(
            [CliMessages.Catalog, CommandLineMessages.Catalog, MessageCatalog.FromEmbeddedResources("primitives", typeof(Error)), MessageCatalog.FromEmbeddedResources("kernel", typeof(Kernel.Hosting.App))],
            localization);
        services.AddSingleton<IMessageLocalizer>(localizer);
        services.AddSingleton<IMessageResolver>(localizer);
        services.AddSingleton(launcher);
        services.AddXiHanCommand<HelpCommand>();
        services.AddXiHanCommand<VersionCommand>();
        services.AddXiHanCommand<PackCommand>();
        services.AddXiHanCommand<MigrateCommand>();
        foreach (var (name, description) in new[] { ("graph", CliMessages.GraphDescription), ("pipeline", CliMessages.PipelineDescription), ("operations", CliMessages.OperationsDescription), ("invoke", CliMessages.InvokeDescription) })
        {
            services.AddSingleton<ICommand>(provider => new ForwardCommand(name, description, provider.GetRequiredService<IProcessLauncher>()));
        }

        services.AddSingleton(static provider => new CommandLineRunner(provider, provider.GetServices<ICommand>()));
        await using var provider = services.BuildServiceProvider();
        MessageScope.SetDefault(localizer, localization.DefaultLocale);
        return await provider.GetRequiredService<CommandLineRunner>().RunAsync(args, output, error, cancellationToken).ConfigureAwait(false);
    }
}
