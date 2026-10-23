// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.Caching.Remote;

/// <summary>
/// 操作集消息的源生成序列化上下文：属性名 camelCase，<see langword="null"/> 不写出。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RemoteCacheOperations.KeyRequest))]
[JsonSerializable(typeof(RemoteCacheOperations.GetResponse))]
[JsonSerializable(typeof(RemoteCacheOperations.SetRequest))]
[JsonSerializable(typeof(RemoteCacheOperations.EmptyResponse))]
[JsonSerializable(typeof(RemoteCacheOperations.AddedResponse))]
[JsonSerializable(typeof(RemoteCacheOperations.RemovedResponse))]
[JsonSerializable(typeof(RemoteCacheOperations.TryRemoveRequest))]
[JsonSerializable(typeof(RemoteCacheOperations.KeysRequest))]
[JsonSerializable(typeof(RemoteCacheOperations.GetManyResponse))]
[JsonSerializable(typeof(RemoteCacheOperations.SetManyRequest))]
[JsonSerializable(typeof(RemoteCacheOperations.RemovedManyResponse))]
internal sealed partial class RemoteCacheJsonContext : JsonSerializerContext;
