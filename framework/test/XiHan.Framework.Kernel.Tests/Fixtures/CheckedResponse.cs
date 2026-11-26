// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 写出的文本要过 JSON 文本检查的响应
/// </summary>
/// <remarks>
/// 每个成员对应一种可能写出超过 64 层或重复成员的写法，没用到的是 <see langword="null"/>、不写出
/// </remarks>
internal sealed class CheckedResponse
{
    /// <summary>
    /// 与扩展数据的键可能同名的声明成员
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 一层套一层的对象
    /// </summary>
    public DepthNode? Nest { get; set; }

    /// <summary>
    /// JSON 元素
    /// </summary>
    public JsonElement? Element { get; set; }

    /// <summary>
    /// JSON 节点
    /// </summary>
    public JsonNode? Node { get; set; }

    /// <summary>
    /// 键按命名策略改写的字典
    /// </summary>
    public IReadOnlyDictionary<string, int>? Counts { get; set; }

    /// <summary>
    /// 原样嵌进去的文本
    /// </summary>
    public RawJson? Raw { get; set; }

    /// <summary>
    /// 转换器写两次同名成员的对象
    /// </summary>
    public RepeatedMember? Repeated { get; set; }

    /// <summary>
    /// 扩展数据
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
