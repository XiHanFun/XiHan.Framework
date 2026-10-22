// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.ObjectStorage.FileSystem.Tests;

/// <summary>
/// 每个用例一个临时根目录，释放时递归删除。
/// </summary>
internal sealed class TemporaryRoot : IDisposable
{
    /// <summary>
    /// 创建并建目录。
    /// </summary>
    public TemporaryRoot()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xihan-blobs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>
    /// 根目录路径。
    /// </summary>
    public string Path { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
