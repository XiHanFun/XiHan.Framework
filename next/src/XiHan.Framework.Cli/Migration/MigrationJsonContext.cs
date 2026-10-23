// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 迁移报告的序列化上下文：camelCase、缩进。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(MigrationReport))]
internal sealed partial class MigrationJsonContext : JsonSerializerContext;
