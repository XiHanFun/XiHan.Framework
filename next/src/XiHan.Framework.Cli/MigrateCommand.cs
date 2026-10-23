// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Cli.Migration;
using XiHan.Framework.Hosting.CommandLine;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli;

/// <summary>
/// <c>migrate v4 [--path &lt;目录|.csproj|.slnx&gt;] [--fixers V4-01,V4-03] [--dry-run] [--report &lt;文件&gt;]</c>：按 <c>spec/migration/v4.md</c> 把 4.x 代码改写成 5.0，打印每条规则的改动与标记。
/// </summary>
internal sealed class MigrateCommand : ICommand
{
    /// <summary>
    /// 支持的迁移来源。
    /// </summary>
    private const string SupportedVersion = "v4";

    /// <inheritdoc />
    public string Name => "migrate";

    /// <inheritdoc />
    public string DescriptionKey => CliMessages.MigrateDescription;

    /// <inheritdoc />
    public async ValueTask<int> ExecuteAsync(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var version = context.Arguments.Count > 0 ? context.Arguments[0] : string.Empty;
        if (!string.Equals(version, SupportedVersion, StringComparison.OrdinalIgnoreCase))
        {
            return await CommandLineFailure.WriteAsync(context.Error, Error.Validation(CliErrors.MigrateVersionUnknown, CliMessages.MigrateVersionUnknown, MessageArguments.Of(("version", version))), context.Services, null, CommandLineExitCodes.Usage).ConfigureAwait(false);
        }

        var path = Path.GetFullPath(context.Option("path").TryGetValue(out var explicitPath) ? explicitPath : ".");
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            return await CommandLineFailure.WriteAsync(context.Error, Error.NotFound(CliErrors.MigratePathNotFound, CliMessages.MigratePathNotFound, MessageArguments.Of(("path", path))), context.Services, null).ConfigureAwait(false);
        }

        var fixers = V4Rules.All;
        if (context.Option("fixers").TryGetValue(out var selection))
        {
            var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in selection.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!V4Rules.Ids.Contains(id, StringComparer.OrdinalIgnoreCase))
                {
                    return await CommandLineFailure.WriteAsync(context.Error, Error.Validation(CliErrors.MigrateFixerUnknown, CliMessages.MigrateFixerUnknown, MessageArguments.Of(("fixer", id), ("known", string.Join(", ", V4Rules.Ids)))), context.Services, null, CommandLineExitCodes.Usage).ConfigureAwait(false);
                }

                selected.Add(id);
            }

            fixers = V4Rules.All.Where(fixer => selected.Contains(fixer.Id)).ToList();
        }

        var dryRun = context.HasOption("dry-run");
        var report = new V4Migration(path, fixers, dryRun).Run(context.CancellationToken);
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
