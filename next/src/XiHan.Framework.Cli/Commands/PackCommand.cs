// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Cli.Packaging;
using XiHan.Framework.Hosting.CommandLine;
using XiHan.Framework.Hosting.CommandLine.Commands;
using XiHan.Framework.Hosting.CommandLine.Runtime;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Commands;

/// <summary>
/// <c>pack [--manifest &lt;path&gt;] [--output &lt;dir&gt;] [--only dockerfile,kustomize,compose,aspire]</c>：从应用清单生成部署产物，逐行输出写出的文件。
/// </summary>
internal sealed class PackCommand : ICommand
{
    /// <inheritdoc />
    public string Name => "pack";

    /// <inheritdoc />
    public string DescriptionKey => CliMessages.PackDescription;

    /// <inheritdoc />
    public async ValueTask<int> ExecuteAsync(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var manifestPath = Path.GetFullPath(context.Option("manifest").TryGetValue(out var explicitManifest) ? explicitManifest : AppManifestReader.DefaultFileName);
        if (!File.Exists(manifestPath))
        {
            return await CommandLineFailure.WriteAsync(context.Error, Error.NotFound(CliErrors.ManifestNotFound, CliMessages.ManifestNotFound, MessageArguments.Of(("path", manifestPath))), context.Services, null).ConfigureAwait(false);
        }

        var read = AppManifestReader.Read(manifestPath);
        if (!read.TryGetValue(out var manifest))
        {
            return await CommandLineFailure.WriteAsync(context.Error, read.Error!, context.Services, null).ConfigureAwait(false);
        }

        var kinds = PackKinds.All;
        if (context.Option("only").TryGetValue(out var only))
        {
            kinds = PackKinds.None;
            foreach (var name in only.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Enum.TryParse<PackKinds>(name, ignoreCase: true, out var kind) || kind is PackKinds.None or PackKinds.All)
                {
                    return await CommandLineFailure.WriteAsync(context.Error, Error.Validation(CliErrors.ManifestInvalid, CliMessages.PackKindUnknown, MessageArguments.Of(("kind", name))), context.Services, null, CommandLineExitCodes.Usage).ConfigureAwait(false);
                }

                kinds |= kind;
            }
        }

        var manifestDirectory = Path.GetDirectoryName(manifestPath)!;
        var outputDirectory = Path.GetFullPath(context.Option("output").TryGetValue(out var explicitOutput) ? explicitOutput : Path.Combine(manifestDirectory, "deploy"));
        var outputs = Packager.Generate(manifest, manifestDirectory, outputDirectory, kinds);
        await Packager.WriteAsync(outputs, outputDirectory, context.CancellationToken).ConfigureAwait(false);
        foreach (var output in outputs)
        {
            await context.Output.WriteLineAsync(output.Path.AsMemory(), context.CancellationToken).ConfigureAwait(false);
        }

        await context.Output.WriteLineAsync(CliMessages.PackWritten.L(("count", outputs.Count), ("output", outputDirectory)).AsMemory(), context.CancellationToken).ConfigureAwait(false);
        return CommandLineExitCodes.Success;
    }
}
