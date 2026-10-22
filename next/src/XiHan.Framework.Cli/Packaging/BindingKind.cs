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
    public static IReadOnlyList<BindingKind> All { get; } =
    [
        new("connection:relational", "postgres:17-alpine", 5432),
        new("cache:distributed", "redis:7-alpine", 6379),
        new("queue", "rabbitmq:4-alpine", 5672),
        new("blob", "minio/minio", 9000),
    ];

    /// <summary>
    /// 按类型找。
    /// </summary>
    public static BindingKind? Find(string type) => All.FirstOrDefault(k => string.Equals(k.Type, type, StringComparison.Ordinal));

    /// <summary>
    /// Compose 里注入给应用的主值（服务名即主机名）。
    /// </summary>
    public string ComposeValue(string binding, string app) => Type switch
    {
        "connection:relational" => $"Host={binding};Port={Port};Database={app};Username=xihan;Password=xihan",
        "cache:distributed" => $"{binding}:{Port}",
        "queue" => $"amqp://guest:guest@{binding}:{Port}",
        _ => $"http://{binding}:{Port}",
    };
}
