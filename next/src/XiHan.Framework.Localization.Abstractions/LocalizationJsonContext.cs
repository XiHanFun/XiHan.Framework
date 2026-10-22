// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Localization.Abstractions;

/// <summary>
/// 语言文件（<c>Localization/&lt;区域&gt;.json</c>）的源生成 JSON 上下文：平面的消息键 → 模板。
/// </summary>
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class LocalizationJsonContext : JsonSerializerContext;
