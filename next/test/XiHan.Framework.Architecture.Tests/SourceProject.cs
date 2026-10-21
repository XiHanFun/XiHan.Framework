// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 一个正式包的项目文件事实：包 ID、项目引用与包引用。
/// </summary>
internal sealed record SourceProject(
    string PackageId,
    string Directory,
    string ProjectPath,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<PackageReference> PackageReferences)
{
    public static SourceProject Load(string csprojPath)
    {
        var document = Workspace.LoadXml(csprojPath);
        var packageId = document.Descendants("PackageId").FirstOrDefault()?.Value
            ?? Path.GetFileNameWithoutExtension(csprojPath);

        var projectReferences = document.Descendants("ProjectReference")
            .Select(static e => Path.GetFileNameWithoutExtension((string?)e.Attribute("Include") ?? string.Empty))
            .ToList();

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
