// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Cli.Integration;
using XiHan.Framework.Cli.Knowledge;
using XiHan.Framework.Hosting.CommandLine;
using XiHan.Framework.Hosting.CommandLine.Commands;
using XiHan.Framework.Hosting.CommandLine.Runtime;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Commands;

/// <summary>
/// 命令 <c>setup</c>：把知识库 MCP 服务写进编码助手的配置。
/// </summary>
/// <remarks>
/// 用法：<c>setup --client &lt;claude|cursor|vscode|codex&gt; [--mode &lt;mcp|skill|both&gt;] [--pin &lt;版本&gt;] [--path &lt;目录&gt;] [--dry-run] [--check]</c>。
/// 版本取 <c>--pin</c>，否则取项目引用的 <c>XiHan.Framework.*</c> 版本，再否则取本工具的版本；重复执行结果相同，JSON 配置只改本服务一项。
/// </remarks>
internal sealed class SetupCommand : ICommand
{
    /// <inheritdoc />
    public string Name => "setup";

    /// <inheritdoc />
    public string DescriptionKey => CliMessages.SetupDescription;

    /// <inheritdoc />
    public async ValueTask<int> ExecuteAsync(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var client = context.Option("client").TryGetValue(out var explicitClient) ? explicitClient : string.Empty;
        if (!SetupPlanner.Clients.Contains(client, StringComparer.Ordinal))
        {
            return await UsageAsync(context, Error.Validation(CliErrors.SetupOptionInvalid, CliMessages.SetupClientInvalid, MessageArguments.Of(("client", client), ("known", string.Join(", ", SetupPlanner.Clients))))).ConfigureAwait(false);
        }

        var mode = context.Option("mode").TryGetValue(out var explicitMode) ? explicitMode : "both";
        if (!SetupPlanner.Modes.Contains(mode, StringComparer.Ordinal))
        {
            return await UsageAsync(context, Error.Validation(CliErrors.SetupOptionInvalid, CliMessages.SetupModeInvalid, MessageArguments.Of(("mode", mode), ("known", string.Join(", ", SetupPlanner.Modes))))).ConfigureAwait(false);
        }

        var start = Path.GetFullPath(context.Option("path").TryGetValue(out var explicitPath) ? explicitPath : ".");
        var project = ProjectDetector.Detect(start);
        if (project.Root is null)
        {
            return await CommandLineFailure.WriteAsync(context.Error, Error.NotFound(CliErrors.SetupRootNotFound, CliMessages.SetupRootNotFound, MessageArguments.Of(("path", start))), context.Services, null).ConfigureAwait(false);
        }

        var version = context.Option("pin").TryGetValue(out var pin) ? pin : project.FrameworkVersion ?? FrameworkKnowledge.Version;
        var actions = SetupPlanner.Plan(client, mode, project.Root, version, OperatingSystem.IsWindows(), FrameworkKnowledge.ReadSkill());
        if (context.HasOption("check"))
        {
            return await CheckAsync(context, client, version, actions).ConfigureAwait(false);
        }

        // 先把要合并的 JSON 配置都读通再动手：任何一份读不了就一个文件都不写，不留下改了一半的项目
        foreach (var action in actions.Where(static a => a.Kind == SetupActionKind.Json))
        {
            var read = SetupPlanner.ReadConfig(action.Path);
            if (!read.IsSuccess)
            {
                return await CommandLineFailure.WriteAsync(context.Error, read.Error!, context.Services, null).ConfigureAwait(false);
            }
        }

        var dryRun = context.HasOption("dry-run");
        foreach (var action in actions)
        {
            if (!dryRun)
            {
                var applied = SetupPlanner.Apply(action);
                if (!applied.IsSuccess)
                {
                    return await CommandLineFailure.WriteAsync(context.Error, applied.Error!, context.Services, null).ConfigureAwait(false);
                }
            }

            await WriteAsync(context, Describe(action, dryRun)).ConfigureAwait(false);
            if (action.Kind != SetupActionKind.File)
            {
                await WriteAsync(context, SetupPlanner.Snippet(action)).ConfigureAwait(false);
            }
        }

        return CommandLineExitCodes.Success;
    }

    /// <summary>
    /// 一条动作的说明。
    /// </summary>
    private static string Describe(SetupAction action, bool dryRun) => action.Kind switch
    {
        SetupActionKind.Json => (dryRun ? CliMessages.SetupJsonPlanned : CliMessages.SetupJsonWritten).L(("server", KnowledgeFeature.ServerName), ("key", action.Key), ("path", action.Path)),
        SetupActionKind.File => (dryRun ? CliMessages.SetupFilePlanned : CliMessages.SetupFileWritten).L(("path", action.Path)),
        _ => CliMessages.SetupPrint.L(("path", action.Path)),
    };

    /// <summary>
    /// 核对全部动作是否已生效。
    /// </summary>
    /// <remarks>
    /// 都已生效时成功，否则以 <see cref="CliErrors.SetupNotReady"/> 列出未生效的项。
    /// </remarks>
    private static async Task<int> CheckAsync(CommandContext context, string client, string version, IReadOnlyList<SetupAction> actions)
    {
        var problems = actions.Select(SetupPlanner.Check).OfType<string>().ToList();
        if (problems.Count > 0)
        {
            var error = Error.Conflict(CliErrors.SetupNotReady, CliMessages.SetupNotReady, MessageArguments.Of(("client", client), ("problems", string.Join("; ", problems))));
            return await CommandLineFailure.WriteAsync(context.Error, error, context.Services, null).ConfigureAwait(false);
        }

        await WriteAsync(context, CliMessages.SetupReady.L(("client", client), ("version", version))).ConfigureAwait(false);
        return CommandLineExitCodes.Success;
    }

    /// <summary>
    /// 用法错误：按 64 退出。
    /// </summary>
    private static Task<int> UsageAsync(CommandContext context, Error error)
        => CommandLineFailure.WriteAsync(context.Error, error, context.Services, null, CommandLineExitCodes.Usage);

    /// <summary>
    /// 写一行到标准输出。
    /// </summary>
    private static Task WriteAsync(CommandContext context, string text)
        => context.Output.WriteLineAsync(text.AsMemory(), context.CancellationToken);
}
