// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;

namespace XiHan.Framework.ObjectStorage.FileSystem;

/// <summary>
/// 元数据文件的源生成序列化上下文。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BlobMetadataDocument))]
[JsonSerializable(typeof(MultipartManifestDocument))]
internal sealed partial class FileSystemJsonContext : JsonSerializerContext;
