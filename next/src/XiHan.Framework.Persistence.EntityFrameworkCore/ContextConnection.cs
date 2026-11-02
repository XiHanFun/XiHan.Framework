// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// 本作用域的 DbContext 建在哪个连接上（作用域服务）。
/// </summary>
/// <remarks>
/// 一个 DbContext 一个连接，建出之后不能再换。绑定命名连接的工作单元在开始时记下连接名（<see cref="Bind"/>），随后建 DbContext 选项的回调（<see cref="Build"/>）据此把命名连接的绑定交给应用的配置回调；没有绑定的作用域按缺省建——带租户分片的调用是分片连接，其余是默认连接。
/// </remarks>
internal sealed class ContextConnection
{
    /// <summary>
    /// 绑定的命名连接。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 表示缺省：带租户分片的调用是分片连接，其余是默认连接。
    /// </remarks>
    public string? Name { get; private set; }

    /// <summary>
    /// 本作用域的 DbContext 是否已经建出。
    /// </summary>
    public bool IsBuilt { get; private set; }

    /// <summary>
    /// 本作用域的 DbContext 建在哪个租户分片上。
    /// </summary>
    /// <remarks>
    /// 建在分片上时是那个分片的连接绑定（主值是分片连接串，属性带 <c>tenant</c>），否则为 <see langword="null"/>。工作单元开始时据此决定要不要核对分片库（<see cref="ShardTimestampChecks"/>）。
    /// </remarks>
    public BindingValue? Shard { get; private set; }

    /// <summary>
    /// 绑定命名连接。
    /// </summary>
    /// <remarks>
    /// 必须在本作用域建出 DbContext 之前：已经按别的连接建出时抛出携带 <see cref="PersistenceErrors.InvalidUnitOfWorkState"/> 的 <see cref="FrameworkException"/>。
    /// </remarks>
    public void Bind(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        // 已建出的 DbContext 换不了连接：静默沿用会让绑定的工作单元读写到别的库，所以要求在新作用域里开绑定连接的工作单元
        if (IsBuilt && !string.Equals(Name, name, StringComparison.Ordinal))
        {
            throw new FrameworkException(PersistenceErrors.InvalidUnitOfWorkState, $"The DbContext of this scope was already created on {(Name is null ? "the default connection" : $"the connection '{Name}'")}, so a unit of work cannot be bound to the connection '{name}' here; begin it in a new scope.");
        }

        Name = name;
    }

    /// <summary>
    /// 建 DbContext 时调用：记下已经建出，返回它所在的命名连接（<see langword="null"/> 为缺省）。
    /// </summary>
    public string? Build()
    {
        IsBuilt = true;
        return Name;
    }

    /// <summary>
    /// 建 DbContext 时记下它建在租户分片上。
    /// </summary>
    public void BuiltOnShard(BindingValue shard) => Shard = shard ?? throw new ArgumentNullException(nameof(shard));
}
