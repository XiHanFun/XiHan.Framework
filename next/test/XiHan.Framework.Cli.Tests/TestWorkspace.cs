// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Tests;

/// <summary>
/// 测试读取的工作区目录。
/// </summary>
internal static class TestWorkspace
{
    /// <summary>
    /// <c>next/</c>：从测试输出目录向上找含解决方案文件的目录。
    /// </summary>
    public static string NextRoot { get; } = Locate();

    /// <summary>
    /// 向上查找。
    /// </summary>
    private static string Locate()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "XiHan.Framework.Next.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The next/ workspace root was not found above the test output directory.");
    }
}
