// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests;

/// <summary>
/// 读取 spec/conformance/vectors/multitenancy 下的一致性向量
/// </summary>
/// <remarks>
/// 向量是规范的一部分，测试只回放，不另存一份
/// </remarks>
internal static class ConformanceVectors
{
    /// <summary>
    /// 向量文件的反序列化选项
    /// </summary>
    /// <remarks>
    /// Web 默认（camelCase、不区分大小写）；对象模型里没有缺省值的构造参数必须出现在文件里，向量改了字段名时读取就失败，不会悄悄读成空
    /// </remarks>
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web) { RespectRequiredConstructorParameters = true };

    /// <summary>
    /// 多租户档向量所在目录
    /// </summary>
    public static string Directory { get; } = Path.Combine(LocateRoot(), "spec", "conformance", "vectors", "multitenancy");

    /// <summary>
    /// 读取并反序列化一个向量文件
    /// </summary>
    /// <typeparam name="T">向量的对象模型</typeparam>
    /// <param name="fileName">相对于 <see cref="Directory"/> 的文件名</param>
    public static T Load<T>(string fileName)
    {
        var path = Path.Combine(Directory, fileName);
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, _options) ?? throw new InvalidOperationException($"Vector file '{path}' is empty.");
    }

    /// <summary>
    /// 从测试输出目录向上查找框架工作区根（以 <c>XiHan.Framework.slnx</c> 为标记）
    /// </summary>
    private static string LocateRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "XiHan.Framework.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("The framework workspace root (the directory of 'XiHan.Framework.slnx') was not found above the test output directory.");
    }
}
