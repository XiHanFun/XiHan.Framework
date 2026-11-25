// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Testing.Workspace.Tests;

/// <summary>
/// <see cref="FrameworkWorkspace"/> 怎样找工作区根、找不到时怎样报错
/// </summary>
public sealed class FrameworkWorkspaceTests : IDisposable
{
    /// <summary>
    /// 放临时目录树的目录
    /// </summary>
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("xihan-workspace-");

    /// <inheritdoc />
    public void Dispose() => _directory.Delete(recursive: true);

    [Fact]
    public void Root_HoldsTheSolutionFile_AndContainsTheTestOutput()
    {
        var root = FrameworkWorkspace.Root;

        Assert.True(File.Exists(Path.Combine(root, FrameworkWorkspace.SolutionFile)));
        Assert.StartsWith(root, AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.Same(root, FrameworkWorkspace.Root);
    }

    [Fact]
    public void Locate_WalksUpFromTheGivenDirectory_ToTheFirstThatHoldsTheSolutionFile()
    {
        var workspace = _directory.CreateSubdirectory("workspace");
        File.WriteAllText(Path.Combine(workspace.FullName, FrameworkWorkspace.SolutionFile), "<Solution />");
        var nested = workspace.CreateSubdirectory(Path.Combine("test", "bin", "Release"));
        // 更深处另有一份时取最近的那个
        var inner = nested.CreateSubdirectory("inner");
        File.WriteAllText(Path.Combine(inner.FullName, FrameworkWorkspace.SolutionFile), "<Solution />");

        Assert.Equal(workspace.FullName, FrameworkWorkspace.Locate(nested.FullName));
        Assert.Equal(workspace.FullName, FrameworkWorkspace.Locate(workspace.FullName));
        Assert.Equal(inner.FullName, FrameworkWorkspace.Locate(inner.FullName));
    }

    [Fact]
    public void Locate_WithoutTheSolutionFileAbove_SaysWhatItLookedForAndWhere()
    {
        var start = _directory.CreateSubdirectory("elsewhere").FullName;

        var error = Assert.Throws<InvalidOperationException>(() => FrameworkWorkspace.Locate(start));

        Assert.Contains($"'{FrameworkWorkspace.SolutionFile}'", error.Message, StringComparison.Ordinal);
        Assert.Contains(start, error.Message, StringComparison.Ordinal);
        Assert.ThrowsAny<ArgumentException>(() => FrameworkWorkspace.Locate(" "));
    }
}
