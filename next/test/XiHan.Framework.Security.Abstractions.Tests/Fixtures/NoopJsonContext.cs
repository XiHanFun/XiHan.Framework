// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Security.Abstractions.Tests.Fixtures;

/// <summary>
/// 空请求与空响应的源生成序列化上下文。
/// </summary>
[JsonSerializable(typeof(NoopRequest))]
[JsonSerializable(typeof(NoopResponse))]
internal sealed partial class NoopJsonContext : JsonSerializerContext;
