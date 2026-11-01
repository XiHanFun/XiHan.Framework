// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Stores;

/// <summary>
/// 缺省存储的选择规则（RFC-0012「缺省实例与命名实例」）。
/// </summary>
/// <remarks>
/// 缺省的 <see cref="Blobs.IBlobStore"/>、<see cref="Multipart.IBlobMultipartStore"/> 与 <see cref="Signing.IBlobUrlSigner"/> 必须落在同一存储上，只约束被要求的契约（ADR-0082）：存储包被要求提供 <c>objectstorage.multipart</c> 或 <c>objectstorage.presign</c>（有清单依赖该能力，或 <c>XiHan:Providers</c> / <c>UseProvider</c> 指定了它，见 <see cref="ResolvedNode.IsRequestedFor"/>）时必须同时被选为 <c>objectstorage.blob</c>；只是兜底选出的不约束，存储包按被顶替处理。宿主代收发的签名器（<c>xihan.hosting.blob-urls</c>）经缺省 <see cref="Blobs.IBlobStore"/> 收发，不是存储包，不受此约束。存储包在 <c>Configure</c> 开头调用 <see cref="ProvidesDefaultInstance"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class BlobStoreSelection
{
    /// <summary>
    /// 核对存储包节点被选中的能力，判定它是否提供缺省实例。
    /// </summary>
    /// <remarks>
    /// 被选为 <c>objectstorage.blob</c> 返回 <see langword="true"/>：它是缺省存储，为被选中的各项能力注册契约（兜底选出的也注册）。
    /// 没被选为 <c>objectstorage.blob</c>、分片上传与预签名也都只是兜底选出（或没被选中）返回 <see langword="false"/>：存储包不提供缺省实例，按被顶替处理，只登记命名实例 Provider。
    /// 没被选为 <c>objectstorage.blob</c> 却被要求提供分片上传或预签名时抛出携带 <see cref="ObjectStorageErrors.StoreSelectionInvalid"/> 的 <see cref="FrameworkException"/>，应用启动失败，消息列出被要求的能力与要改的 <c>XiHan:Providers</c> 键。
    /// </remarks>
    /// <param name="node">存储包在已解析图中的节点。</param>
    /// <returns>存储包是否提供缺省实例。</returns>
    public static bool ProvidesDefaultInstance(ResolvedNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.IsSelectedFor(ObjectStorageCapabilities.Blob))
        {
            return true;
        }

        // 另一个存储被选为缺省 IBlobStore 时，本存储签出的地址或建的分片上传指向的是另一个存储里的对象：
        // 有人依赖或指定了这项能力就是真的要用，不悄悄改选，而是让应用把几项能力指定到同一个存储（或把预签名交给宿主代收发）；
        // 没人依赖也没人指定、只是兜底选出的，没有人会经它取用，不注册即可，不必为此启动失败
        string[] split = [.. new[] { ObjectStorageCapabilities.Multipart, ObjectStorageCapabilities.Presign }.Where(node.IsRequestedFor)];
        if (split.Length > 0)
        {
            throw new FrameworkException(
                ObjectStorageErrors.StoreSelectionInvalid,
                $"Object storage provider '{node.Manifest.Id}' is requested for '{string.Join("', '", split)}' (a manifest requires it or 'XiHan:Providers' selects it) but is not selected for '{ObjectStorageCapabilities.Blob}'; the default IBlobStore, IBlobMultipartStore and IBlobUrlSigner must use the same store. Select the default store's provider for these capabilities with 'XiHan:Providers:<capability>' (signed URLs for a store without native signing come from 'xihan.hosting.blob-urls').");
        }

        return false;
    }
}
