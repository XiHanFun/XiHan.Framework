// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// 探测 S3 兼容服务是否认条件写入。
/// </summary>
/// <remarks>
/// 不覆盖写入靠 <c>If-None-Match: *</c> 保证原子（ADR-0089）：不认它的服务上，先 HEAD 再写的两个写入方会让后写的静默覆盖先写的。探测在 <see cref="S3ObjectStorageOptions.HealthBucket"/> 里对固定的探测键 <see cref="ProbeKey"/> 带条件写入两次，第二次必须被拒（412）；需要时再以同一个键带条件完成一次分片上传，同样必须被拒，被拒的上传随即放弃。探测键写入后不删除：任何一次 412 都证明服务认得条件写入，键已存在时第一次写入就被拒，所以多个实例同时启动、反复启动都得到同一结论，凭据只需要对这一个键的写入权限，桶里只多一个空对象。
/// </remarks>
internal static class S3ConditionalWriteProbe
{
    /// <summary>
    /// 探测键。
    /// </summary>
    /// <remarks>
    /// 框架专用前缀 <c>.xihan/</c> 下的空对象，可单独授权（如 IAM 策略只放行 <c>arn:aws:s3:::&lt;桶&gt;/.xihan/probe/*</c> 的写入）。
    /// </remarks>
    internal const string ProbeKey = ".xihan/probe/conditional-write";

    /// <summary>
    /// 核对条件写入开启时配置了探测用的桶。
    /// </summary>
    /// <remarks>
    /// 没配时抛出 <see cref="ArgumentException"/>（配置错误）：缺省实例装配失败，命名实例由注册表翻译为定义不合法。<paramref name="prefix"/> 是报错用的设置键前缀。
    /// </remarks>
    internal static void RequireBucket(S3ObjectStorageOptions options, string prefix)
    {
        if (options.ConditionalWrites && options.HealthBucket is null)
        {
            throw new ArgumentException($"'{prefix}HealthBucket' must name a bucket the credentials can write to: the S3 provider verifies there that the service honours conditional writes (If-None-Match) before writing with Overwrite = false. Set it, or set '{prefix}ConditionalWrites' to 'false' if the application never writes with Overwrite = false.");
        }
    }

    /// <summary>
    /// 探测一次。
    /// </summary>
    /// <remarks>
    /// 成功表示服务认条件写入。失败的原因各有错误码：不认 <see cref="ObjectStorageErrors.ConditionalWriteUnavailable"/>，被拒绝（凭据无效或无权写探测键、建桶、放弃上传）<see cref="ObjectStorageErrors.ProbeDenied"/>，桶不存在 <see cref="ObjectStorageErrors.BlobNotFound"/>，连不上、超时或其余服务端错误 <see cref="ObjectStorageErrors.StoreUnavailable"/>。调用方的取消原样抛出。单次探测以 <see cref="S3ObjectStorageOptions.HealthProbeInterval"/> 为超时。
    /// </remarks>
    /// <param name="client">客户端。</param>
    /// <param name="options">选项；<see cref="S3ObjectStorageOptions.HealthBucket"/> 必须有值。</param>
    /// <param name="multipart">是否也探测分片上传的完成。</param>
    /// <param name="clock">时钟（超时按它计）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    internal static async ValueTask<Result> RunAsync(IAmazonS3 client, S3ObjectStorageOptions options, bool multipart, TimeProvider clock, CancellationToken cancellationToken)
    {
        var bucket = options.HealthBucket ?? throw new ArgumentException("The conditional write probe needs a health bucket.", nameof(options));
        using var deadline = new CancellationTokenSource(options.HealthProbeInterval, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var operation = "PutObject";
        // 记录当前请求名：失败时报出是哪一步（写探测键、建桶、分片上传的哪一步）
        void Step(string value) => operation = value;
        try
        {
            // 第一次写入：键不存在时写入成功；已存在（以前探过）时认条件写入的服务返回 412，这就已经证明了
            if (await PutAsync(client, bucket, options.EnsureBuckets, Step, linked.Token).ConfigureAwait(false))
            {
                // 键此刻一定存在（刚写入，探测键从不删除）：第二次写入还成功就是服务忽略了条件
                if (await PutAsync(client, bucket, ensureBucket: false, Step, linked.Token).ConfigureAwait(false))
                {
                    return Unsupported(bucket, "PutObject");
                }
            }

            if (multipart)
            {
                if (!await CompleteRefusedAsync(client, bucket, Step, linked.Token).ConfigureAwait(false))
                {
                    return Unsupported(bucket, "CompleteMultipartUpload");
                }
            }

            return Result.Success();
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.Forbidden || exception.ErrorCode is "AccessDenied" or "InvalidAccessKeyId" or "SignatureDoesNotMatch")
        {
            return Error.Denied(ObjectStorageErrors.ProbeDenied, ObjectStorageMessages.ConditionalWriteProbeDenied, MessageArguments.Of(("bucket", bucket), ("key", ProbeKey), ("operation", operation), ("reason", exception.ErrorCode ?? exception.StatusCode.ToString())));
        }
        catch (AmazonS3Exception exception) when (exception.ErrorCode == "NoSuchBucket")
        {
            return Error.NotFound(ObjectStorageErrors.BlobNotFound, ObjectStorageMessages.ConditionalWriteProbeBucketMissing, MessageArguments.Of(("bucket", bucket)));
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotImplemented)
        {
            // 服务明说没有实现这个请求（或其中的条件头）：同样保证不了不覆盖
            return Unsupported(bucket, operation);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested && exception is AmazonServiceException or AmazonClientException or HttpRequestException or IOException or OperationCanceledException)
        {
            // 调用方没有取消：取消只可能来自探测超时
            var reason = exception is OperationCanceledException
                ? string.Create(CultureInfo.InvariantCulture, $"{operation} of '{ProbeKey}' got no response within {options.HealthProbeInterval.TotalSeconds} s")
                : $"{operation} of '{ProbeKey}': {exception.Message}";
            return Error.Unavailable(ObjectStorageErrors.StoreUnavailable, ObjectStorageMessages.StoreUnavailable, MessageArguments.Of(("reason", reason)));
        }
    }

    /// <summary>
    /// 带条件写入探测键。
    /// </summary>
    /// <remarks>
    /// 写入成功返回 <see langword="true"/>，被条件拒绝（412）返回 <see langword="false"/>；桶不存在且允许建桶时建桶后重试一次，其余错误抛出。<paramref name="step"/> 记录当前请求名，供报错。
    /// </remarks>
    private static async ValueTask<bool> PutAsync(IAmazonS3 client, string bucket, bool ensureBucket, Action<string> step, CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                step("PutObject");
                await client.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = ProbeKey, ContentBody = string.Empty, IfNoneMatch = "*" }, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                return false;
            }
            catch (AmazonS3Exception exception) when (ensureBucket && exception.ErrorCode == "NoSuchBucket")
            {
                // 与写入时的 EnsureBuckets 相同：建桶后只重试一次
                ensureBucket = false;
                step("CreateBucket");
                await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket }, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 以探测键带条件完成一次分片上传。
    /// </summary>
    /// <remarks>
    /// 被条件拒绝（412）返回 <see langword="true"/> 并放弃这个上传（不留未完成的分片上传；服务已自行清掉时不算错）；完成了返回 <see langword="false"/>（服务忽略了条件，探测键被同样为空的内容覆盖，无害）。中途出错时尽力放弃上传，再抛出原来的错误。<paramref name="step"/> 记录当前请求名，供报错。
    /// </remarks>
    private static async ValueTask<bool> CompleteRefusedAsync(IAmazonS3 client, string bucket, Action<string> step, CancellationToken cancellationToken)
    {
        step("CreateMultipartUpload");
        var uploadId = (await client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest { BucketName = bucket, Key = ProbeKey }, cancellationToken).ConfigureAwait(false)).UploadId;
        try
        {
            step("UploadPart");
            using var content = new MemoryStream([0], writable: false);
            var part = await client.UploadPartAsync(new UploadPartRequest { BucketName = bucket, Key = ProbeKey, UploadId = uploadId, PartNumber = 1, InputStream = content, PartSize = content.Length }, cancellationToken).ConfigureAwait(false);
            step("CompleteMultipartUpload");
            try
            {
                // 唯一的一片就是末片，不受 5 MiB 下限约束；探测键已存在，认条件的服务必须拒绝
                await client.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest { BucketName = bucket, Key = ProbeKey, UploadId = uploadId, PartETags = [new PartETag(1, part.ETag)], IfNoneMatch = "*" }, cancellationToken).ConfigureAwait(false);
                return false;
            }
            catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                // 被拒：服务认条件完成，往下放弃这个上传
            }
        }
        catch
        {
            // 探测本身出错（501、断连、超时）：尽力放弃上传，放弃的失败不掩盖原来的错误
            try
            {
                await client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest { BucketName = bucket, Key = ProbeKey, UploadId = uploadId }, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is AmazonServiceException or AmazonClientException or HttpRequestException or IOException or OperationCanceledException)
            {
                // 尽力而为：未完成的上传留给桶的生命周期规则清理
            }

            throw;
        }

        // 被拒后的上传仍在：必须放弃，放弃被拒绝时按探测被拒报告（凭据缺少放弃分片上传的权限）
        step("AbortMultipartUpload");
        try
        {
            await client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest { BucketName = bucket, Key = ProbeKey, UploadId = uploadId }, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonS3Exception exception) when (exception.ErrorCode == "NoSuchUpload")
        {
            // 服务在拒绝时已经清掉了上传
        }

        return true;
    }

    /// <summary>
    /// 服务不认条件写入。
    /// </summary>
    private static Error Unsupported(string bucket, string operation)
        => Error.Unavailable(ObjectStorageErrors.ConditionalWriteUnavailable, ObjectStorageMessages.ConditionalWriteUnsupported, MessageArguments.Of(("bucket", bucket), ("operation", operation)));
}
