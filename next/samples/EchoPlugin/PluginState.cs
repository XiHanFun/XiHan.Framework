// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace EchoPlugin;

/// <summary>
/// 插件进程内的状态：清单、配置、授予的权限、收到的事件与健康状态变化。
/// </summary>
internal sealed class PluginState
{
    /// <summary>
    /// 收到的事件 ID（按投递顺序）。
    /// </summary>
    private readonly ConcurrentQueue<string> _events = new();

    /// <summary>
    /// 健康状态变化的订阅者。
    /// </summary>
    private readonly ConcurrentDictionary<Guid, Func<Task>> _watchers = new();

    /// <summary>
    /// 清单文件的 JSON 文本（与宿主核对）。
    /// </summary>
    public string ManifestJson { get; } = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "xihan.manifest.json"));

    /// <summary>
    /// 宿主下发的配置节（<c>XiHan:Echo</c>）。
    /// </summary>
    public JsonObject Configuration { get; private set; } = [];

    /// <summary>
    /// 宿主授予的权限。
    /// </summary>
    public IReadOnlyList<string> GrantedPermissions { get; private set; } = [];

    /// <summary>
    /// 当前自报的生命周期状态（协议枚举值）。
    /// </summary>
    public int State { get; private set; } = 3;

    /// <summary>
    /// 收到的事件 ID 快照。
    /// </summary>
    public IReadOnlyList<string> Events => _events.ToArray();

    /// <summary>
    /// 采纳 Configure。
    /// </summary>
    public void Configure(string configurationJson, IEnumerable<string> granted)
    {
        Configuration = JsonNode.Parse(string.IsNullOrWhiteSpace(configurationJson) ? "{}" : configurationJson) as JsonObject ?? [];
        GrantedPermissions = granted.ToList();
    }

    /// <summary>
    /// 记一条收到的事件。
    /// </summary>
    public void Record(string eventId) => _events.Enqueue(eventId);

    /// <summary>
    /// 进入某状态并通知健康订阅者。
    /// </summary>
    public async Task TransitionAsync(int state)
    {
        State = state;
        foreach (var watcher in _watchers.Values)
        {
            await watcher().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 订阅状态变化。
    /// </summary>
    /// <remarks>
    /// 返回取消订阅的句柄。
    /// </remarks>
    public IDisposable Watch(Func<Task> onChange)
    {
        var id = Guid.NewGuid();
        _watchers[id] = onChange;
        return new Subscription(() => _watchers.TryRemove(id, out _));
    }

    /// <summary>
    /// 配置里的问候语。
    /// </summary>
    /// <remarks>
    /// 没有配置时为空。
    /// </remarks>
    public string? Greeting => Configuration[nameof(Greeting)]?.GetValue<string>();

    /// <summary>
    /// 取消订阅的句柄。
    /// </summary>
    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
