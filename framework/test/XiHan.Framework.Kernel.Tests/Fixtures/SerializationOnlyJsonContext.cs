// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 只生成快速路径的 <see cref="MeasureRequest"/> 序列化上下文
/// </summary>
/// <remarks>
/// <c>GenerationMode = Serialization</c>：只能按生成时的选项写出，给不出按元数据的写法，也读不了。成员名与 <see cref="MeasureJsonContext"/> 相同
/// </remarks>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Serialization, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MeasureRequest))]
internal sealed partial class SerializationOnlyJsonContext : JsonSerializerContext;
