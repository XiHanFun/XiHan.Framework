// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Plugins.OutOfProcess.Runtime;

/// <summary>
/// 宿主正交给一个插件处理的调用的租户
/// </summary>
/// <remarks>
/// 进程外插件并发处理调用，回调不指明它属于哪次调用：回调能带的租户是此刻在途的某个调用的租户（ADR-0195）。按租户计数，计数归零即移除，条目数不超过在途调用数
/// </remarks>
internal sealed class PluginTenantCalls
{
    /// <summary>
    /// 每个租户的在途调用数
    /// </summary>
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

    /// <summary>
    /// 保护计数的锁
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 登记一次在途调用，释放返回值时撤销
    /// </summary>
    /// <remarks>
    /// 没有租户的调用不登记：它的回调只能不带租户
    /// </remarks>
    public Entry Enter(string? tenant)
    {
        if (tenant is null)
        {
            return default;
        }

        lock (_gate)
        {
            _counts[tenant] = _counts.TryGetValue(tenant, out var count) ? count + 1 : 1;
        }

        return new Entry(this, tenant);
    }

    /// <summary>
    /// 是不是某个在途调用的租户
    /// </summary>
    public bool Contains(string tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        lock (_gate)
        {
            return _counts.ContainsKey(tenant);
        }
    }

    /// <summary>
    /// 撤销一次在途调用
    /// </summary>
    private void Exit(string tenant)
    {
        lock (_gate)
        {
            // 计数归零即移除：调用结束后插件不能再以这个租户回调，字典也不随租户数增长
            if (_counts.TryGetValue(tenant, out var count) && count > 1)
            {
                _counts[tenant] = count - 1;
            }
            else
            {
                _counts.Remove(tenant);
            }
        }
    }

    /// <summary>
    /// 一次在途调用的登记
    /// </summary>
    /// <remarks>
    /// 只释放一次；缺省值（没有租户的调用）释放无操作
    /// </remarks>
    public readonly struct Entry : IDisposable
    {
        /// <summary>
        /// 所属的登记表
        /// </summary>
        private readonly PluginTenantCalls? _owner;

        /// <summary>
        /// 登记的租户
        /// </summary>
        private readonly string? _tenant;

        /// <summary>
        /// 创建一次登记
        /// </summary>
        internal Entry(PluginTenantCalls owner, string tenant)
        {
            _owner = owner;
            _tenant = tenant;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_owner is not null && _tenant is not null)
            {
                _owner.Exit(_tenant);
            }
        }
    }
}
