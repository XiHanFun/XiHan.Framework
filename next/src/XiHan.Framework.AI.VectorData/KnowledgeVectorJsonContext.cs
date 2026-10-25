// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.AI.VectorData;

/// <summary>
/// 元数据字典的序列化上下文。
/// </summary>
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class KnowledgeVectorJsonContext : JsonSerializerContext;
