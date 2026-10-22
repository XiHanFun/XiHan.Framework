// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Cli.Packaging;

namespace XiHan.Framework.Cli.Tests;

/// <summary>
/// 仓库里提交的示例部署产物必须与其清单一致：重新生成后逐文件比较。
/// </summary>
public sealed class SampleDeployTests
{
    [Fact]
    public void KubernetesSample_CommittedDeployFolder_MatchesItsManifest()
    {
        var next = FindNextRoot();
        var sample = Path.Combine(next, "samples", "KubernetesApp");
        var manifest = AppManifestReader.Read(Path.Combine(sample, "xihan.app.json")).ValueOrThrow();
        var outputDirectory = Path.Combine(sample, "deploy");
        var outputs = Packager.Generate(manifest, sample, outputDirectory, PackKinds.All);

        Assert.NotEmpty(outputs);
        foreach (var output in outputs)
        {
            var path = Path.Combine(outputDirectory, output.Path.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"'{output.Path}' is missing; run xihan pack for samples/KubernetesApp.");
            Assert.Equal(output.Content, File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// 从测试输出目录向上找 <c>next/</c>（含解决方案文件的目录）。
    /// </summary>
    private static string FindNextRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "XiHan.Framework.Next.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The next/ workspace root was not found above the test output directory.");
    }
}
