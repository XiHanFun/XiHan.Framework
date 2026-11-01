// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Stores;

/// <summary>
/// 缺省存储的选择规则（RFC-0012「缺省实例与命名实例」）。
/// </summary>
/// <remarks>
/// 缺省的 <see cref="Blobs.IBlobStore"/>、<see cref="Multipart.IBlobMultipartStore"/> 与 <see cref="Signing.IBlobUrlSigner"/> 必须落在同一存储上：存储包被选为 <c>objectstorage.multipart</c> 或 <c>objectstorage.presign</c> 时必须同时被选为 <c>objectstorage.blob</c>。宿主代收发的签名器（<c>xihan.hosting.blob-urls</c>）经缺省 <see cref="Blobs.IBlobStore"/> 收发，不是存储包，不受此约束。存储包在 <c>Configure</c> 开头调用 <see cref="EnsureSameStore"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class BlobStoreSelection
{
    /// <summary>
    /// 核对存储包节点被选中的能力。
    /// </summary>
    /// <remarks>
    /// 被选为分片上传或预签名却没被选为对象存储时抛出携带 <see cref="ObjectStorageErrors.StoreSelectionInvalid"/> 的 <see cref="FrameworkException"/>，应用启动失败，消息列出要改的 <c>XiHan:Providers</c> 键。
    /// </remarks>
    public static void EnsureSameStore(ResolvedNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.IsSelectedFor(ObjectStorageCapabilities.Blob))
        {
            return;
        }

        // 另一个存储被选为缺省 IBlobStore 时，本存储签出的地址或建的分片上传指向的是另一个存储里的对象：
        // 这里不悄悄改选，而是让应用把几项能力指定到同一个存储（或把预签名交给宿主代收发）
        string[] split = [.. new[] { ObjectStorageCapabilities.Multipart, ObjectStorageCapabilities.Presign }.Where(node.IsSelectedFor)];
        if (split.Length > 0)
        {
            throw new FrameworkException(
                ObjectStorageErrors.StoreSelectionInvalid,
                $"Object storage provider '{node.Manifest.Id}' is selected for '{string.Join("', '", split)}' but not for '{ObjectStorageCapabilities.Blob}'; the default IBlobStore, IBlobMultipartStore and IBlobUrlSigner must use the same store. Select one provider for all of them with 'XiHan:Providers:<capability>' (signed URLs for a store without native signing come from 'xihan.hosting.blob-urls').");
        }
    }
}
