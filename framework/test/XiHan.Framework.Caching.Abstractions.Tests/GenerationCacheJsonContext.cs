// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;
using XiHan.Framework.Caching.Abstractions.Caches;

namespace XiHan.Framework.Caching.Abstractions.Tests;

/// <summary>
/// 测试组件的缓存类型：代数缓存条目与代数
/// </summary>
[JsonSerializable(typeof(GenerationCacheEntry<string[]>))]
[JsonSerializable(typeof(string))]
internal sealed partial class GenerationCacheJsonContext : JsonSerializerContext;
