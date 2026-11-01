// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 索引文件与知识库工具输入输出的序列化元数据。
/// </summary>
/// <remarks>
/// 属性名小驼峰，空值不写。工具输出与索引文件都用 <see cref="Relaxed"/> / <see cref="Files"/>：不转义中文，文档摘录原样可读。
/// </remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CatalogDocument))]
[JsonSerializable(typeof(ConfigurationDocument))]
[JsonSerializable(typeof(InfoRequest))]
[JsonSerializable(typeof(InfoResponse))]
[JsonSerializable(typeof(ConfigRequest))]
[JsonSerializable(typeof(ConfigResponse))]
[JsonSerializable(typeof(SearchRequest))]
[JsonSerializable(typeof(SearchResponse))]
internal sealed partial class KnowledgeJsonContext : JsonSerializerContext
{
    /// <summary>
    /// 紧凑、不转义中文的实例，用于工具的输入输出。
    /// </summary>
    public static KnowledgeJsonContext Relaxed { get; } = new(CreateOptions(indented: false));

    /// <summary>
    /// 缩进、LF 换行、不转义中文的实例，用于索引文件。
    /// </summary>
    public static KnowledgeJsonContext Files { get; } = new(CreateOptions(indented: true));

    /// <summary>
    /// 与源生成选项一致、另设编码器与缩进的选项。
    /// </summary>
    private static JsonSerializerOptions CreateOptions(bool indented) => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // 输出给编码助手与人读：中文与标点原样写出，否则每个字都成了 \uXXXX
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = indented,
        NewLine = "\n",
    };
}
