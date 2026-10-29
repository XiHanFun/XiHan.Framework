// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 按种类调用各生成器并写文件。
/// </summary>
/// <remarks>
/// 每次整份覆盖已有产物，不删除输出目录里的其他文件。
/// </remarks>
internal static class Packager
{
    /// <summary>
    /// 生成产物。
    /// </summary>
    public static IReadOnlyList<PackOutput> Generate(AppManifest manifest, string manifestDirectory, string outputDirectory, PackKinds kinds)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(manifestDirectory);
        ArgumentNullException.ThrowIfNull(outputDirectory);
        var outputs = new List<PackOutput>();
        // 构建上下文默认是清单所在目录；单仓库里可指向仓库根，项目路径与 Dockerfile 路径都相对它
        var contextDirectory = Path.GetFullPath(Path.Combine(manifestDirectory, manifest.Build.Context));
        if (kinds.HasFlag(PackKinds.Dockerfile))
        {
            outputs.AddRange(DockerfileGenerator.Generate(manifest, Relative(contextDirectory, Path.Combine(manifestDirectory, manifest.Project))));
        }

        if (kinds.HasFlag(PackKinds.Kustomize))
        {
            outputs.AddRange(KustomizeGenerator.Generate(manifest, manifestDirectory));
        }

        if (kinds.HasFlag(PackKinds.Compose))
        {
            // Compose 文件里的上下文相对 compose 文件自己，Dockerfile 相对上下文
            var context = Relative(outputDirectory, contextDirectory);
            var dockerfile = Relative(contextDirectory, Path.Combine(outputDirectory, "Dockerfile"));
            outputs.Add(ComposeGenerator.Generate(manifest, context, dockerfile));
        }

        if (kinds.HasFlag(PackKinds.Aspire))
        {
            outputs.Add(AspireGenerator.Generate(manifest));
        }

        return outputs;
    }

    /// <summary>
    /// 写到输出目录，LF 换行、UTF-8 无 BOM。
    /// </summary>
    public static async Task WriteAsync(IReadOnlyList<PackOutput> outputs, string outputDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(outputDirectory);
        foreach (var output in outputs)
        {
            var path = Path.Combine(outputDirectory, output.Path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, output.Content, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 正斜杠的相对路径。
    /// </summary>
    /// <remarks>
    /// 相同目录为 <c>.</c>。
    /// </remarks>
    private static string Relative(string from, string to)
    {
        var relative = Path.GetRelativePath(from, to).Replace('\\', '/');
        return relative.Length == 0 ? "." : relative;
    }
}
