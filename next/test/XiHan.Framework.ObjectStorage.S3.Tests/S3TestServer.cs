// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Amazon.S3;
using Amazon.S3.Model;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 真实的 S3 兼容服务来自环境变量 <c>XIHAN_TEST_S3</c>
/// </summary>
/// <remarks>
/// 服务端点，如 <c>http://localhost:9000</c>。密钥来自 <c>XIHAN_TEST_S3_ACCESS_KEY</c> / <c>XIHAN_TEST_S3_SECRET_KEY</c>（缺省 <c>rustfsadmin</c>，即 RustFS 镜像不设密钥时的根凭据，ADR-0086）。未设置端点时跳过需要它的用例。另一个端点 <c>XIHAN_TEST_S3_UNCONDITIONAL</c> 指向不认条件写入的兼容服务（如 Zenko CloudServer），用同一对密钥，只有核对启动失败的用例使用（ADR-0089）
/// </remarks>
internal static class S3TestServer
{
    /// <summary>
    /// 指向测试服务端点的环境变量名
    /// </summary>
    public const string EnvironmentVariable = "XIHAN_TEST_S3";

    /// <summary>
    /// 指向不认条件写入的兼容服务端点的环境变量名
    /// </summary>
    public const string UnconditionalEnvironmentVariable = "XIHAN_TEST_S3_UNCONDITIONAL";

    /// <summary>
    /// 条件写入探测与健康项用的桶
    /// </summary>
    /// <remarks>
    /// 与一致性用例的容器分开，探测键不出现在用例写入的容器里
    /// </remarks>
    public const string ProbeBucket = "xihan-probe";

    /// <summary>
    /// 进程内共享的客户端，首次使用时创建
    /// </summary>
    private static readonly Lazy<AmazonS3Client> _client = new(() => S3ObjectStorageFeature.CreateClient(Options()));

    /// <summary>
    /// 已确认存在的桶（端点与桶名）
    /// </summary>
    private static readonly HashSet<string> _buckets = new(StringComparer.Ordinal);

    /// <summary>
    /// 保护 <see cref="_buckets"/>
    /// </summary>
    private static readonly Lock _gate = new();

    /// <summary>
    /// 环境变量里的服务端点
    /// </summary>
    /// <remarks>
    /// 未设置为 <see langword="null"/>
    /// </remarks>
    public static string? ServiceUrl => Environment.GetEnvironmentVariable(EnvironmentVariable);

    /// <summary>
    /// 访问密钥 ID
    /// </summary>
    public static string AccessKey => Environment.GetEnvironmentVariable("XIHAN_TEST_S3_ACCESS_KEY") is { Length: > 0 } key ? key : "rustfsadmin";

    /// <summary>
    /// 访问密钥
    /// </summary>
    public static string SecretKey => Environment.GetEnvironmentVariable("XIHAN_TEST_S3_SECRET_KEY") is { Length: > 0 } key ? key : "rustfsadmin";

    /// <summary>
    /// 返回服务端点
    /// </summary>
    /// <remarks>
    /// 未设置时跳过当前用例
    /// </remarks>
    public static string RequireServiceUrl()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ServiceUrl), $"Set the environment variable '{EnvironmentVariable}' to an S3-compatible endpoint to run this test.");
        return ServiceUrl!;
    }

    /// <summary>
    /// 返回不认条件写入的服务端点
    /// </summary>
    /// <remarks>
    /// 未设置时跳过当前用例
    /// </remarks>
    public static string RequireUnconditionalServiceUrl()
    {
        var url = Environment.GetEnvironmentVariable(UnconditionalEnvironmentVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(url), $"Set the environment variable '{UnconditionalEnvironmentVariable}' to an S3-compatible endpoint that ignores conditional writes to run this test.");
        return url!;
    }

    /// <summary>
    /// 指向测试服务的选项
    /// </summary>
    public static S3ObjectStorageOptions Options(bool ensureBuckets = false) => new()
    {
        ServiceUrl = RequireServiceUrl(),
        AccessKey = AccessKey,
        SecretKey = SecretKey,
        EnsureBuckets = ensureBuckets,
    };

    /// <summary>
    /// 宿主配置：<c>XiHan:ObjectStorage:S3</c> 节指向测试服务
    /// </summary>
    /// <remarks>
    /// 健康桶是 <see cref="ProbeBucket"/>（保证存在），启动时在其中探测条件写入
    /// </remarks>
    public static IEnumerable<KeyValuePair<string, string?>> Configuration()
    {
        EnsureBucket(Client(), ProbeBucket);
        return
        [
            new(S3ObjectStorageOptions.ConfigurationSection + ":ServiceUrl", RequireServiceUrl()),
            new(S3ObjectStorageOptions.ConfigurationSection + ":AccessKey", AccessKey),
            new(S3ObjectStorageOptions.ConfigurationSection + ":SecretKey", SecretKey),
            new(S3ObjectStorageOptions.ConfigurationSection + ":HealthBucket", ProbeBucket),
        ];
    }

    /// <summary>
    /// 共享客户端
    /// </summary>
    /// <remarks>
    /// 未设置端点时跳过当前用例
    /// </remarks>
    public static IAmazonS3 Client()
    {
        RequireServiceUrl();
        return _client.Value;
    }

    /// <summary>
    /// 创建指向测试服务的存储，并保证桶存在
    /// </summary>
    public static S3BlobStore CreateStore(string bucket)
    {
        var client = Client();
        EnsureBucket(client, bucket);
        return new S3BlobStore(client, Options(), TimeProvider.System);
    }

    /// <summary>
    /// 桶不存在就创建
    /// </summary>
    /// <remarks>
    /// 每个端点上的每个桶只检查一次
    /// </remarks>
    public static void EnsureBucket(IAmazonS3 client, string bucket)
    {
        var entry = client.Config.ServiceURL + "|" + bucket;
        lock (_gate)
        {
            if (_buckets.Contains(entry))
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

            _buckets.Add(entry);
        }
    }
}
