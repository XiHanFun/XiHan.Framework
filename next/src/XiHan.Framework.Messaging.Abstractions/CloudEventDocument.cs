// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Messaging.Abstractions;

/// <summary>
/// CloudEvents 结构化 JSON 文档。
/// </summary>
internal sealed class CloudEventDocument
{
    /// <summary>
    /// 规范版本 <c>specversion</c>；必须是 <c>1.0</c>。
    /// </summary>
    [JsonPropertyName("specversion")]
    public string? SpecVersion { get; set; }

    /// <summary>
    /// 事件 ID <c>id</c>。
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// 来源 <c>source</c>。
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    /// <summary>
    /// 事件类型 <c>type</c>。
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// 发生时间 <c>time</c>，RFC 3339 文本。
    /// </summary>
    [JsonPropertyName("time")]
    public string? Time { get; set; }

    /// <summary>
    /// 主题标识 <c>subject</c>。
    /// </summary>
    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    /// <summary>
    /// 数据内容类型 <c>datacontenttype</c>。
    /// </summary>
    [JsonPropertyName("datacontenttype")]
    public string? DataContentType { get; set; }

    /// <summary>
    /// 内联的 JSON 数据 <c>data</c>；与 <see cref="DataBase64"/> 互斥。
    /// </summary>
    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }

    /// <summary>
    /// Base64 编码的二进制数据 <c>data_base64</c>；与 <see cref="Data"/> 互斥。
    /// </summary>
    [JsonPropertyName("data_base64")]
    public string? DataBase64 { get; set; }

    /// <summary>
    /// 其余顶层成员，即 CloudEvents 扩展属性。
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }
}
