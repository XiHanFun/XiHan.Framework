// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 一种绑定类型在 Compose 与 Aspire 里的对应。
/// </summary>
/// <param name="Type">清单里的类型。</param>
/// <param name="Image">Compose 镜像。</param>
/// <param name="Port">服务端口。</param>
internal sealed record BindingKind(string Type, string Image, int Port)
{
    /// <summary>
    /// 全部已知类型。
    /// </summary>
    /// <remarks>
    /// 与框架 Provider 清单声明的绑定类型一一对应（<c>spec/packaging/app-manifest.md</c>）：持久化 <c>connection:relational</c>、<c>Caching.Redis</c> <c>cache:distributed</c>、<c>Messaging.RabbitMQ</c> <c>queue</c>、<c>ObjectStorage.S3</c> <c>blob</c>、<c>Messaging.Kafka</c> <c>stream</c>、<c>Configuration.Nacos</c> <c>configuration:remote</c>。
    /// </remarks>
    public static IReadOnlyList<BindingKind> All { get; } =
    [
        new("connection:relational", "postgres:17-alpine", 5432),
        new("cache:distributed", "redis:7-alpine", 6379),
        new("queue", "rabbitmq:4-alpine", 5672),
        new("blob", "minio/minio", 9000),
        new("stream", "apache/kafka:4.1.0", 9092),
        new("configuration:remote", "nacos/nacos-server:v2.5.1", 8848),
    ];

    /// <summary>
    /// 按类型找。
    /// </summary>
    public static BindingKind? Find(string type) => All.FirstOrDefault(k => string.Equals(k.Type, type, StringComparison.Ordinal));

    /// <summary>
    /// Compose 里注入给应用的主值（服务名即主机名）。
    /// </summary>
    /// <remarks>
    /// 形式与各 Provider 读取绑定主值的方式一致：Kafka 是引导地址 <c>host:port</c>，Nacos 是服务地址 URL（路径缺省为上下文路径 <c>/nacos</c>）。
    /// </remarks>
    public string ComposeValue(string binding, string app) => Type switch
    {
        "connection:relational" => $"Host={binding};Port={Port};Database={app};Username=xihan;Password=xihan",
        "cache:distributed" or "stream" => $"{binding}:{Port}",
        "queue" => $"amqp://guest:guest@{binding}:{Port}",
        _ => $"http://{binding}:{Port}",
    };

    /// <summary>
    /// 本地联调的 MinIO 根用户名。
    /// </summary>
    /// <remarks>
    /// Compose 与 Aspire 以它启动 MinIO，并作为 <c>blob</c> 绑定的属性 <c>accessKey</c> 注入应用（RFC-0016「资源绑定与配置节」）。只用于本地联调。
    /// </remarks>
    public const string MinioUser = "xihan";

    /// <summary>
    /// 本地联调的 MinIO 根密码。
    /// </summary>
    /// <remarks>
    /// 作为 <c>blob</c> 绑定的属性 <c>secretKey</c> 注入应用；MinIO 要求至少 8 个字符。只用于本地联调。
    /// </remarks>
    public const string MinioPassword = "xihanxihan";
}
