// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;

namespace XiHan.Framework.Primitives.Tests;

/// <summary>
/// 读取 spec/conformance/vectors/primitives 下的一致性向量。向量是规范的一部分，测试只回放，不另存一份。
/// </summary>
internal static class ConformanceVectors
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    public static string Directory { get; } = Path.Combine(LocateRoot(), "spec", "conformance", "vectors", "primitives");

    public static T Load<T>(string fileName)
    {
        var path = Path.Combine(Directory, fileName);
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, _options) ?? throw new InvalidOperationException($"向量文件 {path} 为空。");
    }

    private static string LocateRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "XiHan.Framework.Next.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("找不到 XiHan.Framework.Next.slnx。");
    }
}
