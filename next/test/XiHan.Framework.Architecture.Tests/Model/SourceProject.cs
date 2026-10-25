// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Architecture.Tests.Model;

/// <summary>
/// 一个正式包的项目文件事实：包 ID、项目引用与包引用。
/// </summary>
/// <param name="PackageId">包 ID；项目文件未声明时取文件名。</param>
/// <param name="Directory">项目目录。</param>
/// <param name="ProjectPath">项目文件路径。</param>
/// <param name="ProjectReferences">引用的项目（以文件名即包 ID 表示）。</param>
/// <param name="PackageReferences">引用的 NuGet 包。</param>
internal sealed record SourceProject(
    string PackageId,
    string Directory,
    string ProjectPath,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<PackageReference> PackageReferences)
{
    /// <summary>
    /// 从项目文件读取事实。只读 XML，不求值 MSBuild 属性，因此引用必须在项目文件里显式写出。
    /// </summary>
    /// <param name="csprojPath">项目文件路径。</param>
    public static SourceProject Load(string csprojPath)
    {
        var document = Workspace.LoadXml(csprojPath);
        var packageId = document.Descendants("PackageId").FirstOrDefault()?.Value
            ?? Path.GetFileNameWithoutExtension(csprojPath);

        var projectReferences = document.Descendants("ProjectReference")
            .Select(static e => Path.GetFileNameWithoutExtension((string?)e.Attribute("Include") ?? string.Empty))
            .ToList();

        // PrivateAssets 既可以是属性也可以是子元素，两种写法都识别。
        var packageReferences = document.Descendants("PackageReference")
            .Select(static e => new PackageReference(
                (string?)e.Attribute("Include") ?? string.Empty,
                string.Equals((string?)e.Attribute("PrivateAssets") ?? e.Element("PrivateAssets")?.Value, "all", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return new SourceProject(
            packageId,
            Path.GetDirectoryName(csprojPath)!,
            csprojPath,
            projectReferences,
            packageReferences);
    }
}
