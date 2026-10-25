// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions.Remote;

/// <summary>
/// 远程配置源的选项。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record RemoteConfigurationOptions
{
    /// <summary>
    /// 创建。
    /// </summary>
    public RemoteConfigurationOptions(string @namespace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(@namespace);
        Namespace = @namespace;
    }

    /// <summary>
    /// 命名空间。
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// 把文档挂到的配置节（如 <c>Remote</c>），为空时挂在根。
    /// </summary>
    public string? Prefix { get; init; }

    /// <summary>
    /// 轮询周期；默认 30 秒，<see langword="null"/> 关闭轮询。
    /// </summary>
    public TimeSpan? PollInterval
    {
        get => field;
        init => field = value is null || value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The poll interval must be positive.");
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 构建配置时首次加载的超时；默认 10 秒。
    /// </summary>
    public TimeSpan LoadTimeout
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The load timeout must be positive.");
    } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 首次加载失败时是否继续（为空）；默认 <see langword="false"/>：抛出让启动失败。
    /// </summary>
    public bool Optional { get; init; }

    /// <summary>
    /// 加载或轮询失败时的回调（配置在日志之前构建，这里是唯一的报告渠道）。
    /// </summary>
    public Action<Error>? OnError { get; init; }
}
