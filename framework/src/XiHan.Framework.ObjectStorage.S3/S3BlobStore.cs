// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Xml;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Listing;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.ObjectStorage.Abstractions.Signing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// S3 对象存储
/// </summary>
/// <remarks>
/// 容器即桶，键即对象键。不覆盖写入用条件请求（<c>If-None-Match: *</c>）保证原子，已存在返回 <see cref="ObjectStorageErrors.BlobExists"/>；服务是否认条件写入要先探明（ADR-0089、ADR-0090）：特性启动或命名实例建出时探一次，没探明就在后台重试，这期间不覆盖的写入与分片上传返回 <see cref="ObjectStorageErrors.ConditionalWriteUnconfirmed"/>，探明不认或选项关闭条件写入时一律返回 <see cref="ObjectStorageErrors.ConditionalWriteUnavailable"/>；批量删除先逐键 HEAD 得知是否存在，再一次 <c>DeleteObjects</c>，SDK 对有错误项的应答抛出 <see cref="DeleteObjectsException"/> 时从它带的应答逐项报告（ADR-0333）；列举经 <c>ListObjectsV2</c>，条目只有键、长度、ETag 与修改时间（内容类型为默认值、自定义元数据为空，需要时再 HEAD）。范围读取经 <c>Range</c> 请求头；分片上传直接映射 S3 的 multipart（不覆盖的完成带 <c>If-None-Match: *</c>）；预签名地址由 SDK 本地签出（签名 V4），客户端直连存储。不存在的键与桶映射为 <see cref="ObjectStorageErrors.BlobNotFound"/>，其余服务端与网络错误映射为 <see cref="ObjectStorageErrors.StoreUnavailable"/>，服务应答成功却缺了必需的内容同此（发起分片上传的应答没有可用的上传标识，ADR-0302；上传分片的应答没有可用的 ETag、列出的分片没有可用的编号或 ETag 或长度为负，ADR-0308；读取、HEAD 与写入的应答没有可用的 ETag，读取与 HEAD 的应答没有修改时间，列举截断却没有能前进的续页标记、列出的对象缺 ETag 或修改时间，列出分片的分页不前进或超过 10000 片，批量删除的错误项认不出是哪一项，ADR-0313；整读的 ETag 不是十六进制、SDK 当作 MD5 解码不了，ADR-0317；读取与 HEAD 的应答没有可用的 Content-Length，列出的对象没有可用的长度，任一步的应答 SDK 解析不了，列出的分片里同一编号出现两次，ADR-0322；响应体不是良构的 XML，放弃分片上传与建桶的应答解析不了，列出的分片编号不比这一页的起点大，自定义元数据里有解不了的 RFC 2047 encoded-word，ADR-0333）。整读的内容对不上应答声明的 ETag 或校验和时 SDK 读到末尾才发现，读内容的流抛出携带 <see cref="ObjectStorageErrors.StoreUnavailable"/> 的 <see cref="FrameworkException"/>（ADR-0322）；没读到末尾就释放时不抛出，只记 Warning（ADR-0333）。自定义元数据的值头写不出原样时照 RFC 2047 编码写入、读回时解码（<see cref="S3MetadataHeader"/>，ADR-0333）；整个是 encoded-word 形状的只有 ASCII 的值存不成原样，写入与分片上传的发起返回 <see cref="ObjectStorageErrors.MetadataValueUnsupported"/>、不发请求，服务以 <c>MetadataTooLarge</c> 拒收元数据时返回 <see cref="ObjectStorageErrors.MetadataTooLarge"/>，别的写入方原样写进头的 UTF-8 字节读回时严格按 UTF-8 解出、解不了按不合规的应答返回（ADR-0343）。写入结果的修改时间是本机时钟：PutObject 的应答本来就不带修改时间，要服务端记下的时刻就在写入之后再 <see cref="HeadAsync"/>（ADR-0313、ADR-0322）。内容类型原样存取：SDK 写不出原值的（<see cref="S3ContentTypeHeader"/>）写入与分片上传的发起返回 <see cref="ObjectStorageErrors.ContentTypeUnsupported"/>，不发请求；读取时 SDK 按 .NET 的写法交出它解析得了的值（ADR-0295）。没有公开构造（ADR-0266，公开面不出现 SDK 的类型）：缺省实例由 <see cref="S3ObjectStorageFeature"/> 建出，客户端归容器所有，释放实例什么都不做；命名实例由 <see cref="S3BlobStoreProvider"/> 按定义建出（经 <c>BlobStores</c>，或直接调它的 <c>CreateAsync</c>），自己建客户端并在后台重试探测，释放实例即停止重试、释放客户端
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial class S3BlobStore : IBlobStore, IBlobMultipartStore, IBlobUrlSigner, IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 自定义元数据在 S3 头里的前缀
    /// </summary>
    private const string MetadataPrefix = "x-amz-meta-";

    /// <summary>
    /// 一次 DeleteObjects 允许的最大键数
    /// </summary>
    private const int DeleteBatchSize = 1000;

    /// <summary>
    /// 句柄收得下的最长上传标识
    /// </summary>
    /// <remarks>
    /// 同 <see cref="BlobMultipartUpload"/> 构造的公开前置条件（1 到 1024 个可见字符）
    /// </remarks>
    private const int MaxUploadIdLength = 1024;

    /// <summary>
    /// 发起的应答没有可用的上传标识时 <c>reason</c> 的文本
    /// </summary>
    /// <remarks>
    /// 固定文本，不带应答里的值（ADR-0302）
    /// </remarks>
    private const string UploadIdMissingReason = "InitiateMultipartUpload returned no usable UploadId";

    /// <summary>
    /// 上传分片的应答没有可用的 ETag 时 <c>reason</c> 的文本
    /// </summary>
    /// <remarks>
    /// 固定文本，不带应答里的值（ADR-0308）
    /// </remarks>
    private const string PartETagMissingReason = "UploadPart returned no usable ETag";

    /// <summary>
    /// 列出的分片没有可用的编号时 <c>reason</c> 的文本
    /// </summary>
    /// <remarks>
    /// 编号缺失或不在 1 到 <see cref="BlobMultipart.MaxPartNumber"/> 之内（ADR-0308）
    /// </remarks>
    private const string ListedPartNumberMissingReason = "ListParts returned a part without a usable PartNumber";

    /// <summary>
    /// 列出的分片没有可用的 ETag 时 <c>reason</c> 的文本
    /// </summary>
    /// <remarks>
    /// 固定文本，不带应答里的值（ADR-0308）
    /// </remarks>
    private const string ListedPartETagMissingReason = "ListParts returned a part without a usable ETag";

    /// <summary>
    /// 列出的分片长度为负时 <c>reason</c> 的文本
    /// </summary>
    /// <remarks>
    /// 固定文本，不带应答里的值（ADR-0308）
    /// </remarks>
    private const string ListedPartSizeNegativeReason = "ListParts returned a part with a negative Size";

    /// <summary>
    /// 列出分片的应答截断了却没有能前进的续页标记时 <c>reason</c> 的文本
    /// </summary>
    /// <remarks>
    /// 标记缺失、不比这一页的起点大，或不小于 <see cref="BlobMultipart.MaxPartNumber"/>（ADR-0313）
    /// </remarks>
    private const string NextPartNumberMarkerMissingReason = "ListParts was truncated without a usable NextPartNumberMarker";

    /// <summary>
    /// 列出的分片里同一编号出现两次时 <c>reason</c> 的文本
    /// </summary>
    /// <remarks>
    /// 同一页里或跨页都算；编号在 1 到 <see cref="BlobMultipart.MaxPartNumber"/> 之内且各不相同，一次列出因此至多这么多片（ADR-0322）
    /// </remarks>
    private const string ListedPartNumberRepeatedReason = "ListParts returned the same PartNumber twice";

    /// <summary>
    /// 列出的分片编号不比这一页的起点大时 <c>reason</c> 的文本
    /// </summary>
    /// <remarks>
    /// S3 的 <c>ListParts</c> 只交出编号大于 <c>PartNumberMarker</c> 的分片；重复的编号先按重复报告（ADR-0333）
    /// </remarks>
    private const string ListedPartNumberBehindMarkerReason = "ListParts returned a PartNumber not greater than the PartNumberMarker of the page";

    /// <summary>
    /// 列举的应答截断了却没有能前进的续页标记时 <c>reason</c> 的文本
    /// </summary>
    /// <remarks>
    /// 标记缺失、为空，或与本次请求带的相同（ADR-0313）
    /// </remarks>
    private const string NextContinuationTokenMissingReason = "ListObjectsV2 was truncated without a usable NextContinuationToken";

    /// <summary>
    /// 列出的对象没有可用的 ETag 时 <c>reason</c> 的文本
    /// </summary>
    /// <remarks>
    /// 固定文本，不带应答里的值（ADR-0313）
    /// </remarks>
    private const string ListedObjectETagMissingReason = "ListObjectsV2 returned an object without a usable ETag";

    /// <summary>
    /// 列出的对象没有修改时间时 <c>reason</c> 的文本
    /// </summary>
    /// <remarks>
    /// 固定文本，不带应答里的值（ADR-0313）
    /// </remarks>
    private const string ListedObjectLastModifiedMissingReason = "ListObjectsV2 returned an object without a LastModified";

    /// <summary>
    /// 列出的对象没有可用的长度时 <c>reason</c> 的文本
    /// </summary>
    /// <remarks>
    /// 缺 <c>Size</c> 或为负，固定文本，不带应答里的值（ADR-0322）
    /// </remarks>
    private const string ListedObjectSizeMissingReason = "ListObjectsV2 returned an object without a usable Size";

    /// <summary>
    /// 写入的应答没有可用的 ETag 时 <c>reason</c> 的文本
    /// </summary>
    /// <remarks>
    /// 固定文本，不带应答里的值（ADR-0313）
    /// </remarks>
    private const string PutETagMissingReason = "PutObject returned no usable ETag";

    /// <summary>
    /// 批量删除的应答有认不出是哪一项的错误项时 <c>reason</c> 的文本
    /// </summary>
    /// <remarks>
    /// 错误项没有键，或带的键不在这一批里（ADR-0313）
    /// </remarks>
    private const string DeleteErrorKeyMissingReason = "DeleteObjects returned an error without a usable Key";

    /// <summary>
    /// 批量删除的错误项认得出键却没有错误码时 <c>reason</c> 的文本
    /// </summary>
    /// <remarks>
    /// <c>&lt;Code&gt;</c> 缺失、为空或只有空白：照样只报那一项失败，<c>reason</c> 不为空（ADR-0344）
    /// </remarks>
    private const string DeleteErrorCodeMissingReason = "DeleteObjects returned an error without a usable Code";

    /// <summary>
    /// 整读的 ETag 不是十六进制时 <c>reason</c> 的文本
    /// </summary>
    /// <remarks>
    /// 固定文本，不带应答里的值（ADR-0317）
    /// </remarks>
    private const string ETagNotHexadecimalReason = "GetObject returned an ETag that is not hexadecimal";

    /// <summary>
    /// 整读的应答 SDK 解析不了时 <c>reason</c> 的文本
    /// </summary>
    /// <remarks>
    /// 整读时 SDK 的 <see cref="FormatException"/> 既可能来自解组时读头（修改时间、长度写错），也可能来自把含十六进制数字之外字符的 ETag 当 MD5 解码：不靠调用栈分不出是哪一处，文本同时点名两者（ADR-0322）
    /// </remarks>
    private const string WholeReadUnparsableReason = "GetObject returned an ETag that is not hexadecimal or a header the SDK could not parse";

    /// <summary>
    /// 整读的内容对不上应答声明的 ETag 或校验和时 <c>reason</c> 的文本
    /// </summary>
    /// <remarks>
    /// 只进日志：读内容的流以携带 <see cref="ObjectStorageErrors.StoreUnavailable"/> 的异常报告它（ADR-0322）
    /// </remarks>
    private const string ContentMismatchReason = "GetObject returned content that does not match its ETag or checksum";

    /// <summary>
    /// 整读的内容对不上应答声明的 ETag 或校验和时异常的消息
    /// </summary>
    /// <remarks>
    /// 固定文本，不带桶名、键与应答里的值：这个异常会交到调用方手里（ADR-0322）
    /// </remarks>
    private const string ContentMismatchMessage = "The content the S3 service returned does not match the ETag or checksum it answered with; discard the bytes already read.";

    /// <summary>
    /// 读取的应答
    /// </summary>
    private static readonly ObjectAnswer _getObjectAnswer = new("GetObject", "GetObject returned no usable ETag", "GetObject returned no Last-Modified", "GetObject returned no usable Content-Length", "GetObject returned user metadata with an RFC 2047 encoded-word that does not decode to a valid value", "GetObject returned user metadata with raw non-ASCII bytes that do not decode as UTF-8 to a valid value");

    /// <summary>
    /// HEAD 的应答
    /// </summary>
    private static readonly ObjectAnswer _headObjectAnswer = new("HeadObject", "HeadObject returned no usable ETag", "HeadObject returned no Last-Modified", "HeadObject returned no usable Content-Length", "HeadObject returned user metadata with an RFC 2047 encoded-word that does not decode to a valid value", "HeadObject returned user metadata with raw non-ASCII bytes that do not decode as UTF-8 to a valid value");

    /// <summary>
    /// S3 客户端
    /// </summary>
    private readonly IAmazonS3 _client;

    /// <summary>
    /// 选项
    /// </summary>
    private readonly S3ObjectStorageOptions _options;

    /// <summary>
    /// 时钟
    /// </summary>
    /// <remarks>
    /// 写入结果的修改时间来自它（PutObject 的响应不带修改时间）
    /// </remarks>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 条件写入的状态：不覆盖写入之前经它放行或拒绝
    /// </summary>
    private readonly S3ConditionalWriteGate _conditionalWrites;

    /// <summary>
    /// 是否拥有客户端与条件写入的状态（释放实例时一并停止重试、释放客户端）
    /// </summary>
    private readonly bool _owned;

    /// <summary>
    /// 创建不探测条件写入的实例
    /// </summary>
    /// <remarks>
    /// 只在包内与测试里使用（ADR-0266）：构造收 SDK 的客户端，公开出去就把 SDK 的类型带进了本包的公开面。客户端归调用方所有，释放实例什么都不做。不探测条件写入：<see cref="S3ObjectStorageOptions.ConditionalWrites"/> 开启时视作服务认它（由调用方负责），关闭时不覆盖的写入一律返回 <see cref="ObjectStorageErrors.ConditionalWriteUnavailable"/>
    /// </remarks>
    internal S3BlobStore(IAmazonS3 client, S3ObjectStorageOptions options, TimeProvider clock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger<S3BlobStore>();
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _conditionalWrites = S3ConditionalWriteGate.Unprobed(_client, _options, _clock);
        _owned = false;
    }

    /// <summary>
    /// 创建
    /// </summary>
    /// <remarks>
    /// <paramref name="owned"/> 为真时客户端与条件写入的状态归本实例所有，释放实例即停止重试、释放客户端（命名实例）；为假时归容器（缺省实例）
    /// </remarks>
    internal S3BlobStore(IAmazonS3 client, S3ObjectStorageOptions options, TimeProvider clock, ILoggerFactory loggerFactory, S3ConditionalWriteGate conditionalWrites, bool owned)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger<S3BlobStore>();
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _conditionalWrites = conditionalWrites ?? throw new ArgumentNullException(nameof(conditionalWrites));
        _owned = owned;
    }

    /// <summary>
    /// 条件写入的状态（测试核对用）
    /// </summary>
    internal S3ConditionalWriteGate ConditionalWrites => _conditionalWrites;

    /// <summary>
    /// 释放：只在拥有时发出停止重试的信号并释放客户端，不等待正在进行的探测
    /// </summary>
    public void Dispose()
    {
        if (_owned)
        {
            _conditionalWrites.Cancel();
            _client.Dispose();
        }
    }

    /// <summary>
    /// 释放：只在拥有时停止重试、等正在进行的探测结束，再释放客户端
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_owned)
        {
            // 先等后台探测放下客户端，再释放它：避免探测在已释放的客户端上报出意外错误
            await _conditionalWrites.DisposeAsync().ConfigureAwait(false);
            _client.Dispose();
        }
    }

    /// <summary>
    /// 健康探测：连通性加条件写入的状态
    /// </summary>
    /// <remarks>
    /// 按本实例的 <see cref="S3ObjectStorageOptions.HealthBucket"/> HEAD 桶或列桶（同缺省健康项），连通之后再看条件写入是否已确认：尚未确认时返回最近一次探测失败的错误（连不上，或启动之后才被拒绝、缺桶），探明不认时返回不认的错误（注册表都记为 Degraded）
    /// </remarks>
    internal async ValueTask<Result> ProbeAsync(CancellationToken cancellationToken)
    {
        var reachable = await S3ObjectStorageFeature.ProbeAsync(_client, _options.HealthBucket, cancellationToken).ConfigureAwait(false);
        return reachable.IsFailure ? reachable : _conditionalWrites.Health();
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result<BlobMetadata>>> PutAsync(ContainerName container, IReadOnlyList<BlobUpload> uploads, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uploads);
        var results = new List<Result<BlobMetadata>>(uploads.Count);
        foreach (var upload in uploads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await PutOneAsync(container, upload, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlobContent>> GetAsync(ContainerName container, BlobKey key, CancellationToken cancellationToken)
    {
        GetObjectResponse? response = null;
        try
        {
            response = await _client.GetObjectAsync(new GetObjectRequest { BucketName = container.Value, Key = key.Value }, cancellationToken).ConfigureAwait(false);
            // 长度取应答头的原值（SDK 解组时读出，缺失时为 -1），不取 HTTP 客户端给的值：那个值缺失时是 0（ADR-0322）
            var described = DescribeObject(container, key, _getObjectAnswer, response.Headers.ContentLength, response.Headers.ContentType, response.ETag, response.LastModified, response.Metadata);
            if (!described.TryGetValue(out var metadata))
            {
                return described.Error!;
            }

            var content = new BlobContent(metadata, new S3ObjectStream(response, exception => ContentRejected(container, exception), _ => ContentDiscarded(container)));
            response = null;
            return Result.Success(content);
        }
        catch (AmazonS3Exception exception) when (IsNotFound(exception))
        {
            return NotFound(container, key);
        }
        catch (Exception exception) when (IsUndecodableETag(exception))
        {
            // SDK 整读时把 ETag 当作内容的 MD5，解不了十六进制就在交出应答之前抛出（应答已由 SDK 释放）：
            // 是服务一侧的不合规应答，同缺 ETag 按服务端错误返回，不把编程错误类的异常抛给调用方，不重发（ADR-0317）
            return UnusableAnswer(container, "GetObject", ETagNotHexadecimalReason);
        }
        catch (Exception exception) when (IsUnparsableAnswer(exception))
        {
            // 整读的 FormatException 可能来自解组时读头，也可能来自 ETag 含十六进制数字之外的字符：两者同是不合规的应答，原因同时点名（ADR-0322）
            return UnusableAnswer(container, "GetObject", exception is FormatException ? WholeReadUnparsableReason : UnparsableReason("GetObject", exception));
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            return Unavailable(exception);
        }
        finally
        {
            // 没能交出去的响应要释放（它持有 HTTP 连接）
            DisposeUndelivered(response);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlobMetadata>> HeadAsync(ContainerName container, BlobKey key, CancellationToken cancellationToken)
    {
        var head = await HeadObjectAsync(container, key, cancellationToken).ConfigureAwait(false);
        return head.TryGetValue(out var response)
            ? DescribeObject(container, key, _headObjectAnswer, response.Headers.ContentLength, response.Headers.ContentType, response.ETag, response.LastModified, response.Metadata)
            : head.Error!;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result<bool>>> DeleteAsync(ContainerName container, IReadOnlyList<BlobKey> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count == 0)
        {
            return [];
        }

        // S3 的删除不报告对象是否存在过：先逐键 HEAD 定存在性（同一批里重复的键只有第一次算存在），再把存在的键一次批量删除
        var outcomes = new Result<bool>[keys.Count];
        var claimed = new HashSet<string>(StringComparer.Ordinal);
        var toDelete = new List<string>();
        for (var index = 0; index < keys.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var exists = await HeadExistsAsync(container, keys[index], cancellationToken).ConfigureAwait(false);
            if (!exists.TryGetValue(out var found))
            {
                outcomes[index] = Result.Failure<bool>(exists.Error!);
            }
            else if (found && claimed.Add(keys[index].Value))
            {
                toDelete.Add(keys[index].Value);
                outcomes[index] = Result.Success(true);
            }
            else
            {
                outcomes[index] = Result.Success(false);
            }
        }

        var failed = new Dictionary<string, Error>(StringComparer.Ordinal);
        foreach (var batch in toDelete.Chunk(DeleteBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var response = await _client.DeleteObjectsAsync(new DeleteObjectsRequest
                {
                    BucketName = container.Value,
                    Objects = [.. batch.Select(static key => new KeyVersion { Key = key })],
                    Quiet = true,
                }, cancellationToken).ConfigureAwait(false);
                // AWS SDK for .NET 4.0 有错误项时抛出、不走到这里；以后的 SDK 若在应答里交出错误项，同样逐项报告，不当作全部删成
                RecordDeleteErrors(container, batch, response.DeleteErrors, failed);
            }
            catch (DeleteObjectsException exception) when (exception.Response is { } answered)
            {
                // SDK 遇到有错误项的应答（静默模式也是）抛出这个异常、带着解出的应答：照错误项逐项报告，
                // 不当作存储故障整批报错（ADR-0333；以前逐项报告只有测试替身走得到）
                RecordDeleteErrors(container, batch, answered.DeleteErrors, failed);
            }
            catch (Exception exception) when (IsUnparsableAnswer(exception))
            {
                // 应答解析不了，这一批里哪些删成了就说不清：同这一批的请求整个失败（ADR-0322）
                var unparsable = UnusableAnswer(container, "DeleteObjects", UnparsableReason("DeleteObjects", exception));
                foreach (var key in batch)
                {
                    failed[key] = unparsable;
                }
            }
            catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
            {
                var error = Unavailable(exception);
                foreach (var key in batch)
                {
                    failed[key] = error;
                }
            }
        }

        var results = new List<Result<bool>>(keys.Count);
        for (var index = 0; index < keys.Count; index++)
        {
            // 只有"存在且本次负责删除"的项会因删除失败而失败
            var outcome = outcomes[index];
            if (outcome.IsSuccess && outcome.ValueOrThrow() && failed.TryGetValue(keys[index].Value, out var error))
            {
                outcome = Result.Failure<bool>(error);
            }

            results.Add(outcome);
        }

        return results;
    }

    /// <summary>
    /// 记下一批删除的应答里的错误项
    /// </summary>
    /// <remarks>
    /// 静默模式的应答只列错误项：都认得出是这一批里的哪个键时只记那几项，各报 S3 的错误码，错误项没有错误码时报固定文字、这一批记一条 Warning（ADR-0344）；有一项认不出（没有键，或键不在这一批里），这一批里哪些删成了就说不清，同这一批的请求整个失败，各项报同一个错误，不以空键写进结果（ADR-0313）
    /// </remarks>
    private void RecordDeleteErrors(ContainerName container, string[] batch, List<DeleteError>? errors, Dictionary<string, Error> failed)
    {
        if (errors is not { Count: > 0 })
        {
            return;
        }

        var members = new HashSet<string>(batch, StringComparer.Ordinal);
        if (errors.Any(error => string.IsNullOrEmpty(error.Key) || !members.Contains(error.Key)))
        {
            var unusable = UnusableAnswer(container, "DeleteObjects", DeleteErrorKeyMissingReason);
            foreach (var key in batch)
            {
                failed[key] = unusable;
            }

            return;
        }

        // 认得出键却没有错误码的错误项照样只报那一项：reason 写固定文字而不是空，这一批只记一条 Warning（ADR-0344）
        if (errors.Any(static error => string.IsNullOrWhiteSpace(error.Code)))
        {
            LogAnswerUnusable(_logger, "DeleteObjects", container.Value, DeleteErrorCodeMissingReason);
        }

        foreach (var error in errors)
        {
            // 服务端给的文字不进错误，错误码足够区分
            failed[error.Key] = Unavailable(string.IsNullOrWhiteSpace(error.Code) ? DeleteErrorCodeMissingReason : error.Code);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlobPage>> ListAsync(ContainerName container, BlobQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        // 页大小越界是调用方可以改正的输入（RFC-0012）：以校验错误返回，不发请求（S3 对越界的 MaxKeys 会截断或报错，各家兼容服务不一）
        if (query.Validate(BlobQuery.Revision) is { IsFailure: true } invalid)
        {
            return invalid.Error!;
        }

        try
        {
            var response = await _client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = container.Value,
                Prefix = query.Prefix,
                MaxKeys = query.PageSize,
                ContinuationToken = query.ContinuationToken,
            }, cancellationToken).ConfigureAwait(false);

            var items = new List<BlobMetadata>(response.KeyCount ?? 0);
            foreach (var entry in response.S3Objects ?? [])
            {
                // 不符合键文法的对象不是本契约的对象，跳过：以 / 结尾的"目录"占位，以及保留前缀下框架自己的对象（如条件写入探测键，ADR-0090）；
                // 跳过的对象缺什么都不影响这一页
                if (!BlobKey.IsValid(entry.Key))
                {
                    continue;
                }

                // 要交出的对象缺 ETag 或修改时间是不合规的应答：整页失败，不跳过它（跳过之后这一页少一个对象，调用方看不出来，ADR-0313）
                var etag = Unquote(entry.ETag);
                if (!IsUsableETag(etag))
                {
                    return UnusableAnswer(container, "ListObjectsV2", ListedObjectETagMissingReason);
                }

                if (entry.LastModified is not { } modified)
                {
                    return UnusableAnswer(container, "ListObjectsV2", ListedObjectLastModifiedMissingReason);
                }

                // 缺长度不以 0 交出：调用方会拿到一个看上去为空的对象，与缺 ETag、修改时间同样整页失败（ADR-0322）
                if (entry.Size is not { } size || size < 0)
                {
                    return UnusableAnswer(container, "ListObjectsV2", ListedObjectSizeMissingReason);
                }

                items.Add(new BlobMetadata
                {
                    Key = new BlobKey(entry.Key),
                    Length = size,
                    ContentType = BlobPutOptions.DefaultContentType,
                    ETag = etag,
                    LastModified = ToTimestamp(modified),
                    Metadata = EmptyMetadata,
                });
            }

            string? next = null;
            if (response.IsTruncated == true)
            {
                // 截断了却没有能前进的续页标记：交出空的标记调用方以为列完了，交出本次带来的那个标记调用方再列只会回到原处（ADR-0313）
                next = response.NextContinuationToken;
                if (string.IsNullOrEmpty(next) || string.Equals(next, query.ContinuationToken, StringComparison.Ordinal))
                {
                    return UnusableAnswer(container, "ListObjectsV2", NextContinuationTokenMissingReason);
                }
            }

            return Result.Success(new BlobPage { Items = items, ContinuationToken = next });
        }
        catch (AmazonS3Exception exception) when (IsNotFound(exception))
        {
            // 桶不存在视作空容器，与文件系统实现里容器目录不存在的语义一致
            return Result.Success(new BlobPage { Items = [] });
        }
        catch (Exception exception) when (IsUnparsableAnswer(exception))
        {
            return UnusableAnswer(container, "ListObjectsV2", UnparsableReason("ListObjectsV2", exception));
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            return Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlobContent>> GetAsync(ContainerName container, BlobKey key, BlobRange range, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(range);
        GetObjectResponse? response = null;
        try
        {
            var byteRange = range.Length is { } length ? new ByteRange(range.Offset, range.Offset + length - 1) : new ByteRange($"bytes={range.Offset.ToString(CultureInfo.InvariantCulture)}-");
            response = await _client.GetObjectAsync(new GetObjectRequest { BucketName = container.Value, Key = key.Value, ByteRange = byteRange }, cancellationToken).ConfigureAwait(false);

            // 206 的 Content-Range 是 bytes 起-止/总长；服务端忽略了 Range（200 整对象）时不猜，按存储故障报告
            if (!TryParseContentRange(response.ContentRange, out var resolved))
            {
                return Unavailable("the server ignored the range request");
            }

            var described = DescribeObject(container, key, _getObjectAnswer, resolved.TotalLength, response.Headers.ContentType, response.ETag, response.LastModified, response.Metadata);
            if (!described.TryGetValue(out var metadata))
            {
                return described.Error!;
            }

            // 范围读取的流同样换掉读时 SDK 的核对异常：SDK 不核对范围读取的内容，实际遇不到，换法与整读一致（ADR-0322）
            var content = new BlobContent(metadata, new S3ObjectStream(response, exception => ContentRejected(container, exception), _ => ContentDiscarded(container)), resolved);
            response = null;
            return Result.Success(content);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            // 起点不在对象内：再 HEAD 一次拿总长写进错误参数（对象此间被删时如实报告不存在）；只取总长，不看 ETag 与修改时间。
            // 总长缺失时不以 0 写进错误参数：这个 HEAD 用到了长度，缺了就是不合规的应答（ADR-0322）
            var head = await HeadObjectAsync(container, key, cancellationToken).ConfigureAwait(false);
            if (!head.TryGetValue(out var found))
            {
                return Result.Failure<BlobContent>(head.Error!);
            }

            return found.Headers.ContentLength is >= 0 and var total
                ? Error.Validation(ObjectStorageErrors.RangeNotSatisfiable, ObjectStorageMessages.RangeNotSatisfiable, MessageArguments.Of(("offset", range.Offset), ("length", total)))
                : UnusableAnswer(container, _headObjectAnswer.Operation, _headObjectAnswer.ContentLengthMissingReason);
        }
        catch (AmazonS3Exception exception) when (IsNotFound(exception))
        {
            return NotFound(container, key);
        }
        catch (Exception exception) when (IsUnparsableAnswer(exception))
        {
            return UnusableAnswer(container, "GetObject", UnparsableReason("GetObject", exception));
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            return Unavailable(exception);
        }
        finally
        {
            DisposeUndelivered(response);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlobMultipartUpload>> StartAsync(ContainerName container, BlobKey key, BlobPutOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        // 内容类型写不出原值时什么请求都不发：发起的请求带着它，SDK 会抛出或改写（ADR-0295）
        if (!S3ContentTypeHeader.IsSentVerbatim(options.ContentType))
        {
            return ContentTypeUnsupported(options.ContentType);
        }

        // 服务会存成另一个值的元数据值同样什么请求都不发：宁可报错，不悄悄改值（ADR-0343）
        if (FindAlteredMetadataName(options.Metadata) is { } altered)
        {
            return MetadataValueUnsupported(altered);
        }

        // 不覆盖的发起要求条件写入已确认：探明之前、不认或关闭时拒绝，不发起没有保证的上传
        if (!options.Overwrite && await _conditionalWrites.RefuseAsync(container, key, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (!options.Overwrite)
        {
            var existing = await HeadExistsAsync(container, key, cancellationToken).ConfigureAwait(false);
            if (!existing.TryGetValue(out var found))
            {
                return Result.Failure<BlobMultipartUpload>(existing.Error!);
            }

            if (found)
            {
                return Exists(container, key);
            }
        }

        var ensured = false;
        while (true)
        {
            try
            {
                var request = new InitiateMultipartUploadRequest { BucketName = container.Value, Key = key.Value, ContentType = options.ContentType };
                foreach (var (name, value) in options.Metadata)
                {
                    // 头写不出原样的值照 RFC 2047 编码（ADR-0333）
                    request.Metadata[name] = S3MetadataHeader.Encode(value);
                }

                var response = await _client.InitiateMultipartUploadAsync(request, cancellationToken).ConfigureAwait(false);
                // 服务回了成功却没给当得了句柄的上传标识，是不合规的应答：按服务端错误返回，不让句柄的构造把 ArgumentException 抛给调用方。
                // 不重发：服务端可能已经建了这个上传，没有标识放弃不了，留给桶的生命周期规则清理（ADR-0302）
                if (!IsUsableUploadId(response.UploadId))
                {
                    LogUploadIdMissing(_logger, container.Value);
                    return Unavailable(UploadIdMissingReason);
                }

                return Result.Success(new BlobMultipartUpload(container, key, response.UploadId) { Overwrite = options.Overwrite });
            }
            catch (AmazonS3Exception exception) when (IsMetadataTooLarge(exception))
            {
                // 元数据的上限按服务自己的计法（Amazon S3 按编码之后的头）超了：是调用方能改的输入，不是存储故障（ADR-0343）
                return MetadataTooLarge();
            }
            catch (AmazonS3Exception exception) when (!ensured && _options.EnsureBuckets && IsNoSuchBucket(exception))
            {
                // 以前建桶失败时 SDK 的异常从这个 catch 里原样抛给调用方（ADR-0333）
                ensured = true;
                if (await CreateBucketAsync(container, cancellationToken).ConfigureAwait(false) is { } failed)
                {
                    return failed;
                }
            }
            catch (Exception exception) when (IsUnparsableAnswer(exception))
            {
                // 建桶在上一个 catch 里自己转成错误，解析不了的只能是发起的应答；不重发，同缺上传标识（ADR-0302、ADR-0322）
                return UnusableAnswer(container, "InitiateMultipartUpload", UnparsableReason("InitiateMultipartUpload", exception));
            }
            catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
            {
                return Unavailable(exception);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlobPart>> UploadPartAsync(BlobMultipartUpload upload, int partNumber, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(content);
        BlobMultipart.ValidatePartNumber(partNumber);

        // UploadPart 需要分片长度：不可定位的流先落到临时文件（关闭即删）
        Stream source = content;
        FileStream? buffered = null;
        try
        {
            if (!source.CanSeek)
            {
                buffered = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
                await source.CopyToAsync(buffered, cancellationToken).ConfigureAwait(false);
                buffered.Position = 0;
                source = buffered;
            }

            var length = source.Length - source.Position;
            var response = await _client.UploadPartAsync(new UploadPartRequest
            {
                BucketName = upload.Container.Value,
                Key = upload.Key.Value,
                UploadId = upload.UploadId,
                PartNumber = partNumber,
                InputStream = source,
                PartSize = length,
            }, cancellationToken).ConfigureAwait(false);
            // 服务回了成功却没给当得了分片的 ETag，是不合规的应答：按服务端错误返回，不让分片的构造把 ArgumentException 抛给调用方。
            // 不重发：这一片可能已在服务端，调用方重传同一编号即覆盖它，或从列出分片里取它的 ETag（ADR-0308）
            var etag = Unquote(response.ETag);
            if (!IsUsableETag(etag))
            {
                return UnusableAnswer(upload.Container, "UploadPart", PartETagMissingReason);
            }

            return Result.Success(new BlobPart(partNumber, etag, length));
        }
        catch (AmazonS3Exception exception) when (IsNoSuchUpload(exception))
        {
            return UploadNotFound(upload);
        }
        catch (Exception exception) when (IsUnparsableAnswer(exception))
        {
            return UnusableAnswer(upload.Container, "UploadPart", UnparsableReason("UploadPart", exception));
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            return Unavailable(exception);
        }
        finally
        {
            if (buffered is not null)
            {
                await buffered.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<BlobPart>>> ListPartsAsync(BlobMultipartUpload upload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        try
        {
            var parts = new List<BlobPart>();
            // 已列出的编号：一个上传的每个编号至多一片，编号又都在 1 到 10000 之内，集合与列表因此至多 10000 项
            var numbers = new HashSet<int>();
            // 这一页的起点：第一页不带标记，按 0 计
            var marker = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await _client.ListPartsAsync(new ListPartsRequest
                {
                    BucketName = upload.Container.Value,
                    Key = upload.Key.Value,
                    UploadId = upload.UploadId,
                    PartNumberMarker = marker == 0 ? null : marker.ToString(CultureInfo.InvariantCulture),
                }, cancellationToken).ConfigureAwait(false);
                foreach (var part in response.Parts ?? [])
                {
                    // 分片缺可用的编号或 ETag、长度为负，是不合规的应答：整个列出按服务端错误返回，不让分片的构造抛出。
                    // 不跳过那一片：跳过之后续传会重传它，或让完成清单少一片（ADR-0308）
                    if (part.PartNumber is not { } number || number is < 1 or > BlobMultipart.MaxPartNumber)
                    {
                        return UnusableAnswer(upload.Container, "ListParts", ListedPartNumberMissingReason);
                    }

                    var etag = Unquote(part.ETag);
                    if (!IsUsableETag(etag))
                    {
                        return UnusableAnswer(upload.Container, "ListParts", ListedPartETagMissingReason);
                    }

                    if (part.Size is < 0)
                    {
                        return UnusableAnswer(upload.Container, "ListParts", ListedPartSizeNegativeReason);
                    }

                    // 同一编号出现两次（同一页里或跨页）是应答重复交出了分片：照收下去，调用方拿列出的清单去完成，
                    // 清单不是严格升序，报成调用方给错了清单。它也替代了以前「累计超过 10000 片」的判定（ADR-0313、ADR-0322）
                    if (!numbers.Add(number))
                    {
                        return UnusableAnswer(upload.Container, "ListParts", ListedPartNumberRepeatedReason);
                    }

                    // S3 只交出编号大于这一页起点的分片：不大的混进清单，是起点之前的分片又交了一遍或分页错了位，
                    // 没重复也不照收；重复的先按重复报告（ADR-0333）
                    if (number <= marker)
                    {
                        return UnusableAnswer(upload.Container, "ListParts", ListedPartNumberBehindMarkerReason);
                    }

                    // 长度只用于说明、完成时不核对：缺失照旧当作 0
                    parts.Add(new BlobPart(number, etag, part.Size ?? 0));
                }

                if (response.IsTruncated != true)
                {
                    break;
                }

                // 截断时续页标记是这一页的最后一片：不比起点大，再请求只会回到原处；不小于 10000，后面不可能还有分片。
                // 标记每页严格增大且小于 10000，一次列出至多请求 10000 页（ADR-0313）
                if (response.NextPartNumberMarker is not { } next || next <= marker || next >= BlobMultipart.MaxPartNumber)
                {
                    return UnusableAnswer(upload.Container, "ListParts", NextPartNumberMarkerMissingReason);
                }

                marker = next;
            }

            return Result.Success<IReadOnlyList<BlobPart>>([.. parts.OrderBy(static p => p.PartNumber)]);
        }
        catch (AmazonS3Exception exception) when (IsNoSuchUpload(exception))
        {
            return UploadNotFound(upload);
        }
        catch (Exception exception) when (IsUnparsableAnswer(exception))
        {
            return UnusableAnswer(upload.Container, "ListParts", UnparsableReason("ListParts", exception));
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            return Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlobMetadata>> CompleteAsync(BlobMultipartUpload upload, IReadOnlyList<BlobPart> parts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        // 清单本身先在本地查，与文件系统实现同一个函数、同一个错误；分片是否上传、ETag 与最小分片由服务端判定
        if (BlobMultipart.ValidateParts(parts) is { IsFailure: true } invalid)
        {
            return invalid.Error!;
        }

        if (!upload.Overwrite && await _conditionalWrites.RefuseAsync(upload.Container, upload.Key, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        // 不覆盖：先 HEAD（已存在时不必拼接），再带 If-None-Match 完成，由服务端保证原子（服务认条件完成已由上面的放行确认）
        if (!upload.Overwrite)
        {
            var existing = await HeadExistsAsync(upload.Container, upload.Key, cancellationToken).ConfigureAwait(false);
            if (!existing.TryGetValue(out var found))
            {
                return Result.Failure<BlobMetadata>(existing.Error!);
            }

            if (found)
            {
                return Exists(upload.Container, upload.Key);
            }
        }

        try
        {
            var request = new CompleteMultipartUploadRequest
            {
                BucketName = upload.Container.Value,
                Key = upload.Key.Value,
                UploadId = upload.UploadId,
                PartETags = [.. parts.Select(static p => new PartETag(p.PartNumber, p.ETag))],
            };
            if (!upload.Overwrite)
            {
                request.IfNoneMatch = "*";
            }

            await _client.CompleteMultipartUploadAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            return Exists(upload.Container, upload.Key);
        }
        catch (AmazonS3Exception exception) when (IsNoSuchUpload(exception))
        {
            return UploadNotFound(upload);
        }
        catch (AmazonS3Exception exception) when (exception.ErrorCode is "InvalidPart" or "InvalidPartOrder" or "EntityTooSmall" or "MalformedXML")
        {
            return Error.Validation(ObjectStorageErrors.PartsInvalid, ObjectStorageMessages.PartsRejected, MessageArguments.Of(("reason", exception.ErrorCode + ": " + exception.Message)));
        }
        catch (Exception exception) when (IsUnparsableAnswer(exception))
        {
            // 对象可能已经拼成：不重发，同完成之后的 HEAD 缺内容（ADR-0313、ADR-0322）
            return UnusableAnswer(upload.Container, "CompleteMultipartUpload", UnparsableReason("CompleteMultipartUpload", exception));
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            return Unavailable(exception);
        }

        // 完成的响应只有 ETag：再 HEAD 一次拿长度、内容类型、自定义元数据与修改时间，按 HEAD 的应答判定（缺内容时对象已经拼成，ADR-0313）
        return await HeadAsync(upload.Container, upload.Key, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result>> AbortAsync(IReadOnlyList<BlobMultipartUpload> uploads, CancellationToken cancellationToken)
    {
        BlobMultipart.ValidateUploads(uploads);
        var results = new List<Result>(uploads.Count);
        foreach (var upload in uploads)
        {
            // S3 没有批量放弃的接口，逐项请求。每一项之前检查取消，请求途中的取消由 SDK 抛出：
            // 已放弃的不恢复，放弃是幂等的（不存在也成功），调用方整批重试即可
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await AbortOneAsync(upload, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlobSignedUrl>> SignAsync(ContainerName container, BlobKey key, BlobUrlRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Upload is { } upload && (upload.Container != container || upload.Key != key))
        {
            throw new ArgumentException("The multipart upload belongs to another blob.", nameof(request));
        }

        var expiresAt = _clock.GetUtcNow() + request.ExpiresIn;
        var presign = new GetPreSignedUrlRequest
        {
            BucketName = container.Value,
            Key = key.Value,
            Verb = request.Method == BlobUrlMethod.Get ? HttpVerb.GET : HttpVerb.PUT,
            Expires = expiresAt.UtcDateTime,
            // SDK 缺省按 HTTPS 签：兼容服务的端点是 http 时要显式指定，否则签出的地址连不上
            Protocol = _options.ServiceUrl is { } url && url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? Protocol.HTTP : Protocol.HTTPS,
        };
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (request.ContentType is { } contentType)
        {
            presign.ContentType = contentType;
            headers["Content-Type"] = contentType;
        }

        if (request.DownloadFileName is { } fileName)
        {
            presign.ResponseHeaderOverrides.ContentDisposition = BlobContentDisposition.Attachment(fileName);
        }

        if (request.Upload is { } part)
        {
            presign.UploadId = part.UploadId;
            presign.PartNumber = request.PartNumber!.Value;
        }

        try
        {
            var signed = await _client.GetPreSignedURLAsync(presign).ConfigureAwait(false);
            return Result.Success(new BlobSignedUrl { Url = new Uri(signed), Method = request.Method, ExpiresAt = expiresAt, Headers = headers });
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            return Unavailable(exception);
        }
    }

    /// <summary>
    /// 解析 <c>Content-Range: bytes 起-止/总长</c>
    /// </summary>
    private static bool TryParseContentRange(string? header, out BlobContentRange range)
    {
        range = null!;
        if (header is null || !header.StartsWith("bytes ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var slash = header.IndexOf('/', StringComparison.Ordinal);
        var dash = header.IndexOf('-', StringComparison.Ordinal);
        if (slash < 0 || dash < 0 || dash > slash
            || !long.TryParse(header.AsSpan(6, dash - 6), NumberStyles.None, CultureInfo.InvariantCulture, out var first)
            || !long.TryParse(header.AsSpan(dash + 1, slash - dash - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var last)
            || !long.TryParse(header.AsSpan(slash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var total)
            || last < first)
        {
            return false;
        }

        range = new BlobContentRange(first, last - first + 1, total);
        return true;
    }

    /// <summary>
    /// 分片上传不存在或已结束
    /// </summary>
    private static bool IsNoSuchUpload(AmazonS3Exception exception) => exception.ErrorCode == "NoSuchUpload";

    /// <summary>
    /// 应答里的上传标识当得了句柄
    /// </summary>
    /// <remarks>
    /// 照 <see cref="BlobMultipartUpload"/> 构造的公开前置条件先判：1 到 <see cref="MaxUploadIdLength"/> 个字符，不含空白与控制字符。条件写入探测的发起同用它（ADR-0313）
    /// </remarks>
    internal static bool IsUsableUploadId([NotNullWhen(true)] string? uploadId)
        => uploadId is { Length: > 0 and <= MaxUploadIdLength } && !uploadId.Any(static ch => char.IsWhiteSpace(ch) || char.IsControl(ch));

    /// <summary>
    /// 异常是 SDK 整读时把为空或字符数为奇数的 ETag 当作 MD5 解码失败
    /// </summary>
    /// <remarks>
    /// 依据 AWS SDK for .NET 4.0 的实现（ADR-0317）：<c>AmazonS3ResponseHandler.ProcessResponseHandlers</c> 在解组之后、交出应答之前，对不带范围的 <c>GetObject</c> 以 <c>AWSSDKUtils.HexStringToBytes</c> 解码去掉引号的 ETag，它为空或字符数为奇数时抛参数名为 <c>hex</c> 的 <see cref="ArgumentOutOfRangeException"/>，含十六进制数字之外的字符时由 <see cref="Convert.FromHexString(string)"/> 抛 <see cref="FormatException"/>。只认前一种的参数名，不看调用栈：参数名是 SDK 源码里的字面量，裁剪掉栈追踪的运行时（Native AOT）照样带着它（ADR-0322；以前按 <c>Exception.StackTrace</c> 里那一帧的文本识别）。后一种与解组时读头失败的 <see cref="FormatException"/> 分不开，归入 <see cref="IsUnparsableAnswer"/>，同是不合规的应答。用例 <c>S3WholeReadETagTests</c> 钉住这一 SDK 行为
    /// </remarks>
    internal static bool IsUndecodableETag(Exception exception) => exception is ArgumentOutOfRangeException { ParamName: "hex" };

    /// <summary>
    /// 异常是 SDK 解析应答失败
    /// </summary>
    /// <remarks>
    /// AWS SDK for .NET 4.0 的异步解组不包装异常：<c>Last-Modified</c> 不是日期、<c>Content-Length</c> 或 <c>x-amz-mp-parts-count</c> 不是数或越界、布尔值的头不是 <c>true</c> / <c>false</c>、XML 里的数不是数，都原样抛出 <see cref="FormatException"/> 或 <see cref="OverflowException"/>（ADR-0317 实测）；响应体不是良构的 XML 时原样抛出 <see cref="XmlException"/>（ADR-0322 实测，ADR-0333 起接住）。只认这三类：<see cref="UriFormatException"/> 来自端点而不是应答，除外；本类发出的请求里的值都已按各自的文法核对过（内容类型见 <see cref="S3ContentTypeHeader"/>），SDK 在请求一侧不会因它们抛出这几类异常（ADR-0322）。SDK 解析出错的应答（4xx、5xx）时自己兜住，抛的是 <see cref="AmazonS3Exception"/>，不经这里
    /// </remarks>
    internal static bool IsUnparsableAnswer(Exception exception)
        => exception is OverflowException or XmlException || (exception is FormatException && exception is not UriFormatException);

    /// <summary>
    /// 去掉两端的引号
    /// </summary>
    /// <remarks>
    /// 应答里的 ETag 带引号（S3 的写法）；缺失时为空串
    /// </remarks>
    internal static string Unquote(string? etag) => (etag ?? string.Empty).Trim('"');

    /// <summary>
    /// 去掉引号之后的 ETag 可用
    /// </summary>
    /// <remarks>
    /// 照 <see cref="BlobPart"/> 构造的公开前置条件判：不为空、不只有空白。对象的 ETag 同用它：<see cref="BlobMetadata.ETag"/> 要能随内容变化，空串与空白做不到（ADR-0313）。条件写入探测里上传分片的应答同用它（ADR-0322）
    /// </remarks>
    internal static bool IsUsableETag(string etag) => !string.IsNullOrWhiteSpace(etag);

    /// <summary>
    /// SDK 解析不了某一步的应答时 <c>reason</c> 的文本
    /// </summary>
    /// <remarks>
    /// 固定文本，只带请求名，不带应答里的值与异常的消息（消息里有那个写错的值，ADR-0322）；写坏的 XML 另写明是响应体（ADR-0333）
    /// </remarks>
    private static string UnparsableReason(string operation, Exception exception)
        => exception is XmlException
            ? operation + " returned a body the SDK could not parse as XML"
            : operation + " returned a header or element the SDK could not parse";

    /// <summary>
    /// 读内容时 SDK 发现内容对不上应答声明的 ETag 或校验和
    /// </summary>
    /// <remarks>
    /// 记一条 Warning（只带桶名与固定原因），交出携带 <see cref="ObjectStorageErrors.StoreUnavailable"/> 的 <see cref="FrameworkException"/> 由读内容的流抛出：元数据早已交出，流只能以异常报告；SDK 的异常作内层，它的消息不带应答里的值（ADR-0322）
    /// </remarks>
    private FrameworkException ContentRejected(ContainerName container, AmazonClientException exception)
    {
        LogAnswerUnusable(_logger, "GetObject", container.Value, ContentMismatchReason);
        return new FrameworkException(ObjectStorageErrors.StoreUnavailable, ContentMismatchMessage, exception);
    }

    /// <summary>
    /// 调用方没读到末尾就释放读内容的流时，SDK 在释放时核对出内容对不上
    /// </summary>
    /// <remarks>
    /// 释放不抛出（ADR-0333）：只记一条 Warning（桶名与固定原因），这是唯一留下的痕迹。SDK 在已读的字节数等于应答声明的长度时才在释放时核对，所以走到这里的是读够了长度、没读到返回 0 的调用方
    /// </remarks>
    private void ContentDiscarded(ContainerName container) => LogContentDiscarded(_logger, container.Value);

    /// <summary>
    /// 服务应答成功却缺了必需的内容
    /// </summary>
    /// <remarks>
    /// 记一条 Warning（只带请求名与桶名，不带应答里的值），按服务端错误返回 <see cref="ObjectStorageErrors.StoreUnavailable"/>（ADR-0302、ADR-0308、ADR-0313）
    /// </remarks>
    private Error UnusableAnswer(ContainerName container, string operation, string reason)
    {
        LogAnswerUnusable(_logger, operation, container.Value, reason);
        return Unavailable(reason);
    }

    /// <summary>
    /// 分片上传不存在或已结束
    /// </summary>
    private static Error UploadNotFound(BlobMultipartUpload upload)
        => Error.NotFound(ObjectStorageErrors.UploadNotFound, ObjectStorageMessages.UploadNotFound, MessageArguments.Of(("container", upload.Container.Value), ("key", upload.Key.Value), ("upload", upload.UploadId)));

    /// <summary>
    /// 空的自定义元数据
    /// </summary>
    private static IReadOnlyDictionary<string, string> EmptyMetadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 写一个对象
    /// </summary>
    /// <remarks>
    /// 不覆盖时先经条件写入的状态放行（探明之前、不认或关闭时直接失败，不访问写入目标），再 HEAD（已存在时不必上传内容）、带条件写入，由服务端保证原子。桶不存在且开启 <see cref="S3ObjectStorageOptions.EnsureBuckets"/> 时建桶后重试一次
    /// </remarks>
    private async ValueTask<Result<BlobMetadata>> PutOneAsync(ContainerName container, BlobUpload upload, CancellationToken cancellationToken)
    {
        // 内容类型写不出原值时先于一切判定，连不覆盖写入的 Head 也不发：SDK 会在请求发出一半时抛出，或改写之后签名对不上、悄悄存成改写后的值（ADR-0295、ADR-0308）
        if (!S3ContentTypeHeader.IsSentVerbatim(upload.Options.ContentType))
        {
            return ContentTypeUnsupported(upload.Options.ContentType);
        }

        // 服务会存成另一个值的元数据值同样先于一切判定、连 Head 也不发：宁可报错，不悄悄改值（ADR-0343）
        if (FindAlteredMetadataName(upload.Options.Metadata) is { } altered)
        {
            return MetadataValueUnsupported(altered);
        }

        // 条件写入没确认（尚未探明、不认或关闭）时照样先查再写会在并发时静默覆盖，所以明确拒绝，不做没有保证的尝试
        if (!upload.Options.Overwrite && await _conditionalWrites.RefuseAsync(container, upload.Key, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (!upload.Options.Overwrite)
        {
            var existing = await HeadExistsAsync(container, upload.Key, cancellationToken).ConfigureAwait(false);
            if (!existing.TryGetValue(out var found))
            {
                return Result.Failure<BlobMetadata>(existing.Error!);
            }

            if (found)
            {
                return Exists(container, upload.Key);
            }
        }

        // PutObject 需要内容长度：不可定位的流先落到临时文件（关闭即删），不把整个对象装进内存
        Stream content = upload.Content;
        FileStream? buffered = null;
        try
        {
            if (!content.CanSeek)
            {
                buffered = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
                await content.CopyToAsync(buffered, cancellationToken).ConfigureAwait(false);
                buffered.Position = 0;
                content = buffered;
            }

            var length = content.Length - content.Position;
            var ensured = false;
            while (true)
            {
                try
                {
                    var request = new PutObjectRequest
                    {
                        BucketName = container.Value,
                        Key = upload.Key.Value,
                        InputStream = content,
                        AutoCloseStream = false,
                        ContentType = upload.Options.ContentType,
                    };
                    if (!upload.Options.Overwrite)
                    {
                        request.IfNoneMatch = "*";
                    }

                    foreach (var (name, value) in upload.Options.Metadata)
                    {
                        // 头写不出原样的值照 RFC 2047 编码，写入结果里仍是原值（ADR-0333）
                        request.Metadata[name] = S3MetadataHeader.Encode(value);
                    }

                    var response = await _client.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
                    // PutObject 的应答本该带 ETag：缺了是不合规的应答，按服务端错误返回。不重发、不回滚：对象可能已经写进去了，调用方可以 Head 或重写（ADR-0313）
                    var etag = Unquote(response.ETag);
                    if (!IsUsableETag(etag))
                    {
                        return UnusableAnswer(container, "PutObject", PutETagMissingReason);
                    }

                    // 应答本来就不带修改时间：取本机收到应答时的时钟，不另发 HEAD（多一个请求，HEAD 失败会把写成的报成失败，并发覆盖时还可能读到别的版本）；
                    // 要服务端记下的时刻由调用方再 Head（维持，ADR-0322）
                    return Result.Success(new BlobMetadata
                    {
                        Key = upload.Key,
                        Length = length,
                        ContentType = upload.Options.ContentType,
                        ETag = etag,
                        LastModified = _clock.GetUtcNow(),
                        Metadata = new Dictionary<string, string>(upload.Options.Metadata, StringComparer.Ordinal),
                    });
                }
                catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    return Exists(container, upload.Key);
                }
                catch (AmazonS3Exception exception) when (IsMetadataTooLarge(exception))
                {
                    // 元数据的上限按服务自己的计法（Amazon S3 按编码之后的头）超了：是调用方能改的输入，不是存储故障；服务拒收了，对象没有写进去（ADR-0343）
                    return MetadataTooLarge();
                }
                catch (AmazonS3Exception exception) when (!ensured && _options.EnsureBuckets && IsNoSuchBucket(exception))
                {
                    ensured = true;
                    if (await CreateBucketAsync(container, cancellationToken).ConfigureAwait(false) is { } failed)
                    {
                        return failed;
                    }

                    content.Position = content.Length - length;
                }
            }
        }
        catch (Exception exception) when (IsUnparsableAnswer(exception))
        {
            // 对象可能已经写进去了：不重发、不回滚，同写入缺 ETag；建桶自己转成错误，到不了这里（ADR-0313、ADR-0322、ADR-0333）
            return UnusableAnswer(container, "PutObject", UnparsableReason("PutObject", exception));
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            return Unavailable(exception);
        }
        finally
        {
            if (buffered is not null)
            {
                await buffered.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 释放没能交出去的读取应答
    /// </summary>
    /// <remarks>
    /// 它持有 HTTP 连接，必须释放。SDK 释放整读的响应时，读到的长度等于应答声明的长度就核对 MD5：缺 <c>Content-Length</c> 时声明的是 0，内容一个字节没读也要核对，对不上即抛 <see cref="AmazonClientException"/>。这个应答已经判为不可用、内容不交出去，核对的结论没有用处，不让它盖过已经定下的结果（ADR-0322）
    /// </remarks>
    private static void DisposeUndelivered(GetObjectResponse? response)
    {
        try
        {
            response?.Dispose();
        }
        catch (AmazonClientException)
        {
            // 见上：内容不交出去，SDK 对没读的内容的核对不改变已经返回的错误
        }
    }

    /// <summary>
    /// 放弃一个上传
    /// </summary>
    /// <remarks>
    /// 上传或桶不存在也成功；其余服务端与网络错误返回 <see cref="ObjectStorageErrors.StoreUnavailable"/>，调用方的取消照常抛出
    /// </remarks>
    private async ValueTask<Result> AbortOneAsync(BlobMultipartUpload upload, CancellationToken cancellationToken)
    {
        try
        {
            await _client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest { BucketName = upload.Container.Value, Key = upload.Key.Value, UploadId = upload.UploadId }, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (AmazonS3Exception exception) when (IsNoSuchUpload(exception) || IsNotFound(exception))
        {
            return Result.Success();
        }
        catch (Exception exception) when (IsUnparsableAnswer(exception))
        {
            // 成功应答现在没有 SDK 按类型读的值：SDK 以后加了，解析异常同其余各步按不合规的应答返回，不漏给调用方（ADR-0333）
            return UnusableAnswer(upload.Container, "AbortMultipartUpload", UnparsableReason("AbortMultipartUpload", exception));
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            return Unavailable(exception);
        }
    }

    /// <summary>
    /// 建桶（<see cref="S3ObjectStorageOptions.EnsureBuckets"/>）
    /// </summary>
    /// <remarks>
    /// 成功为 <see langword="null"/>；服务端与网络错误为 <see cref="ObjectStorageErrors.StoreUnavailable"/>，应答解析不了同此、<c>reason</c> 点名 <c>PutBucket</c>（成功应答现在没有 SDK 按类型读的值，ADR-0333）；调用方的取消照常抛出。写入与发起分片上传在接住「桶不存在」的 catch 里调它：那里抛出的异常同一个 try 的其余 catch 接不住，所以在这里就转成错误
    /// </remarks>
    private async ValueTask<Error?> CreateBucketAsync(ContainerName container, CancellationToken cancellationToken)
    {
        try
        {
            await _client.PutBucketAsync(new PutBucketRequest { BucketName = container.Value }, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception exception) when (IsUnparsableAnswer(exception))
        {
            return UnusableAnswer(container, "PutBucket", UnparsableReason("PutBucket", exception));
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            return Unavailable(exception);
        }
    }

    /// <summary>
    /// 响应里的自定义元数据（去掉 <c>x-amz-meta-</c> 前缀，解开 RFC 2047 的 encoded-word）
    /// </summary>
    /// <remarks>
    /// 有一个值解不了时为 <see langword="null"/>，<paramref name="undecodable"/> 是那个值（只用来分辨原因，不进错误与日志）：调用方按不合规的应答返回，不原样交出编码的文本或读成 Latin-1 的乱码（ADR-0333、ADR-0343）
    /// </remarks>
    private static Dictionary<string, string>? ReadMetadata(MetadataCollection metadata, out string undecodable)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in metadata.Keys)
        {
            var name = key.StartsWith(MetadataPrefix, StringComparison.OrdinalIgnoreCase) ? key[MetadataPrefix.Length..] : key;
            var answered = metadata[key];
            if (!S3MetadataHeader.TryDecode(answered, out var value))
            {
                undecodable = answered;
                return null;
            }

            result[name] = value;
        }

        undecodable = string.Empty;
        return result;
    }

    /// <summary>
    /// 第一个服务会存成另一个值的元数据名
    /// </summary>
    /// <remarks>
    /// 整个是 encoded-word 形状的只有 ASCII 的值（<see cref="S3MetadataHeader.IsEncodedWords"/>，判定同读回时解码的形状）：原样写出读回时被解码，编码写出 Amazon S3 又先解码、存下只有 ASCII 的原值并原样返回（ADR-0343）。按名的序数次序取第一个，同一份元数据每次报同一个名；没有为 <see langword="null"/>
    /// </remarks>
    private static string? FindAlteredMetadataName(IReadOnlyDictionary<string, string> metadata)
    {
        string? first = null;
        foreach (var (name, value) in metadata)
        {
            if (S3MetadataHeader.IsEncodedWords(value) && (first is null || string.CompareOrdinal(name, first) < 0))
            {
                first = name;
            }
        }

        return first;
    }

    /// <summary>
    /// 把 SDK 的修改时间（UTC）转成带偏移的时刻
    /// </summary>
    /// <remarks>
    /// 缺失的修改时间到不了这里：调用方先判，缺了按不合规的应答返回（ADR-0313）
    /// </remarks>
    private static DateTimeOffset ToTimestamp(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    /// <summary>
    /// 由读取或 HEAD 的应答组成元数据
    /// </summary>
    /// <remarks>
    /// GetObject 与 HeadObject 的应答本该带 ETag、Last-Modified 与 Content-Length（Amazon S3 API Reference）：缺了是不合规的应答，按服务端错误返回，不以空串、最小时刻与 0 填进元数据（ADR-0313、ADR-0322）。<paramref name="length"/> 为负即缺长度：整读与 HEAD 传应答头的原值（缺失时 SDK 给 -1），范围读取传 <c>Content-Range</c> 里的总长
    /// </remarks>
    private Result<BlobMetadata> DescribeObject(ContainerName container, BlobKey key, ObjectAnswer answer, long length, string? contentType, string? etag, DateTime? lastModified, MetadataCollection metadata)
    {
        var tag = Unquote(etag);
        if (!IsUsableETag(tag))
        {
            return UnusableAnswer(container, answer.Operation, answer.ETagMissingReason);
        }

        if (lastModified is not { } modified)
        {
            return UnusableAnswer(container, answer.Operation, answer.LastModifiedMissingReason);
        }

        if (length < 0)
        {
            return UnusableAnswer(container, answer.Operation, answer.ContentLengthMissingReason);
        }

        // 解不了的 encoded-word 与原样的字节同缺内容：原样交出是编码的文本或乱码，替换掉又是另一种乱码，都不是存进去的值（ADR-0333、ADR-0343）
        if (ReadMetadata(metadata, out var undecodable) is not { } values)
        {
            return UnusableAnswer(container, answer.Operation, S3MetadataHeader.IsRaw(undecodable) ? answer.MetadataBytesUndecodableReason : answer.MetadataUndecodableReason);
        }

        return Result.Success(new BlobMetadata
        {
            Key = key,
            Length = length,
            ContentType = contentType ?? BlobPutOptions.DefaultContentType,
            ETag = tag,
            LastModified = ToTimestamp(modified),
            Metadata = values,
        });
    }

    /// <summary>
    /// HEAD 一个对象，原样交出应答
    /// </summary>
    /// <remarks>
    /// 不存在为 <see cref="ObjectStorageErrors.BlobNotFound"/>，其余服务端与网络错误为 <see cref="ObjectStorageErrors.StoreUnavailable"/>。不看 ETag、修改时间与长度：只判存在与否、只取总长的调用方各自判它们用到的（ADR-0313）。SDK 解析不了的应答同样是 <see cref="ObjectStorageErrors.StoreUnavailable"/>：SDK 没有交出应答，存在与否也无从判定（ADR-0322）
    /// </remarks>
    private async ValueTask<Result<GetObjectMetadataResponse>> HeadObjectAsync(ContainerName container, BlobKey key, CancellationToken cancellationToken)
    {
        try
        {
            return Result.Success(await _client.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = container.Value, Key = key.Value }, cancellationToken).ConfigureAwait(false));
        }
        catch (AmazonS3Exception exception) when (IsNotFound(exception))
        {
            return NotFound(container, key);
        }
        catch (Exception exception) when (IsUnparsableAnswer(exception))
        {
            return UnusableAnswer(container, "HeadObject", UnparsableReason("HeadObject", exception));
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            return Unavailable(exception);
        }
    }

    /// <summary>
    /// 经 HEAD 判定对象在不在
    /// </summary>
    /// <remarks>
    /// 在为真，不存在为假，其余失败原样返回。只看应答的状态，应答缺 ETag 或修改时间不影响判定（ADR-0313）
    /// </remarks>
    private async ValueTask<Result<bool>> HeadExistsAsync(ContainerName container, BlobKey key, CancellationToken cancellationToken)
    {
        var head = await HeadObjectAsync(container, key, cancellationToken).ConfigureAwait(false);
        if (head.IsSuccess)
        {
            return Result.Success(true);
        }

        return head.Error!.Code == ObjectStorageErrors.BlobNotFound ? Result.Success(false) : Result.Failure<bool>(head.Error);
    }

    /// <summary>
    /// 对象或桶不存在
    /// </summary>
    private static bool IsNotFound(AmazonS3Exception exception)
        => exception.StatusCode == HttpStatusCode.NotFound || exception.ErrorCode is "NoSuchKey" or "NotFound" || IsNoSuchBucket(exception);

    /// <summary>
    /// 桶不存在
    /// </summary>
    private static bool IsNoSuchBucket(AmazonS3Exception exception) => exception.ErrorCode == "NoSuchBucket";

    /// <summary>
    /// 服务按它自己的计法判定自定义元数据超过上限
    /// </summary>
    /// <remarks>
    /// S3 回 400 <c>MetadataTooLarge</c>：AWS SDK for .NET 4.0 抛出 <c>ErrorCode</c> 为它的 <see cref="AmazonS3Exception"/>、不重发（ADR-0343 实测）
    /// </remarks>
    private static bool IsMetadataTooLarge(AmazonS3Exception exception) => exception.ErrorCode == "MetadataTooLarge";

    /// <summary>
    /// 该异常是存储故障（服务端错误、网络错误、SDK 超时）而不是调用方取消
    /// </summary>
    /// <remarks>
    /// 单次请求超过 <see cref="S3ObjectStorageOptions.RequestTimeout"/> 时 SDK 抛出的是 <see cref="TimeoutException"/>（内层才是取消），与调用方有没有取消无关（ADR-0281）
    /// </remarks>
    private static bool IsStoreFailure(Exception exception, CancellationToken cancellationToken)
        => exception is AmazonServiceException or AmazonClientException or HttpRequestException or IOException or TimeoutException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    /// <summary>
    /// 不存在
    /// </summary>
    private static Error NotFound(ContainerName container, BlobKey key)
        => Error.NotFound(ObjectStorageErrors.BlobNotFound, ObjectStorageMessages.BlobNotFound, MessageArguments.Of(("container", container.Value), ("key", key.Value)));

    /// <summary>
    /// 已存在且不允许覆盖
    /// </summary>
    private static Error Exists(ContainerName container, BlobKey key)
        => Error.Conflict(ObjectStorageErrors.BlobExists, ObjectStorageMessages.BlobExists, MessageArguments.Of(("container", container.Value), ("key", key.Value)));

    /// <summary>
    /// 内容类型写不出原值
    /// </summary>
    /// <remarks>
    /// 见 <see cref="S3ContentTypeHeader"/>；不改写之后照写，由调用方决定换一个写法还是放弃（ADR-0295）
    /// </remarks>
    private static Error ContentTypeUnsupported(string contentType)
        => Error.Validation(ObjectStorageErrors.ContentTypeUnsupported, ObjectStorageMessages.ContentTypeUnsupported, MessageArguments.Of(("contentType", contentType)));

    /// <summary>
    /// 服务收不下的自定义元数据
    /// </summary>
    /// <remarks>
    /// 没有参数：服务不说是哪一项、上限按什么计（ADR-0343）
    /// </remarks>
    private static Error MetadataTooLarge() => Error.Validation(ObjectStorageErrors.MetadataTooLarge, ObjectStorageMessages.MetadataTooLarge);

    /// <summary>
    /// 服务会存成另一个值的元数据值
    /// </summary>
    /// <remarks>
    /// 参数只有元数据名、不带值：值至多约 2 KB，也可能是业务数据（ADR-0343）
    /// </remarks>
    private static Error MetadataValueUnsupported(string name)
        => Error.Validation(ObjectStorageErrors.MetadataValueUnsupported, ObjectStorageMessages.MetadataValueUnsupported, MessageArguments.Of(("name", name)));

    /// <summary>
    /// 存储不可用
    /// </summary>
    /// <remarks>
    /// SDK 的异常消息可能带端点与桶名，而这个错误会返回给调用方：<c>reason</c> 只写类型，完整异常只进日志
    /// </remarks>
    private Error Unavailable(Exception exception)
    {
        LogOperationFailed(_logger, exception);
        var type = exception.GetType();
        return Unavailable(type.FullName ?? type.Name);
    }

    /// <summary>
    /// 存储不可用
    /// </summary>
    private static Error Unavailable(string reason)
        => Error.Unavailable(ObjectStorageErrors.StoreUnavailable, ObjectStorageMessages.StoreUnavailable, MessageArguments.Of(("reason", reason)));

    /// <summary>
    /// 存储操作失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "An S3 operation of the blob store failed.")]
    private static partial void LogOperationFailed(ILogger logger, Exception exception);

    /// <summary>
    /// 发起分片上传的应答没有可用的上传标识
    /// </summary>
    /// <remarks>
    /// 不带应答里的值：它可能很长，也可能带控制字符
    /// </remarks>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The S3 service answered InitiateMultipartUpload for bucket '{Bucket}' without a usable upload ID.")]
    private static partial void LogUploadIdMissing(ILogger logger, string bucket);

    /// <summary>
    /// 服务的应答缺了必需的内容
    /// </summary>
    /// <remarks>
    /// <c>Reason</c> 是固定文本，不带应答里的值
    /// </remarks>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The S3 service answered {Operation} for bucket '{Bucket}' without the content the blob store needs: {Reason}.")]
    private static partial void LogAnswerUnusable(ILogger logger, string operation, string bucket, string reason);

    /// <summary>
    /// 调用方没读到末尾就释放读内容的流，SDK 在释放时核对出内容对不上
    /// </summary>
    /// <remarks>
    /// 释放不抛出（ADR-0333），这条日志是唯一留下的痕迹；不带应答里的值
    /// </remarks>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The S3 service answered GetObject for bucket '{Bucket}' with content that does not match its ETag or checksum; the caller released the stream before reading to its end, so the release did not throw.")]
    private static partial void LogContentDiscarded(ILogger logger, string bucket);

    /// <summary>
    /// 一种对象应答（读取或 HEAD）的请求名与缺内容时 <c>reason</c> 的文本
    /// </summary>
    /// <remarks>
    /// 文本固定，不带应答里的值（ADR-0313）
    /// </remarks>
    /// <param name="Operation">请求名</param>
    /// <param name="ETagMissingReason">没有可用的 ETag 时的文本</param>
    /// <param name="LastModifiedMissingReason">没有修改时间时的文本</param>
    /// <param name="ContentLengthMissingReason">没有可用的长度时的文本（ADR-0322）</param>
    /// <param name="MetadataUndecodableReason">自定义元数据里有解不了的 RFC 2047 encoded-word 时的文本（ADR-0333）</param>
    /// <param name="MetadataBytesUndecodableReason">自定义元数据里有别的写入方原样写进头、解不成合法值的字节时的文本（ADR-0343）</param>
    private sealed record ObjectAnswer(string Operation, string ETagMissingReason, string LastModifiedMissingReason, string ContentLengthMissingReason, string MetadataUndecodableReason, string MetadataBytesUndecodableReason);
}
