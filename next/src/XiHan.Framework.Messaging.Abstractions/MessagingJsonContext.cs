// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Messaging.Abstractions;

[JsonSerializable(typeof(CloudEventDocument))]
[JsonSerializable(typeof(string))]
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class MessagingJsonContext : JsonSerializerContext;
