// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Scheduling.Abstractions.Tests;

/// <summary>
/// 读取 spec/conformance/vectors/scheduling 下的一致性向量
/// </summary>
/// <remarks>
/// 向量是规范的一部分，测试只回放，不另存一份。对象模型要认得文件里的每个成员：文件里缺了模型要的成员、多出模型不认得的成员都读取失败，向量换了写法时不会漏掉字段还照样通过
/// </remarks>
internal static class ConformanceVectors
{
    /// <summary>
    /// 文件顶部给人看的说明，不属于对象模型
    /// </summary>
    private const string CommentMember = "$comment";

    /// <summary>
    /// 向量文件的反序列化选项
    /// </summary>
    /// <remarks>
    /// Web 默认（camelCase、不区分大小写），外加成员必须与对象模型一一对应：构造参数都要在文件里，文件里的成员都要有去处
    /// </remarks>
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>
    /// 调度向量所在目录
    /// </summary>
    public static string Directory { get; } = Path.Combine(LocateRoot(), "spec", "conformance", "vectors", "scheduling");

    /// <summary>
    /// 读取并反序列化一个向量文件
    /// </summary>
    /// <typeparam name="T">向量的对象模型</typeparam>
    /// <param name="fileName">相对于 <see cref="Directory"/> 的文件名</param>
    public static T Load<T>(string fileName)
    {
        var path = Path.Combine(Directory, fileName);
        var document = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? throw new InvalidOperationException($"Vector file '{path}' is not a JSON object.");
        document.Remove(CommentMember);
        return document.Deserialize<T>(_options) ?? throw new InvalidOperationException($"Vector file '{path}' is empty.");
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
