// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions;

/// <summary>
/// 对象存储子系统的能力 ID 与接口版本。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class ObjectStorageCapabilities
{
    /// <summary>对象存储：容器 / 键 / 字节 / 元数据，<see cref="IBlobStore"/>。</summary>
    public const string Blob = "objectstorage.blob";

    /// <summary>当前接口版本。</summary>
    public const int Version = 1;
}
