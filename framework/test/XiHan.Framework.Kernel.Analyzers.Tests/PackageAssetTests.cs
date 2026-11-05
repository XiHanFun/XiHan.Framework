// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace XiHan.Framework.Kernel.Analyzers.Tests;

/// <summary>
/// 分析器包随带的 MSBuild 资产
/// </summary>
/// <remarks>
/// 规则范围靠 <c>build_property.*</c> 读 MSBuild 属性，属性必须先声明为 <c>CompilerVisibleProperty</c>：包的使用者不该自己补声明，所以包里要带一份 <c>buildTransitive</c> props，且与分析器实际读取的属性、仓库内的声明保持一致
/// </remarks>
public sealed partial class PackageAssetTests
{
    /// <summary>
    /// 包 ID，也是 props 的文件名
    /// </summary>
    private const string PackageId = "XiHan.Framework.Kernel.Analyzers";

    [Fact]
    public void BuildTransitiveProps_DeclaresEveryPropertyTheRulesRead()
    {
        var source = Path.Combine(FindFrameworkRoot(), "src", PackageId);
        var declared = CompilerVisibleProperties(Path.Combine(source, "buildTransitive", PackageId + ".props"));
        var read = Directory.EnumerateFiles(source, "*.cs")
            .SelectMany(static file => BuildProperty().Matches(File.ReadAllText(file)).Select(static match => match.Groups["name"].Value))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(["XiHanFrameworkPackage", "XiHanRing"], read.Order(StringComparer.Ordinal));
        Assert.Equal(read.Order(StringComparer.Ordinal), declared.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void BuildTransitiveProps_MatchesTheRepositoryDeclarations()
    {
        var framework = FindFrameworkRoot();
        var packaged = CompilerVisibleProperties(Path.Combine(framework, "src", PackageId, "buildTransitive", PackageId + ".props"));
        var repository = CompilerVisibleProperties(Path.Combine(framework, "src", "Directory.Build.props"));

        Assert.Equal(repository.Order(StringComparer.Ordinal), packaged.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Project_PacksThePropsIntoBuildTransitive()
    {
        var project = XDocument.Load(Path.Combine(FindFrameworkRoot(), "src", PackageId, PackageId + ".csproj"));

        var packed = project.Descendants("None")
            .Where(static item => (string?)item.Attribute("Pack") == "true" && (string?)item.Attribute("PackagePath") == "buildTransitive/")
            .Select(static item => (string?)item.Attribute("Include"))
            .ToList();

        Assert.Equal(["buildTransitive/$(PackageId).props"], packed);
        Assert.Equal(PackageId, project.Descendants("PackageId").Single().Value);
    }

    /// <summary>
    /// 一份 MSBuild 文件声明的 <c>CompilerVisibleProperty</c>
    /// </summary>
    private static List<string> CompilerVisibleProperties(string path)
        => XDocument.Load(path).Descendants("CompilerVisibleProperty").Select(static item => (string)item.Attribute("Include")!).ToList();

    /// <summary>
    /// 从测试输出目录向上找 <c>framework/</c>（含解决方案文件的目录）
    /// </summary>
    private static string FindFrameworkRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "XiHan.Framework.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The framework/ workspace root was not found above the test output directory.");
    }

    /// <summary>
    /// 分析器源码里读取的 <c>build_property.&lt;名字&gt;</c>
    /// </summary>
    [GeneratedRegex("\"build_property\\.(?<name>[A-Za-z0-9_]+)\"")]
    private static partial Regex BuildProperty();
}
