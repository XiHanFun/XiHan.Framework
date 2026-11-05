// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Persistence.Conformance.Fixtures;

/// <summary>
/// 套件操作的请求与响应的源生成序列化元数据
/// </summary>
[JsonSerializable(typeof(ConformanceScriptRequest))]
[JsonSerializable(typeof(ConformanceScriptResponse))]
internal sealed partial class ConformanceJsonContext : JsonSerializerContext;
