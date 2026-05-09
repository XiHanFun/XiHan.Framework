// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// 管道执行上下文。不绑定 HTTP，可用于消息、后台任务等任何场景。
/// </summary>
[ApiLevel(Stability.Stable, "1.0")]
public class PipelineContext
{
    private readonly Dictionary<string, object?> _items = [];

    /// <summary>
    /// 用户标识。
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// 租户标识。
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// 追踪标识，用于分布式追踪。
    /// </summary>
    public string TraceId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 区域/语言。
    /// </summary>
    public string? Culture { get; set; }

    /// <summary>
    /// 请求标识。
    /// </summary>
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 扩展数据字典。
    /// </summary>
    public IDictionary<string, object?> Items => _items;

    /// <summary>
    /// 获取或设置一个扩展值。
    /// </summary>
    public object? Get(string key) => _items.TryGetValue(key, out var value) ? value : null;

    /// <summary>
    /// 设置一个扩展值。
    /// </summary>
    public void Set(string key, object? value) => _items[key] = value;
}
