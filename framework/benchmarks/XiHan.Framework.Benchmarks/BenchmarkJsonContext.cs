// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Benchmarks;

/// <summary>
/// 基准用的源生成序列化上下文
/// </summary>
/// <remarks>
/// 选项与生成时一致：System.Text.Json 写出时可以走源生成的快速路径
/// </remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(OrderSummary))]
internal sealed partial class BenchmarkJsonContext : JsonSerializerContext;
