// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;

namespace XiHan.Framework.Primitives.Tests;

/// <summary>
/// 读取 spec/conformance/vectors/primitives 下的一致性向量。
/// </summary>
/// <remarks>
/// 向量是规范的一部分，测试只回放，不另存一份。
/// </remarks>
internal static class ConformanceVectors
{
    /// <summary>
    /// 向量文件的反序列化选项：Web 默认（camelCase、不区分大小写）。
    /// </summary>
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 原语层向量所在目录。
    /// </summary>
    public static string Directory { get; } = Path.Combine(LocateRoot(), "spec", "conformance", "vectors", "primitives");

    /// <summary>
    /// 读取并反序列化一个向量文件。
    /// </summary>
    /// <typeparam name="T">向量的对象模型。</typeparam>
    /// <param name="fileName">相对于 <see cref="Directory"/> 的文件名。</param>
    public static T Load<T>(string fileName)
    {
        var path = Path.Combine(Directory, fileName);
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, _options) ?? throw new InvalidOperationException($"Vector file '{path}' is empty.");
    }

    /// <summary>
    /// 从测试输出目录向上查找 next 工作区根（以 <c>XiHan.Framework.Next.slnx</c> 为标记）。
    /// </summary>
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

        throw new InvalidOperationException("Could not locate 'XiHan.Framework.Next.slnx'.");
    }
}
