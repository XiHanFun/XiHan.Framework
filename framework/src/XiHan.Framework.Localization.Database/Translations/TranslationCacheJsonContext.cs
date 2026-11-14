// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Localization.Database.Translations;

/// <summary>
/// 翻译版本戳的源生成序列化上下文
/// </summary>
/// <remarks>
/// 版本戳是 <see cref="string"/>；经 <c>CacheSerializerRegistration.AddTypeInfo</c> 交给跨进程缓存的序列化器，应用的序列化上下文不必登记它（ADR-0229）
/// </remarks>
[JsonSerializable(typeof(string))]
internal sealed partial class TranslationCacheJsonContext : JsonSerializerContext;
