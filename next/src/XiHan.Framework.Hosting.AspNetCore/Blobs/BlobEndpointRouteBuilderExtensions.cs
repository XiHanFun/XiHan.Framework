// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.ObjectStorage.Abstractions.Signing;
using XiHan.Framework.ObjectStorage.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore.Blobs;

/// <summary>
/// 宿主代收发预签名地址的端点。
/// </summary>
/// <remarks>
/// <c>GET &lt;BasePath&gt;/{container}/{**key}</c> 下载（单个 <c>Range</c> 区间回 206 与 <c>Content-Range</c>，不可满足回 416），<c>PUT</c> 同一路径上传整个对象或分片上传的一片（回 200 与 <c>ETag</c>）。地址由 <see cref="HostedBlobUrlSigner"/> 签出，验签失败或过期回 403（<see cref="ObjectStorageErrors.SignatureInvalid"/>）。端点经内核管道（Observe 照常记录），不要求凭据：签名即授权。上传解除请求体大小上限，单对象上限由存储自己把关。地址带 <c>x-store</c> 时经 <see cref="BlobStores"/> 取命名实例，否则用缺省的 <see cref="IBlobStore"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class BlobEndpointRouteBuilderExtensions
{
    /// <summary>
    /// 映射端点。
    /// </summary>
    /// <remarks>
    /// 路由前缀取 <see cref="HostedBlobUrlOptions.BasePath"/>。要求已装配 <see cref="HostedBlobUrlFeature"/> 且 <c>UseXiHan()</c> 在路由之后。
    /// </remarks>
    public static IEndpointRouteBuilder MapXiHanBlobs(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var signer = endpoints.ServiceProvider.GetService<HostedBlobUrlSigner>()
            ?? throw new FrameworkException(KernelErrors.InvalidAppState, "MapXiHanBlobs() requires the hosted signed URL feature; add UseFeature<HostedBlobUrlFeature>() to the application.");
        var pattern = signer.Options.BasePath + "/{container}/{**key}";
        // RequestDelegate 重载不经参数绑定的动态代码生成，AOT 安全；路由值自己取
        endpoints.MapGet(pattern, http => DownloadAsync(http, signer, RouteValue(http, "container"), RouteValue(http, "key"))).WithName("xihan.objectstorage.blob.download");
        endpoints.MapPut(pattern, http => UploadAsync(http, signer, RouteValue(http, "container"), RouteValue(http, "key"))).WithName("xihan.objectstorage.blob.upload");
        return endpoints;
    }

    /// <summary>
    /// 路由值的文本。
    /// </summary>
    /// <remarks>
    /// 缺失为空串（随后按标识不合法处理）。
    /// </remarks>
    private static string RouteValue(HttpContext http, string name) => http.Request.RouteValues[name] as string ?? string.Empty;

    /// <summary>
    /// 下载：验签，按 <c>Range</c> 头整读或范围读，写出内容与头。
    /// </summary>
    private static async Task DownloadAsync(HttpContext http, HostedBlobUrlSigner signer, string container, string key)
    {
        var context = Context(http);
        if (!ContainerName.IsValid(container) || !BlobKey.IsValid(key))
        {
            context.SetOutcome(Error.NotFound(ObjectStorageErrors.BlobNotFound, ObjectStorageMessages.BlobNotFound, MessageArguments.Of(("container", container), ("key", key))));
            return;
        }

        var verified = signer.Verify(BlobUrlMethod.Get, container, key, http.Request.Query);
        if (!verified.TryGetValue(out var grant))
        {
            context.SetOutcome(verified.Error!);
            return;
        }

        var resolved = await StoreOf(http, grant, context.CancellationToken).ConfigureAwait(false);
        if (!resolved.TryGetValue(out var store))
        {
            context.SetOutcome(resolved.Error!);
            return;
        }

        var range = await RangeOf(http, store, container, key, context.CancellationToken).ConfigureAwait(false);
        if (!range.IsSuccess)
        {
            context.SetOutcome(range.Error!);
            return;
        }

        var got = range.ValueOrThrow().TryGetValue(out var requested)
            ? await store.GetAsync(container, key, requested, context.CancellationToken).ConfigureAwait(false)
            : await store.GetAsync(container, key, context.CancellationToken).ConfigureAwait(false);
        if (!got.TryGetValue(out var blob))
        {
            if (got.Error!.Code == ObjectStorageErrors.RangeNotSatisfiable)
            {
                // 416 自己写出并开始响应：错误仍记为调用结果（Observe 可见），但不再被改写成 Problem Details
                http.Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
                http.Response.Headers.ContentRange = $"bytes */{got.Error.Details?["length"]}";
                await http.Response.StartAsync(context.CancellationToken).ConfigureAwait(false);
            }

            context.SetOutcome(got.Error);
            return;
        }

        await using (blob.ConfigureAwait(false))
        {
            var response = http.Response;
            var metadata = blob.Metadata;
            response.ContentType = metadata.ContentType;
            response.Headers.ETag = new EntityTagHeaderValue("\"" + metadata.ETag + "\"").ToString();
            response.Headers.LastModified = metadata.LastModified.ToString("R", CultureInfo.InvariantCulture);
            response.Headers.AcceptRanges = "bytes";
            response.Headers.CacheControl = "private, no-store";
            if (grant.DownloadFileName is { } fileName)
            {
                response.Headers.ContentDisposition = BlobContentDisposition.Attachment(fileName);
            }

            if (blob.Range is { } served)
            {
                response.StatusCode = StatusCodes.Status206PartialContent;
                response.Headers.ContentRange = $"bytes {served.Offset}-{served.Last}/{served.TotalLength}";
                response.ContentLength = served.Length;
            }
            else
            {
                response.ContentLength = metadata.Length;
            }

            await blob.Content.CopyToAsync(response.Body, context.CancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 上传。
    /// </summary>
    /// <remarks>
    /// 验签（地址限定了内容类型时请求必须一致），整个对象经 <see cref="IBlobStore"/> 写入，分片经 <see cref="IBlobMultipartStore"/> 上传。
    /// </remarks>
    private static async Task UploadAsync(HttpContext http, HostedBlobUrlSigner signer, string container, string key)
    {
        var context = Context(http);
        if (!ContainerName.IsValid(container) || !BlobKey.IsValid(key))
        {
            context.SetOutcome(Error.NotFound(ObjectStorageErrors.BlobNotFound, ObjectStorageMessages.BlobNotFound, MessageArguments.Of(("container", container), ("key", key))));
            return;
        }

        var verified = signer.Verify(BlobUrlMethod.Put, container, key, http.Request.Query);
        if (!verified.TryGetValue(out var grant))
        {
            context.SetOutcome(verified.Error!);
            return;
        }

        var requestType = http.Request.ContentType;
        if (grant.ContentType is { } required && !string.Equals(required, requestType, StringComparison.OrdinalIgnoreCase))
        {
            context.SetOutcome(Error.Denied(ObjectStorageErrors.SignatureInvalid, ObjectStorageMessages.SignedUrlInvalid));
            return;
        }

        // 大文件直传：请求体上限交给存储（单对象上限、配额）而不是服务器的缺省值
        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = null;
        }

        var resolved = await StoreOf(http, grant, context.CancellationToken).ConfigureAwait(false);
        if (!resolved.TryGetValue(out var store))
        {
            context.SetOutcome(resolved.Error!);
            return;
        }

        string etag;
        if (grant.UploadId is { } uploadId)
        {
            var multipart = grant.Store is null ? http.RequestServices.GetService<IBlobMultipartStore>() : store as IBlobMultipartStore;
            if (multipart is null)
            {
                context.SetOutcome(Error.Unavailable(ObjectStorageErrors.StoreUnavailable, ObjectStorageMessages.StoreUnavailable, MessageArguments.Of(("reason", "the object storage does not support multipart uploads"))));
                return;
            }

            var part = await multipart.UploadPartAsync(new BlobMultipartUpload(container, key, uploadId), grant.PartNumber!.Value, http.Request.Body, context.CancellationToken).ConfigureAwait(false);
            if (!part.TryGetValue(out var uploaded))
            {
                context.SetOutcome(part.Error!);
                return;
            }

            etag = uploaded.ETag;
        }
        else
        {
            var options = new BlobPutOptions { ContentType = grant.ContentType ?? (string.IsNullOrWhiteSpace(requestType) ? BlobPutOptions.DefaultContentType : requestType) };
            var put = await store.PutAsync(container, key, http.Request.Body, options, context.CancellationToken).ConfigureAwait(false);
            if (!put.TryGetValue(out var written))
            {
                context.SetOutcome(put.Error!);
                return;
            }

            etag = written.ETag;
        }

        http.Response.StatusCode = StatusCodes.Status200OK;
        http.Response.Headers.ETag = new EntityTagHeaderValue("\"" + etag + "\"").ToString();
    }

    /// <summary>
    /// 地址指向的存储。
    /// </summary>
    /// <remarks>
    /// 带 <c>x-store</c> 的经 <see cref="BlobStores"/> 取命名实例（没装配注册表即不存在），否则取缺省实例。
    /// </remarks>
    private static async ValueTask<Result<IBlobStore>> StoreOf(HttpContext http, VerifiedBlobUrl grant, CancellationToken cancellationToken)
    {
        if (grant.Store is not { } name)
        {
            return Result.Success(http.RequestServices.GetRequiredService<IBlobStore>());
        }

        return http.RequestServices.GetService<BlobStores>() is { } stores
            ? await stores.GetAsync(name, cancellationToken).ConfigureAwait(false)
            : Error.NotFound(ObjectStorageErrors.StoreNotFound, ObjectStorageMessages.StoreNotFound, MessageArguments.Of(("store", name)));
    }

    /// <summary>
    /// 请求的范围。
    /// </summary>
    /// <remarks>
    /// 只认单个 <c>bytes</c> 区间（多区间与不合法的头按整读，RFC 9110 允许忽略）；后缀区间（<c>bytes=-n</c>）先 HEAD 取总长。
    /// </remarks>
    private static async ValueTask<Result<Maybe<BlobRange>>> RangeOf(HttpContext http, IBlobStore store, ContainerName container, BlobKey key, CancellationToken cancellationToken)
    {
        var header = http.Request.GetTypedHeaders().Range;
        if (header is null || !string.Equals(header.Unit.Value, "bytes", StringComparison.OrdinalIgnoreCase) || header.Ranges.Count != 1)
        {
            return Result.Success(Maybe.None<BlobRange>());
        }

        var item = header.Ranges.First();
        if (item.From is { } from)
        {
            return item.To is { } to
                ? Result.Success(to >= from ? Maybe.Some(new BlobRange(from, to - from + 1)) : Maybe.None<BlobRange>())
                : Result.Success(Maybe.Some(new BlobRange(from)));
        }

        if (item.To is not { } suffix || suffix <= 0)
        {
            return Result.Success(Maybe.None<BlobRange>());
        }

        var head = await store.HeadAsync(container, key, cancellationToken).ConfigureAwait(false);
        if (!head.TryGetValue(out var metadata))
        {
            return head.Error!;
        }

        return Result.Success(Maybe.Some(new BlobRange(Math.Max(0, metadata.Length - suffix))));
    }

    /// <summary>
    /// 本次请求的调用上下文。
    /// </summary>
    /// <remarks>
    /// 没有经过 <c>UseXiHan()</c> 是装配错误。
    /// </remarks>
    private static InvocationContext Context(HttpContext http)
        => http.GetXiHanContext().TryGetValue(out var context)
            ? context
            : throw new FrameworkException(KernelErrors.InvalidAppState, "The blob endpoints require UseXiHan() to run before routing; the invocation context is missing.");
}
