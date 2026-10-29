// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Amazon.S3;
using Amazon.S3.Model;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 真实的 S3 兼容服务来自环境变量 <c>XIHAN_TEST_S3</c>。
/// </summary>
/// <remarks>
/// 服务端点，如 <c>http://localhost:9000</c>。密钥来自 <c>XIHAN_TEST_S3_ACCESS_KEY</c> / <c>XIHAN_TEST_S3_SECRET_KEY</c>（缺省 <c>minioadmin</c>）。未设置端点时跳过需要它的用例。
/// </remarks>
internal static class S3TestServer
{
    /// <summary>
    /// 指向测试服务端点的环境变量名。
    /// </summary>
    public const string EnvironmentVariable = "XIHAN_TEST_S3";

    /// <summary>
    /// 进程内共享的客户端，首次使用时创建。
    /// </summary>
    private static readonly Lazy<AmazonS3Client> _client = new(() => S3ObjectStorageFeature.CreateClient(Options()));

    /// <summary>
    /// 已确认存在的桶。
    /// </summary>
    private static readonly HashSet<string> _buckets = new(StringComparer.Ordinal);

    /// <summary>
    /// 保护 <see cref="_buckets"/>。
    /// </summary>
    private static readonly Lock _gate = new();

    /// <summary>
    /// 环境变量里的服务端点。
    /// </summary>
    /// <remarks>
    /// 未设置为 <see langword="null"/>。
    /// </remarks>
    public static string? ServiceUrl => Environment.GetEnvironmentVariable(EnvironmentVariable);

    /// <summary>
    /// 访问密钥 ID。
    /// </summary>
    public static string AccessKey => Environment.GetEnvironmentVariable("XIHAN_TEST_S3_ACCESS_KEY") is { Length: > 0 } key ? key : "minioadmin";

    /// <summary>
    /// 访问密钥。
    /// </summary>
    public static string SecretKey => Environment.GetEnvironmentVariable("XIHAN_TEST_S3_SECRET_KEY") is { Length: > 0 } key ? key : "minioadmin";

    /// <summary>
    /// 返回服务端点。
    /// </summary>
    /// <remarks>
    /// 未设置时跳过当前用例。
    /// </remarks>
    public static string RequireServiceUrl()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ServiceUrl), $"Set the environment variable '{EnvironmentVariable}' to an S3-compatible endpoint to run this test.");
        return ServiceUrl!;
    }

    /// <summary>
    /// 指向测试服务的选项。
    /// </summary>
    public static S3ObjectStorageOptions Options(bool ensureBuckets = false) => new()
    {
        ServiceUrl = RequireServiceUrl(),
        AccessKey = AccessKey,
        SecretKey = SecretKey,
        EnsureBuckets = ensureBuckets,
    };

    /// <summary>
    /// 宿主配置：<c>XiHan:ObjectStorage:S3</c> 节指向测试服务。
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string?>> Configuration() =>
    [
        new(S3ObjectStorageOptions.ConfigurationSection + ":ServiceUrl", RequireServiceUrl()),
        new(S3ObjectStorageOptions.ConfigurationSection + ":AccessKey", AccessKey),
        new(S3ObjectStorageOptions.ConfigurationSection + ":SecretKey", SecretKey),
    ];

    /// <summary>
    /// 共享客户端。
    /// </summary>
    /// <remarks>
    /// 未设置端点时跳过当前用例。
    /// </remarks>
    public static IAmazonS3 Client()
    {
        RequireServiceUrl();
        return _client.Value;
    }

    /// <summary>
    /// 创建指向测试服务的存储，并保证桶存在。
    /// </summary>
    public static S3BlobStore CreateStore(string bucket)
    {
        var client = Client();
        EnsureBucket(client, bucket);
        return new S3BlobStore(client, Options(), TimeProvider.System);
    }

    /// <summary>
    /// 桶不存在就创建。
    /// </summary>
    /// <remarks>
    /// 每个桶只检查一次。
    /// </remarks>
    public static void EnsureBucket(IAmazonS3 client, string bucket)
    {
        lock (_gate)
        {
            if (_buckets.Contains(bucket))
            {
                return;
            }

            try
            {
                client.PutBucketAsync(new PutBucketRequest { BucketName = bucket }).GetAwaiter().GetResult();
            }
            catch (AmazonS3Exception exception) when (exception.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
            {
                // 已存在即可
            }

            _buckets.Add(bucket);
        }
    }
}
