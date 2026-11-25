// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Testing.Workspace;

/// <summary>
/// 定位框架工作区的根目录
/// </summary>
/// <remarks>
/// 框架工作区是解决方案文件 <c>XiHan.Framework.slnx</c> 所在的目录（仓库里的 <c>framework/</c>），从测试的输出目录 <see cref="AppContext.BaseDirectory"/> 逐级向上找。测试读规范（<see cref="SpecVectors"/>）、示例与别的项目的构建产物时以它为起点。只在框架仓库的检出里找得到，所以放在不发包的测试类库里（ADR-0300）
/// </remarks>
public static class FrameworkWorkspace
{
    /// <summary>
    /// 标记工作区根的解决方案文件名
    /// </summary>
    internal const string SolutionFile = "XiHan.Framework.slnx";

    /// <summary>
    /// 第一次读取时找到的根目录
    /// </summary>
    /// <remarks>
    /// 找不到时缓存那个异常，之后每次读取抛出同一个
    /// </remarks>
    private static readonly Lazy<string> _root = new(static () => Locate(AppContext.BaseDirectory), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// 工作区根目录的绝对路径
    /// </summary>
    /// <exception cref="InvalidOperationException">测试的输出目录之上没有 <c>XiHan.Framework.slnx</c></exception>
    public static string Root => _root.Value;

    /// <summary>
    /// 从给定目录起逐级向上找工作区根
    /// </summary>
    /// <param name="start">起点目录，含它自己</param>
    /// <returns>第一个含 <c>XiHan.Framework.slnx</c> 的目录的绝对路径</returns>
    /// <exception cref="InvalidOperationException">起点与它之上的目录都没有这个文件</exception>
    internal static string Locate(string start)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(start);
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFile)))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"The framework workspace root (the directory of '{SolutionFile}') was not found at or above '{start}'; the test must run inside a checkout of the framework repository.");
    }
}
