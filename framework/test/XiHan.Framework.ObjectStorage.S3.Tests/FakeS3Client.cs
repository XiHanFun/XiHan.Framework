// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Net;
using System.Reflection;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 内存里的 S3 替身，只认存储与条件写入探测用到的请求
/// </summary>
/// <remarks>
/// 认的是条件写入探测、启动、健康项、写入、批量删除与分片上传用到的请求。经 <see cref="DispatchProxy"/> 实现 <see cref="IAmazonS3"/>（代理类型由运行时从本类派生，所以本类公开且不密封）：对象只记键，按开关决定是否认 <c>If-None-Match: *</c>，可按请求名注入故障、让请求挂起直到取消，或把请求拦住直到放行；未模拟的请求抛出，用例里出现即说明发出了不该发的请求。线程安全，供并发探测的用例使用
/// </remarks>
public class FakeS3Client : DispatchProxy
{
    /// <summary>
    /// 保护状态
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 已存在的桶
    /// </summary>
    private readonly HashSet<string> _buckets = new(StringComparer.Ordinal);

    /// <summary>
    /// 已存在的对象（桶/键）
    /// </summary>
    private readonly HashSet<string> _objects = new(StringComparer.Ordinal);

    /// <summary>
    /// 未结束的分片上传标识
    /// </summary>
    private readonly HashSet<string> _uploads = new(StringComparer.Ordinal);

    /// <summary>
    /// 每个未结束的上传里已上传的分片（上传标识 → 编号 → 分片）
    /// </summary>
    private readonly Dictionary<string, SortedDictionary<int, PartDetail>> _parts = new(StringComparer.Ordinal);

    /// <summary>
    /// 收到的请求名，按到达顺序
    /// </summary>
    private readonly List<string> _requests = [];

    /// <summary>
    /// 放行被 <see cref="Held"/> 拦住的请求
    /// </summary>
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 第一个被拦住的请求到达时完成
    /// </summary>
    private readonly TaskCompletionSource _heldArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 单次写入是否认条件
    /// </summary>
    public bool HonoursPutCondition { get; set; } = true;

    /// <summary>
    /// 分片上传的完成是否认条件
    /// </summary>
    public bool HonoursCompleteCondition { get; set; } = true;

    /// <summary>
    /// 按请求名注入的故障
    /// </summary>
    /// <remarks>
    /// 请求名是 S3 的操作名（<c>PutObject</c>、<c>AbortMultipartUpload</c>）；返回非空时该请求以它失败
    /// </remarks>
    public Func<string, Exception?> Fault { get; set; } = static _ => null;

    /// <summary>
    /// 发起分片上传时应答里的上传标识
    /// </summary>
    /// <remarks>
    /// 缺省每次一个新的 GUID；换掉它模拟不合规的应答：返回 <see langword="null"/> 时应答不带标识，也可以返回空串或当不了句柄的标识。只有非空的标识记为未结束的上传
    /// </remarks>
    public Func<string?> NextUploadId { get; set; } = static () => Guid.NewGuid().ToString("N");

    /// <summary>
    /// 上传分片时应答里的 ETag
    /// </summary>
    /// <remarks>
    /// 缺省带引号的 <c>"part"</c>；换掉它模拟不合规的应答：返回 <see langword="null"/> 时应答不带 ETag，也可以返回空串、只有引号或只有空白的 ETag。列出分片时交出的就是它
    /// </remarks>
    public Func<string?> NextPartETag { get; set; } = static () => "\"part\"";

    /// <summary>
    /// 列出分片时应答里的分片
    /// </summary>
    /// <remarks>
    /// 缺省原样交出这个上传里已上传的分片（按编号升序）；换掉它模拟不合规的应答，如分片没有编号、编号越界、没有 ETag 或长度为负
    /// </remarks>
    public Func<IReadOnlyList<PartDetail>, IReadOnlyList<PartDetail>> ListedParts { get; set; } = static parts => parts;

    /// <summary>
    /// 列出分片时的整页应答
    /// </summary>
    /// <remarks>
    /// 缺省为 <see langword="null"/>：一页交出全部分片（经 <see cref="ListedParts"/>）。设了它就按请求（带着 <c>PartNumberMarker</c>）与已上传的分片给出整页应答，模拟分页，如截断却不给续页标记、标记不前进
    /// </remarks>
    public Func<ListPartsRequest, IReadOnlyList<PartDetail>, ListPartsResponse>? PartPages { get; set; }

    /// <summary>
    /// 对象的元数据（HEAD）应答
    /// </summary>
    /// <remarks>
    /// 缺省原样交出带 ETag、修改时间与长度的应答；换掉它模拟不合规的应答，如不带 ETag、修改时间或长度
    /// </remarks>
    public Func<GetObjectMetadataResponse, GetObjectMetadataResponse> HeadAnswer { get; set; } = static answer => answer;

    /// <summary>
    /// 批量删除时应答里的错误项
    /// </summary>
    /// <remarks>
    /// 收到这一批的键，缺省没有错误项；换掉它模拟逐项失败或不合规的应答（错误项不带键、带了不在这一批里的键）。带了这一批里的键的错误项，那个键不删除，其余照删。有错误项时同 AWS SDK for .NET 4.0 抛出带着应答的 <see cref="DeleteObjectsException"/>，<see cref="DeleteErrorsAnswered"/> 为真时改为在应答里交出（ADR-0333）
    /// </remarks>
    public Func<IReadOnlyList<string>, IReadOnlyList<DeleteError>> DeleteErrors { get; set; } = static _ => [];

    /// <summary>
    /// 批量删除的错误项是否在应答里交出、不抛出
    /// </summary>
    /// <remarks>
    /// 缺省为假：同 AWS SDK for .NET 4.0，有错误项就抛 <see cref="DeleteObjectsException"/>（ADR-0333）；为真时模拟交出带错误项的应答的 SDK
    /// </remarks>
    public bool DeleteErrorsAnswered { get; set; }

    /// <summary>
    /// 挂起直到取消的请求名
    /// </summary>
    public string? Hang { get; set; }

    /// <summary>
    /// 拦住直到 <see cref="Release"/> 的请求名
    /// </summary>
    /// <remarks>
    /// 被拦住的请求已计入 <see cref="Requests"/>；放行之后同名请求不再被拦
    /// </remarks>
    public string? Held { get; set; }

    /// <summary>
    /// 收到的请求名的快照
    /// </summary>
    public IReadOnlyList<string> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>
    /// 未结束的分片上传数
    /// </summary>
    public int OpenUploads
    {
        get
        {
            lock (_gate)
            {
                return _uploads.Count;
            }
        }
    }

    /// <summary>
    /// 创建替身
    /// </summary>
    /// <param name="buckets">预先存在的桶</param>
    public static FakeS3Client Create(params string[] buckets)
    {
        var fake = (FakeS3Client)(object)Create<IAmazonS3, FakeS3Client>();
        foreach (var bucket in buckets)
        {
            fake._buckets.Add(bucket);
        }

        return fake;
    }

    /// <summary>
    /// 以服务端错误失败的异常
    /// </summary>
    public static AmazonS3Exception ServiceError(HttpStatusCode status, string code)
        => new($"Simulated {code}.", ErrorType.Sender, code, "fake-request", status);

    /// <summary>
    /// 第一个被 <see cref="Held"/> 拦住的请求到达时完成
    /// </summary>
    public Task HeldArrived => _heldArrived.Task;

    /// <summary>
    /// 放行被拦住的请求
    /// </summary>
    public void Release() => _release.TrySetResult();

    /// <summary>
    /// 建一个桶（模拟运维事后建桶）
    /// </summary>
    public void AddBucket(string bucket)
    {
        lock (_gate)
        {
            _buckets.Add(bucket);
        }
    }

    /// <summary>
    /// 当作 <see cref="IAmazonS3"/> 使用
    /// </summary>
    public IAmazonS3 AsClient() => (IAmazonS3)(object)this;

    /// <summary>
    /// 对象是否存在
    /// </summary>
    public bool Contains(string bucket, string key)
    {
        lock (_gate)
        {
            return _objects.Contains(bucket + "/" + key);
        }
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        args ??= [];
        var name = targetMethod.Name;
        if (name == nameof(IDisposable.Dispose))
        {
            return null;
        }

        // 请求名去掉 Async 后缀，与 S3 的操作名一致（PutObject、CompleteMultipartUpload）
        var request = name.Replace("Async", string.Empty, StringComparison.Ordinal);
        var cancellationToken = args.OfType<CancellationToken>().FirstOrDefault();
        lock (_gate)
        {
            _requests.Add(request);
        }

        return name switch
        {
            nameof(IAmazonS3.PutObjectAsync) => Run(request, () => Put((PutObjectRequest)args[0]!), cancellationToken),
            nameof(IAmazonS3.PutBucketAsync) => Run(request, () => PutBucket((PutBucketRequest)args[0]!), cancellationToken),
            nameof(IAmazonS3.HeadBucketAsync) => Run(request, () => HeadBucket((HeadBucketRequest)args[0]!), cancellationToken),
            nameof(IAmazonS3.GetObjectMetadataAsync) when args[0] is GetObjectMetadataRequest head => Run(request, () => HeadObject(head), cancellationToken),
            nameof(IAmazonS3.DeleteObjectsAsync) when args[0] is DeleteObjectsRequest delete => Run(request, () => DeleteObjects(delete), cancellationToken),
            nameof(IAmazonS3.ListBucketsAsync) => Run(request, static () => new ListBucketsResponse(), cancellationToken),
            nameof(IAmazonS3.InitiateMultipartUploadAsync) => Run(request, () => Initiate((InitiateMultipartUploadRequest)args[0]!), cancellationToken),
            nameof(IAmazonS3.UploadPartAsync) => Run(request, () => UploadPart((UploadPartRequest)args[0]!), cancellationToken),
            nameof(IAmazonS3.ListPartsAsync) when args[0] is ListPartsRequest list => Run(request, () => ListParts(list), cancellationToken),
            nameof(IAmazonS3.CompleteMultipartUploadAsync) => Run(request, () => Complete((CompleteMultipartUploadRequest)args[0]!), cancellationToken),
            nameof(IAmazonS3.AbortMultipartUploadAsync) => Run(request, () => Abort((AbortMultipartUploadRequest)args[0]!), cancellationToken),
            _ => throw new NotSupportedException($"The fake S3 client does not simulate '{request}'."),
        };
    }

    /// <summary>
    /// 按故障与挂起设置执行一个请求
    /// </summary>
    private async Task<T> Run<T>(string name, Func<T> handler, CancellationToken cancellationToken)
    {
        if (Hang == name)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            throw new UnreachableException();
        }

        if (Held == name)
        {
            _heldArrived.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        await Task.Yield();
        if (Fault(name) is { } fault)
        {
            throw fault;
        }

        lock (_gate)
        {
            return handler();
        }
    }

    /// <summary>
    /// 桶不存在的异常
    /// </summary>
    private void RequireBucket(string bucket)
    {
        if (!_buckets.Contains(bucket))
        {
            throw ServiceError(HttpStatusCode.NotFound, "NoSuchBucket");
        }
    }

    /// <summary>
    /// 单次写入
    /// </summary>
    private PutObjectResponse Put(PutObjectRequest request)
    {
        RequireBucket(request.BucketName);
        if (request.IfNoneMatch == "*" && HonoursPutCondition && !_objects.Add(request.BucketName + "/" + request.Key))
        {
            throw ServiceError(HttpStatusCode.PreconditionFailed, "PreconditionFailed");
        }

        _objects.Add(request.BucketName + "/" + request.Key);
        return new PutObjectResponse { ETag = "\"etag\"", HttpStatusCode = HttpStatusCode.OK };
    }

    /// <summary>
    /// 对象的元数据（HEAD）
    /// </summary>
    /// <remarks>
    /// 应答带 ETag、修改时间与 <c>Content-Length</c>（同 Amazon S3；替身不存内容，长度记为 0），经 <see cref="HeadAnswer"/> 换成要模拟的应答
    /// </remarks>
    private GetObjectMetadataResponse HeadObject(GetObjectMetadataRequest request)
    {
        RequireBucket(request.BucketName);
        return _objects.Contains(request.BucketName + "/" + request.Key)
            ? HeadAnswer(new GetObjectMetadataResponse { ETag = "\"etag\"", LastModified = new DateTime(2026, 11, 26, 0, 0, 0, DateTimeKind.Utc), HttpStatusCode = HttpStatusCode.OK, Headers = { ContentLength = 0 } })
            : throw ServiceError(HttpStatusCode.NotFound, "NotFound");
    }

    /// <summary>
    /// 批量删除
    /// </summary>
    /// <remarks>
    /// 静默模式（同 Amazon S3）：应答只列错误项，取 <see cref="DeleteErrors"/>；错误项带着这一批里的键时那个键不删除。有错误项时同 SDK 抛出带着这个应答的 <see cref="DeleteObjectsException"/>，除非 <see cref="DeleteErrorsAnswered"/>
    /// </remarks>
    private DeleteObjectsResponse DeleteObjects(DeleteObjectsRequest request)
    {
        RequireBucket(request.BucketName);
        var keys = request.Objects.Select(static o => o.Key).ToList();
        var errors = DeleteErrors(keys);
        var kept = errors.Where(static e => e.Key is not null).Select(static e => e.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var key in keys.Where(key => !kept.Contains(key)))
        {
            _objects.Remove(request.BucketName + "/" + key);
        }

        var response = new DeleteObjectsResponse { DeleteErrors = [.. errors], HttpStatusCode = HttpStatusCode.OK };
        return errors.Count > 0 && !DeleteErrorsAnswered ? throw new DeleteObjectsException(response) : response;
    }

    /// <summary>
    /// 建桶
    /// </summary>
    private PutBucketResponse PutBucket(PutBucketRequest request)
    {
        _buckets.Add(request.BucketName);
        return new PutBucketResponse { HttpStatusCode = HttpStatusCode.OK };
    }

    /// <summary>
    /// 桶是否存在
    /// </summary>
    private HeadBucketResponse HeadBucket(HeadBucketRequest request)
    {
        RequireBucket(request.BucketName);
        return new HeadBucketResponse { HttpStatusCode = HttpStatusCode.OK };
    }

    /// <summary>
    /// 发起分片上传
    /// </summary>
    private InitiateMultipartUploadResponse Initiate(InitiateMultipartUploadRequest request)
    {
        RequireBucket(request.BucketName);
        var id = NextUploadId();
        if (!string.IsNullOrEmpty(id))
        {
            _uploads.Add(id);
        }

        return new InitiateMultipartUploadResponse { UploadId = id, HttpStatusCode = HttpStatusCode.OK };
    }

    /// <summary>
    /// 上传分片
    /// </summary>
    /// <remarks>
    /// 后一次覆盖同一编号的前一次（同 Amazon S3）；应答的 ETag 取 <see cref="NextPartETag"/>
    /// </remarks>
    private UploadPartResponse UploadPart(UploadPartRequest request)
    {
        if (!_uploads.Contains(request.UploadId))
        {
            throw ServiceError(HttpStatusCode.NotFound, "NoSuchUpload");
        }

        var etag = NextPartETag();
        var number = request.PartNumber ?? throw new InvalidOperationException("The fake S3 client got an UploadPart request without a part number.");
        if (!_parts.TryGetValue(request.UploadId, out var parts))
        {
            parts = [];
            _parts[request.UploadId] = parts;
        }

        parts[number] = new PartDetail { PartNumber = number, ETag = etag, Size = request.PartSize };
        return new UploadPartResponse { ETag = etag, HttpStatusCode = HttpStatusCode.OK };
    }

    /// <summary>
    /// 列出分片
    /// </summary>
    /// <remarks>
    /// 缺省一页交出全部分片，经 <see cref="ListedParts"/> 换成要模拟的应答；设了 <see cref="PartPages"/> 时按它分页
    /// </remarks>
    private ListPartsResponse ListParts(ListPartsRequest request)
    {
        if (!_uploads.Contains(request.UploadId))
        {
            throw ServiceError(HttpStatusCode.NotFound, "NoSuchUpload");
        }

        IReadOnlyList<PartDetail> uploaded = _parts.TryGetValue(request.UploadId, out var parts) ? [.. parts.Values] : [];
        return PartPages is { } pages
            ? pages(request, uploaded)
            : new ListPartsResponse { Parts = [.. ListedParts(uploaded)], IsTruncated = false, HttpStatusCode = HttpStatusCode.OK };
    }

    /// <summary>
    /// 完成分片上传
    /// </summary>
    /// <remarks>
    /// 被条件拒绝时上传保留（同 Amazon S3）
    /// </remarks>
    private CompleteMultipartUploadResponse Complete(CompleteMultipartUploadRequest request)
    {
        if (!_uploads.Contains(request.UploadId))
        {
            throw ServiceError(HttpStatusCode.NotFound, "NoSuchUpload");
        }

        if (request.IfNoneMatch == "*" && HonoursCompleteCondition && _objects.Contains(request.BucketName + "/" + request.Key))
        {
            throw ServiceError(HttpStatusCode.PreconditionFailed, "PreconditionFailed");
        }

        _uploads.Remove(request.UploadId);
        _parts.Remove(request.UploadId);
        _objects.Add(request.BucketName + "/" + request.Key);
        return new CompleteMultipartUploadResponse { ETag = "\"complete\"", HttpStatusCode = HttpStatusCode.OK };
    }

    /// <summary>
    /// 放弃分片上传
    /// </summary>
    private AbortMultipartUploadResponse Abort(AbortMultipartUploadRequest request)
    {
        if (!_uploads.Remove(request.UploadId))
        {
            throw ServiceError(HttpStatusCode.NotFound, "NoSuchUpload");
        }

        _parts.Remove(request.UploadId);
        return new AbortMultipartUploadResponse { HttpStatusCode = HttpStatusCode.NoContent };
    }
}
