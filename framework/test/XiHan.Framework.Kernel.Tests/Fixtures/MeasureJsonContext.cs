// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// <see cref="MeasureRequest"/> 的源生成序列化上下文
/// </summary>
/// <remarks>
/// 小驼峰成员名，没有值的成员不写，原始调用的载荷按同样的名字手写。选项与生成时一致，System.Text.Json 写出时走源生成的快速路径
/// </remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MeasureRequest))]
internal sealed partial class MeasureJsonContext : JsonSerializerContext;
