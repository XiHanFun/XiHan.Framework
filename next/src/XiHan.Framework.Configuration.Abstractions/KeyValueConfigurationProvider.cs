// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace XiHan.Framework.Configuration.Abstractions;

/// <summary>
/// 以一份可整体替换的键值表实现 <see cref="IConfigurationProvider"/>。
/// </summary>
/// <remarks>
/// 替换时触发重载令牌。本包只能引用抽象包，所以不派生自 <c>ConfigurationProvider</c>。
/// </remarks>
internal abstract class KeyValueConfigurationProvider : IConfigurationProvider, IDisposable
{
    /// <summary>
    /// 保护键值表与令牌的锁。
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 当前键值表。
    /// </summary>
    private Dictionary<string, string?> _data = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 当前重载令牌的源。
    /// </summary>
    /// <remarks>
    /// 替换数据时取消并换新。
    /// </remarks>
    private CancellationTokenSource _reload = new();

    /// <inheritdoc />
    public abstract void Load();

    /// <inheritdoc />
    public bool TryGet(string key, out string? value)
    {
        lock (_lock)
        {
            return _data.TryGetValue(key, out value);
        }
    }

    /// <inheritdoc />
    public void Set(string key, string? value)
    {
        lock (_lock)
        {
            _data[key] = value;
        }
    }

    /// <inheritdoc />
    public IChangeToken GetReloadToken()
    {
        lock (_lock)
        {
            return new CancellationChangeToken(_reload.Token);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string? parentPath)
    {
        var results = new List<string>();
        lock (_lock)
        {
            foreach (var key in _data.Keys)
            {
                if (parentPath is null)
                {
                    results.Add(Segment(key, 0));
                }
                else if (key.Length > parentPath.Length && key.StartsWith(parentPath, StringComparison.OrdinalIgnoreCase) && key[parentPath.Length] == ':')
                {
                    results.Add(Segment(key, parentPath.Length + 1));
                }
            }
        }

        results.AddRange(earlierKeys);
        results.Sort(ConfigurationKeyOrder.Instance);
        return results;
    }

    /// <summary>
    /// 释放：派生类先停自己的后台工作再调用基类。
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        lock (_lock)
        {
            _reload.Dispose();
        }
    }

    /// <summary>
    /// 当前键值表的副本。
    /// </summary>
    protected Dictionary<string, string?> Snapshot()
    {
        lock (_lock)
        {
            return new Dictionary<string, string?>(_data, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 整体替换键值表。
    /// </summary>
    /// <remarks>
    /// <paramref name="notify"/> 为真且键值有变化时触发重载令牌（首次加载不触发）；重读到同样的内容不触发，订阅者不会因为轮询而反复重绑。
    /// </remarks>
    protected void Replace(IReadOnlyDictionary<string, string?> values, bool notify)
    {
        CancellationTokenSource previous;
        lock (_lock)
        {
            var changed = !SameValues(_data, values);
            _data = new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase);
            if (!notify || !changed)
            {
                return;
            }

            // 先换上新令牌再取消旧令牌：订阅者在回调里重新取令牌时拿到的已是新的
            previous = _reload;
            _reload = new CancellationTokenSource();
        }

        previous.Cancel();
        previous.Dispose();
    }

    /// <summary>
    /// 两份键值表是否相同。
    /// </summary>
    /// <remarks>
    /// 键不分大小写，值按序数比较。
    /// </remarks>
    private static bool SameValues(Dictionary<string, string?> current, IReadOnlyDictionary<string, string?> next)
    {
        if (current.Count != next.Count)
        {
            return false;
        }

        foreach (var (key, value) in next)
        {
            if (!current.TryGetValue(key, out var existing) || !string.Equals(existing, value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 键从 <paramref name="start"/> 起到下一个分隔符之前的一段。
    /// </summary>
    private static string Segment(string key, int start)
    {
        var end = key.IndexOf(':', start);
        return end < 0 ? key[start..] : key[start..end];
    }
}
