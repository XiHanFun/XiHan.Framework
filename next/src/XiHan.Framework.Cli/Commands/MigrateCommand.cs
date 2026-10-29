// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Cli.Migration;
using XiHan.Framework.Hosting.CommandLine;
using XiHan.Framework.Hosting.CommandLine.Commands;
using XiHan.Framework.Hosting.CommandLine.Runtime;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Commands;

/// <summary>
/// 命令 <c>migrate</c>：按来源版本的规则集改写应用代码。
/// </summary>
/// <remarks>
/// 用法：<c>migrate &lt;来源版本&gt; [--path &lt;目录|.csproj|.slnx&gt;] [--fixers &lt;规则编号,…&gt;] [--dry-run] [--report &lt;文件&gt;]</c>。
/// 按来源版本的规则集（<c>spec/migration/README.md</c>）改写应用代码，打印每条规则的改动与标记。
/// </remarks>
/// <param name="ruleSets">工具认识的规则集；运行时是 <see cref="MigrationRuleSets.All"/>，测试可以换成自己的。</param>
internal sealed class MigrateCommand(IReadOnlyList<MigrationRuleSet> ruleSets) : ICommand
{
    /// <inheritdoc />
    public string Name => "migrate";

    /// <inheritdoc />
    public string DescriptionKey => CliMessages.MigrateDescription;

    /// <inheritdoc />
    public async ValueTask<int> ExecuteAsync(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var version = context.Arguments.Count > 0 ? context.Arguments[0] : string.Empty;
        var ruleSet = ruleSets.FirstOrDefault(set => string.Equals(set.Version, version, StringComparison.OrdinalIgnoreCase));
        if (ruleSet is null)
        {
            // 同一个错误码，文案区分「有规则集但版本不对」与「这一版工具没有任何规则集」
            var error = ruleSets.Count == 0
                ? Error.Validation(CliErrors.MigrateVersionUnknown, CliMessages.MigrateNoRuleSets, MessageArguments.Of(("version", version)))
                : Error.Validation(CliErrors.MigrateVersionUnknown, CliMessages.MigrateVersionUnknown, MessageArguments.Of(("version", version), ("known", string.Join(", ", ruleSets.Select(static set => set.Version)))));
            return await CommandLineFailure.WriteAsync(context.Error, error, context.Services, null, CommandLineExitCodes.Usage).ConfigureAwait(false);
        }

        var path = Path.GetFullPath(context.Option("path").TryGetValue(out var explicitPath) ? explicitPath : ".");
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            return await CommandLineFailure.WriteAsync(context.Error, Error.NotFound(CliErrors.MigratePathNotFound, CliMessages.MigratePathNotFound, MessageArguments.Of(("path", path))), context.Services, null).ConfigureAwait(false);
        }

        var fixers = ruleSet.Fixers;
        if (context.Option("fixers").TryGetValue(out var selection))
        {
            var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in selection.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!ruleSet.Ids.Contains(id, StringComparer.OrdinalIgnoreCase))
                {
                    return await CommandLineFailure.WriteAsync(context.Error, Error.Validation(CliErrors.MigrateFixerUnknown, CliMessages.MigrateFixerUnknown, MessageArguments.Of(("fixer", id), ("known", string.Join(", ", ruleSet.Ids)))), context.Services, null, CommandLineExitCodes.Usage).ConfigureAwait(false);
                }

                selected.Add(id);
            }

            // 子集仍按规则集的顺序执行
            fixers = ruleSet.Fixers.Where(fixer => selected.Contains(fixer.Id)).ToList();
        }

        var dryRun = context.HasOption("dry-run");
        var report = new MigrationRunner(path, ruleSet.Version, fixers, dryRun).Run(context.CancellationToken);
        await WriteTableAsync(context, report).ConfigureAwait(false);
        if (context.Option("report").TryGetValue(out var reportPath))
        {
            var fullPath = Path.GetFullPath(reportPath);
            await File.WriteAllTextAsync(fullPath, JsonSerializer.Serialize(report, MigrationJsonContext.Default.MigrationReport), new UTF8Encoding(false), context.CancellationToken).ConfigureAwait(false);
            await context.Output.WriteLineAsync(CliMessages.MigrateReportWritten.L(("path", fullPath)).AsMemory(), context.CancellationToken).ConfigureAwait(false);
        }

        if (dryRun)
        {
            await context.Output.WriteLineAsync(CliMessages.MigrateDryRun.L().AsMemory(), context.CancellationToken).ConfigureAwait(false);
        }

        return CommandLineExitCodes.Success;
    }

    /// <summary>
    /// 终端表格：每条规则一行，再列全部标记，最后一行汇总。
    /// </summary>
    private static async Task WriteTableAsync(CommandContext context, MigrationReport report)
    {
        var headers = CliMessages.MigrateTableHeader.L().Split('|');
        var widths = headers.Select(static header => Math.Max(header.Length, 8)).ToArray();
        var builder = new StringBuilder();
        builder.Append(headers[0].PadRight(widths[0]));
        for (var index = 1; index < headers.Length; index++)
        {
            builder.Append("  ").Append(headers[index].PadLeft(widths[index]));
        }

        await context.Output.WriteLineAsync(builder.ToString().AsMemory(), context.CancellationToken).ConfigureAwait(false);
        foreach (var rule in report.Rules)
        {
            builder.Clear();
            builder.Append(rule.Id.PadRight(widths[0]));
            builder.Append("  ").Append(rule.Changed.ToString(CultureInfo.InvariantCulture).PadLeft(widths[1]));
            builder.Append("  ").Append(rule.Markers.ToString(CultureInfo.InvariantCulture).PadLeft(widths[2]));
            builder.Append("  ").Append(rule.Files.Count.ToString(CultureInfo.InvariantCulture).PadLeft(widths[3]));
            await context.Output.WriteLineAsync(builder.ToString().AsMemory(), context.CancellationToken).ConfigureAwait(false);
        }

        if (report.Markers.Count > 0)
        {
            await context.Output.WriteLineAsync().ConfigureAwait(false);
            foreach (var marker in report.Markers)
            {
                await context.Output.WriteLineAsync($"{marker.File}:{marker.Line.ToString(CultureInfo.InvariantCulture)}  {marker.Rule}  {marker.Message}".AsMemory(), context.CancellationToken).ConfigureAwait(false);
            }
        }

        var files = report.Rules.SelectMany(static rule => rule.Files).Distinct(StringComparer.Ordinal).Count();
        await context.Output.WriteLineAsync().ConfigureAwait(false);
        await context.Output.WriteLineAsync(CliMessages.MigrateSummary.L(("files", files), ("changed", report.Rules.Sum(static rule => rule.Changed)), ("markers", report.Markers.Count)).AsMemory(), context.CancellationToken).ConfigureAwait(false);
    }
}
