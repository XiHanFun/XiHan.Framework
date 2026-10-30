// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;
using XiHan.Framework.Hosting.AspNetCore.Health;

namespace XiHan.Framework.Hosting.AspNetCore;

/// <summary>
/// 宿主响应体的源生成序列化上下文。
/// </summary>
/// <remarks>
/// 驼峰属性名、忽略 <c>null</c>，保持 AOT 友好。Problem Details 由 <see cref="ProblemResponse.WriteTo"/> 手写，不在这里。
/// </remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(HealthResponse))]
// 健康条目数据的值是运行期才知道类型的标量：逐一登记，源生成的序列化按运行时类型找到它们
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(byte))]
[JsonSerializable(typeof(sbyte))]
[JsonSerializable(typeof(short))]
[JsonSerializable(typeof(ushort))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(uint))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(ulong))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(decimal))]
internal sealed partial class HostingJsonContext : JsonSerializerContext;
