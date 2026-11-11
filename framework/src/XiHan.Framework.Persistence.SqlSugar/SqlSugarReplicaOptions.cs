// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 从库的权重与健康探测选项
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Persistence:SqlSugar:Replicas</c> 读取。从库本身由连接配置回调设在 <c>ConnectionConfig.SlaveConnectionConfigs</c> 上（RFC-0005「读写分离」），这里不列从库；没配从库时这些选项都不起作用
/// </remarks>
[ApiLevel(Stability.Experimental, "5.0")]
public sealed record SqlSugarReplicaOptions
{
    /// <summary>
    /// <see cref="DefaultHitRate"/> 的缺省值
    /// </summary>
    public const int StandardHitRate = 10;

    /// <summary>
    /// 权重不大于 0 的从库归一化成的权重
    /// </summary>
    /// <remarks>
    /// 默认 <see cref="StandardHitRate"/>。SqlSugar 的 <c>HitRate</c> 是字段，配置绑定绑不上它；权重全为 0 时它的读路由直接抛异常
    /// </remarks>
    public int DefaultHitRate
    {
        get => field;
        init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The default hit rate must be positive.");
    } = StandardHitRate;

    /// <summary>
    /// 是否周期探测从库
    /// </summary>
    /// <remarks>
    /// 默认 <see langword="true"/>：配了从库才有探测，不探测时从库宕机，落到它的读就失败。关掉后权重不再变化，健康快照里也没有 <see cref="SqlSugarPersistenceFeature.ReplicaHealthName"/>
    /// </remarks>
    public bool HealthCheck { get; init; } = true;

    /// <summary>
    /// 探测间隔
    /// </summary>
    /// <remarks>
    /// 默认 30 秒
    /// </remarks>
    public TimeSpan HealthCheckInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The replica health check interval must be positive.");
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 故障冷却窗口
    /// </summary>
    /// <remarks>
    /// 默认 120 秒：从库最后一次探测失败之后，要过了这段时间并且探测成功才恢复读流量，避免抖动。可以为 0
    /// </remarks>
    public TimeSpan FailureCooldown
    {
        get => field;
        init => field = value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The replica failure cooldown must not be negative.");
    } = TimeSpan.FromSeconds(120);
}
