// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 读取 spec/conformance/vectors 下的一致性向量
/// </summary>
/// <remarks>
/// 向量是规范的一部分，参考实现与各语言 SDK 回放同一份文件，测试不另存副本
/// </remarks>
internal static class SpecVectors
{
    /// <summary>
    /// 向量文件的反序列化选项：Web 默认（camelCase）
    /// </summary>
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 向量的根目录
    /// </summary>
    public static string Directory { get; } = Path.Combine(LocateRoot(), "spec", "conformance", "vectors");

    /// <summary>
    /// 读取并反序列化一个向量文件
    /// </summary>
    /// <typeparam name="T">向量的对象模型</typeparam>
    /// <param name="relativePath">相对 <see cref="Directory"/> 的路径，如 <c>pipeline/ordering.json</c></param>
    public static T Load<T>(string relativePath)
    {
        var path = Path.Combine(Directory, relativePath);
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, _options) ?? throw new InvalidOperationException($"Vector file '{path}' is empty.");
    }

    /// <summary>
    /// 从测试输出目录向上查找框架工作区根（以 <c>XiHan.Framework.slnx</c> 为标记）
    /// </summary>
    private static string LocateRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "XiHan.Framework.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate 'XiHan.Framework.slnx'.");
    }
}
