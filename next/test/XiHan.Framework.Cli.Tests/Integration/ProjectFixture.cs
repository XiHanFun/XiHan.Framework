// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Tests.Integration;

/// <summary>
/// 临时目录里的一个项目：git 根、中央包管理与一个子项目
/// </summary>
/// <remarks>
/// 释放时删除目录
/// </remarks>
internal sealed class ProjectFixture : IDisposable
{
    /// <summary>
    /// 创建
    /// </summary>
    /// <remarks>
    /// <paramref name="frameworkVersion"/> 为空时不引用框架包
    /// </remarks>
    public ProjectFixture(string? frameworkVersion)
    {
        Root = Path.Combine(Path.GetTempPath(), "xihan-cli-project-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(Root, ".git"));
        Directory.CreateDirectory(Path.Combine(Root, "src", "Orders"));
        File.WriteAllText(Path.Combine(Root, "src", "Orders", "Orders.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        if (frameworkVersion is not null)
        {
            File.WriteAllText(Path.Combine(Root, "Directory.Packages.props"), $"""
                <Project>
                  <ItemGroup>
                    <PackageVersion Include="XiHan.Framework.Kernel" Version="{frameworkVersion}" />
                    <PackageVersion Include="XiHan.Framework.Hosting.AspNetCore" Version="{frameworkVersion}" />
                    <PackageVersion Include="Other.Package" Version="9.9.9" />
                  </ItemGroup>
                </Project>
                """);
        }
    }

    /// <summary>
    /// 项目根
    /// </summary>
    public string Root { get; }

    /// <summary>
    /// 根下的子项目目录
    /// </summary>
    public string ProjectDirectory => Path.Combine(Root, "src", "Orders");

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
