// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 删不掉临时目录时查占用者的诊断
/// </summary>
/// <remarks>
/// <see cref="FileHolders"/> 只在 <see cref="PluginOutputLimitTests"/> 重试用尽时用到，平时走不到；这里让本进程占着一个文件，核对查得出来（ADR-0338）
/// </remarks>
public sealed class FileHoldersTests : IDisposable
{
    /// <summary>
    /// 本用例的临时目录
    /// </summary>
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("xihan-holders-");

    /// <inheritdoc />
    public void Dispose() => _directory.Delete(recursive: true);

    [Fact]
    public void Describe_NamesTheProcessHoldingAFile_AndTheRemainingEntries()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows() || OperatingSystem.IsLinux(), "Holders are only looked up on Windows and Linux.");
        var path = Path.Combine(_directory.FullName, "held.txt");
        string described;
        using (var held = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            described = FileHolders.Describe(_directory.FullName);
        }

        Assert.Contains($"(pid {Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}", described, StringComparison.Ordinal);
        Assert.Contains("remaining entries: held.txt", described, StringComparison.Ordinal);
        // 文件关掉之后没有占用者
        Assert.StartsWith("holders: none;", FileHolders.Describe(_directory.FullName), StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_OfAMissingDirectory_SaysSoWithoutThrowing()
    {
        var missing = Path.Combine(_directory.FullName, "missing");

        var described = FileHolders.Describe(missing);

        Assert.EndsWith("remaining entries: none", described, StringComparison.Ordinal);
    }
}
