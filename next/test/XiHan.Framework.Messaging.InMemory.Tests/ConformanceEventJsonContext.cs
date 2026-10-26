// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;
using XiHan.Framework.Persistence.Conformance.Fixtures;

namespace XiHan.Framework.Messaging.InMemory.Tests;

/// <summary>
/// 套件领域事件的源生成序列化上下文（camelCase）。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ConformanceEvent))]
internal sealed partial class ConformanceEventJsonContext : JsonSerializerContext;
