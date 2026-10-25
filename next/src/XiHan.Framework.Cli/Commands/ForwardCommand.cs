// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Cli.Packaging;
using XiHan.Framework.Hosting.CommandLine.Commands;
using XiHan.Framework.Hosting.CommandLine.Runtime;

namespace XiHan.Framework.Cli.Commands;

/// <summary>
/// 把命令转发给应用自己的命令行宿主：<c>dotnet run --project &lt;project&gt; --configuration &lt;c&gt; -- &lt;命令&gt; …</c>。
/// 项目取 <c>--project</c>，否则取当前目录 <c>xihan.app.json</c> 的 <c>project</c>，否则交给 <c>dotnet run</c> 自己找。
/// </summary>
internal sealed class ForwardCommand : ICommand
{
    /// <summary>
    /// 本工具自己消费、不转发的选项。
    /// </summary>
    private static readonly string[] _ownOptions = ["project", "configuration"];

    /// <summary>
    /// 子进程启动器。
    /// </summary>
    private readonly IProcessLauncher _launcher;

    /// <summary>
    /// 创建。
    /// </summary>
    public ForwardCommand(string name, string descriptionKey, IProcessLauncher launcher)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        DescriptionKey = descriptionKey ?? throw new ArgumentNullException(nameof(descriptionKey));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string DescriptionKey { get; }

    /// <inheritdoc />
    public async ValueTask<int> ExecuteAsync(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var arguments = new List<string> { "run" };
        var project = context.Option("project").TryGetValue(out var explicitProject) ? explicitProject : ProjectFromManifest();
        if (project is not null)
        {
            arguments.Add("--project");
            arguments.Add(project);
        }

        arguments.Add("--configuration");
        arguments.Add(context.Option("configuration").TryGetValue(out var configuration) ? configuration : "Release");
        arguments.Add("--");
        arguments.Add(Name);
        arguments.AddRange(context.Arguments);
        // 其余选项原样转发；标志在解析时已成 "true"，转发成 "--flag true" 语义相同
        foreach (var (option, values) in context.CommandLine.Options)
        {
            if (Array.IndexOf(_ownOptions, option) >= 0)
            {
                continue;
            }

            foreach (var value in values)
            {
                arguments.Add("--" + option);
                arguments.Add(value);
            }
        }

        var exit = await _launcher.RunAsync("dotnet", arguments, null, context.Output, context.Error, context.CancellationToken).ConfigureAwait(false);
        return exit.TryGetValue(out var code)
            ? code
            : await CommandLineFailure.WriteAsync(context.Error, exit.Error!, context.Services, null).ConfigureAwait(false);
    }

    /// <summary>
    /// 当前目录有合法的应用清单时取它的项目；没有或不合法时不猜。
    /// </summary>
    private static string? ProjectFromManifest()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), AppManifestReader.DefaultFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        return AppManifestReader.Read(path).TryGetValue(out var manifest) ? Path.Combine(Path.GetDirectoryName(path)!, manifest.Project) : null;
    }
}
