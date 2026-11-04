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
/// 内存里的 S3 替身，只认条件写入探测、启动、健康项与写入用到的请求
/// </summary>
/// <remarks>
/// 经 <see cref="DispatchProxy"/> 实现 <see cref="IAmazonS3"/>（代理类型由运行时从本类派生，所以本类公开且不密封）：对象只记键，按开关决定是否认 <c>If-None-Match: *</c>，可按请求名注入故障、让请求挂起直到取消，或把请求拦住直到放行；未模拟的请求抛出，用例里出现即说明发出了不该发的请求。线程安全，供并发探测的用例使用
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
            nameof(IAmazonS3.ListBucketsAsync) => Run(request, static () => new ListBucketsResponse(), cancellationToken),
            nameof(IAmazonS3.InitiateMultipartUploadAsync) => Run(request, () => Initiate((InitiateMultipartUploadRequest)args[0]!), cancellationToken),
            nameof(IAmazonS3.UploadPartAsync) => Run(request, () => UploadPart((UploadPartRequest)args[0]!), cancellationToken),
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
    private GetObjectMetadataResponse HeadObject(GetObjectMetadataRequest request)
    {
        RequireBucket(request.BucketName);
        return _objects.Contains(request.BucketName + "/" + request.Key)
            ? new GetObjectMetadataResponse { ETag = "\"etag\"", HttpStatusCode = HttpStatusCode.OK }
            : throw ServiceError(HttpStatusCode.NotFound, "NotFound");
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
        var id = Guid.NewGuid().ToString("N");
        _uploads.Add(id);
        return new InitiateMultipartUploadResponse { UploadId = id, HttpStatusCode = HttpStatusCode.OK };
    }

    /// <summary>
    /// 上传分片
    /// </summary>
    private UploadPartResponse UploadPart(UploadPartRequest request)
        => _uploads.Contains(request.UploadId) ? new UploadPartResponse { ETag = "\"part\"", HttpStatusCode = HttpStatusCode.OK } : throw ServiceError(HttpStatusCode.NotFound, "NoSuchUpload");

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
        _objects.Add(request.BucketName + "/" + request.Key);
        return new CompleteMultipartUploadResponse { ETag = "\"complete\"", HttpStatusCode = HttpStatusCode.OK };
    }

    /// <summary>
    /// 放弃分片上传
    /// </summary>
    private AbortMultipartUploadResponse Abort(AbortMultipartUploadRequest request)
        => _uploads.Remove(request.UploadId) ? new AbortMultipartUploadResponse { HttpStatusCode = HttpStatusCode.NoContent } : throw ServiceError(HttpStatusCode.NotFound, "NoSuchUpload");
}
