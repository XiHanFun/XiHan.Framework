// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Cli.Integration;

namespace XiHan.Framework.Cli.Tests.Integration;

/// <summary>
/// 项目根与引用的框架版本。
/// </summary>
public sealed class ProjectDetectorTests
{
    [Fact]
    public void Detect_FindsTheGitRootAboveAndTheCentralFrameworkVersion()
    {
        using var project = new ProjectFixture("5.0.0-preview.1");

        var info = ProjectDetector.Detect(project.ProjectDirectory);

        Assert.Equal(Path.GetFullPath(project.Root), info.Root);
        Assert.Equal("5.0.0-preview.1", info.FrameworkVersion);
    }

    [Fact]
    public void Detect_TakesTheMostReferencedVersion_AndSkipsBuildOutput()
    {
        using var project = new ProjectFixture("4.6.0");
        File.WriteAllText(Path.Combine(project.ProjectDirectory, "Orders.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="XiHan.Framework.Kernel" Version="5.0.0" />
                <PackageReference Include="XiHan.Framework.Domain" Version="5.0.0" />
                <PackageReference Include="XiHan.Framework.Caching.Redis" Version="5.0.0" />
              </ItemGroup>
            </Project>
            """);
        Directory.CreateDirectory(Path.Combine(project.ProjectDirectory, "obj"));
        File.WriteAllText(Path.Combine(project.ProjectDirectory, "obj", "Stale.csproj"), """<Project><ItemGroup><PackageReference Include="XiHan.Framework.A" Version="1.0.0" /><PackageReference Include="XiHan.Framework.B" Version="1.0.0" /><PackageReference Include="XiHan.Framework.C" Version="1.0.0" /><PackageReference Include="XiHan.Framework.D" Version="1.0.0" /></ItemGroup></Project>""");

        Assert.Equal("5.0.0", ProjectDetector.Detect(project.Root).FrameworkVersion);
    }

    [Fact]
    public void Detect_WithoutFrameworkReferences_HasNoVersion_AndMissingDirectoryHasNoRoot()
    {
        using var project = new ProjectFixture(null);

        Assert.Equal(new ProjectInfo(Path.GetFullPath(project.Root), null), ProjectDetector.Detect(project.Root));
        Assert.Equal(new ProjectInfo(null, null), ProjectDetector.Detect(Path.Combine(project.Root, "missing")));
    }
}
