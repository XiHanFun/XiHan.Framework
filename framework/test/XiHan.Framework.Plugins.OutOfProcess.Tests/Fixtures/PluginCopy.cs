// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;

/// <summary>
/// 示例插件输出目录的一份临时副本
/// </summary>
/// <remarks>
/// 用例在副本上改清单或拿掉文件，不碰示例的构建产物；释放时删除副本（应在宿主停止之后）。EchoPlugin 从自己所在目录读清单，改副本里的清单等于同时改了宿主与插件两边看到的清单
/// </remarks>
internal sealed class PluginCopy : IDisposable
{
    /// <summary>
    /// 复制示例插件的输出目录
    /// </summary>
    public PluginCopy(string source)
    {
        Directory = Path.Combine(Path.GetTempPath(), "xihan-plugin-tests", Guid.NewGuid().ToString("N"));
        CopyTree(source, Directory);
    }

    /// <summary>
    /// 副本目录
    /// </summary>
    public string Directory { get; }

    /// <summary>
    /// 副本里的清单路径
    /// </summary>
    public string ManifestPath => Path.Combine(Directory, "xihan.manifest.json");

    /// <summary>
    /// 改写副本里的清单文本
    /// </summary>
    public void EditManifest(Func<string, string> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        var original = File.ReadAllText(ManifestPath);
        var edited = edit(original);
        if (string.Equals(original, edited, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The manifest edit did not change anything.");
        }

        File.WriteAllText(ManifestPath, edited);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 插件进程刚退出时文件句柄可能还没释放：临时目录留给系统清理
        }
    }

    /// <summary>
    /// 递归复制目录（Unix 上 File.Copy 保留可执行位）
    /// </summary>
    private static void CopyTree(string source, string target)
    {
        System.IO.Directory.CreateDirectory(target);
        foreach (var file in System.IO.Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        foreach (var directory in System.IO.Directory.EnumerateDirectories(source))
        {
            CopyTree(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }
}
