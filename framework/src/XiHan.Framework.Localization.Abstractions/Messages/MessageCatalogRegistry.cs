// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Localization.Abstractions.Messages;

/// <summary>
/// 本地化器读取的消息目录
/// </summary>
/// <remarks>
/// 先是注册的目录（同名的已合并为一份），再是按归属登记的目录，按登记顺序；同一键先找到的优先，所以按归属登记的目录不覆盖已有的键。按归属登记的目录在配置阶段经 <see cref="LocalizationServiceCollectionExtensions.AddMessageCatalog(Microsoft.Extensions.DependencyInjection.IServiceCollection, string, MessageCatalog)"/>、在运行期经 <see cref="Add"/> 登记，经 <see cref="RemoveOwnedBy"/> 按归属一次撤回；插件宿主以插件的清单 ID 为归属（RFC-0010「归属目录」，ADR-0252）。容器里是单例，<c>AddMessageCatalog</c> 一并登记
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class MessageCatalogRegistry
{
    /// <summary>
    /// 注册的目录，按注册顺序
    /// </summary>
    private readonly MessageCatalog[] _registered;

    /// <summary>
    /// 按归属登记的目录，按登记顺序
    /// </summary>
    private readonly List<OwnedMessageCatalog> _owned = [];

    /// <summary>
    /// 串行化登记与撤回
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 当前快照：注册的在前，按归属登记的在后
    /// </summary>
    /// <remarks>
    /// 整份替换，读取不取锁
    /// </remarks>
    private volatile MessageCatalog[] _catalogs;

    /// <summary>
    /// 用注册的目录创建
    /// </summary>
    /// <param name="catalogs">注册的目录，按注册顺序</param>
    public MessageCatalogRegistry(IEnumerable<MessageCatalog> catalogs)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        _registered = [.. catalogs];
        foreach (var catalog in _registered)
        {
            ArgumentNullException.ThrowIfNull(catalog, nameof(catalogs));
        }

        _catalogs = _registered;
    }

    /// <summary>
    /// 用注册的目录与配置阶段按归属注册的目录创建
    /// </summary>
    /// <remarks>
    /// 按归属成组登记，规则同 <see cref="Add"/>：冲突抛出携带 <see cref="LocalizationErrors.DuplicateCatalog"/> 的 <see cref="FrameworkException"/>，应用启动失败
    /// </remarks>
    /// <param name="catalogs">注册的目录，按注册顺序</param>
    /// <param name="owned">按归属注册的目录，按注册顺序</param>
    internal MessageCatalogRegistry(IEnumerable<MessageCatalog> catalogs, IEnumerable<OwnedMessageCatalog> owned)
        : this(catalogs)
    {
        ArgumentNullException.ThrowIfNull(owned);
        foreach (var group in owned.GroupBy(static o => o.Owner, StringComparer.Ordinal))
        {
            Add(group.Key, [.. group.Select(static o => o.Catalog)]);
        }
    }

    /// <summary>
    /// 当前的全部目录
    /// </summary>
    /// <remarks>
    /// 注册的在前，按归属登记的在后；取到的是快照，此后的登记与撤回不影响它
    /// </remarks>
    public IReadOnlyList<MessageCatalog> Catalogs => _catalogs;

    /// <summary>
    /// 以一个归属登记一组目录
    /// </summary>
    /// <remarks>
    /// 全进或全不进：与已有的同名目录或同一组里的同名目录在同一键同一区域的模板不同，整组不登记并抛出携带 <see cref="LocalizationErrors.DuplicateCatalog"/> 的 <see cref="FrameworkException"/>。同名目录只核对、不合并成一份，撤回时各自离开。空的一组什么都不做；同一归属已有目录时抛出 <see cref="InvalidOperationException"/>，要先 <see cref="RemoveOwnedBy"/>
    /// </remarks>
    /// <param name="owner">归属</param>
    /// <param name="catalogs">目录</param>
    public void Add(string owner, IReadOnlyCollection<MessageCatalog> catalogs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentNullException.ThrowIfNull(catalogs);
        foreach (var catalog in catalogs)
        {
            ArgumentNullException.ThrowIfNull(catalog, nameof(catalogs));
        }

        if (catalogs.Count == 0)
        {
            return;
        }

        lock (_gate)
        {
            if (_owned.Exists(o => string.Equals(o.Owner, owner, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"Owner '{owner}' already has message catalogs registered; remove them before adding again.");
            }

            // 先全部核对、再一次写入：同一组里后面的目录冲突时，前面的也不留下
            var next = new List<MessageCatalog>(_catalogs.Length + catalogs.Count);
            next.AddRange(_catalogs);
            foreach (var catalog in catalogs)
            {
                foreach (var existing in next)
                {
                    if (string.Equals(existing.Name, catalog.Name, StringComparison.Ordinal) && existing.FindConflict(catalog) is { } conflict)
                    {
                        throw new FrameworkException(LocalizationErrors.DuplicateCatalog, $"Message catalog '{catalog.Name}' of owner '{owner}' has a different template for key '{conflict.Key}' in locale '{conflict.Locale}' than a registered catalog of the same name.");
                    }
                }

                next.Add(catalog);
            }

            foreach (var catalog in catalogs)
            {
                _owned.Add(new OwnedMessageCatalog(owner, catalog));
            }

            _catalogs = [.. next];
        }
    }

    /// <summary>
    /// 撤回一个归属登记的全部目录
    /// </summary>
    /// <remarks>
    /// 没有登记过时什么都不做。撤回之后，只在这些目录里有的键解析不到
    /// </remarks>
    /// <param name="owner">归属</param>
    /// <returns>撤回的目录数</returns>
    public int RemoveOwnedBy(string owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        lock (_gate)
        {
            var removed = _owned.RemoveAll(o => string.Equals(o.Owner, owner, StringComparison.Ordinal));
            if (removed > 0)
            {
                _catalogs = [.. _registered, .. _owned.Select(static o => o.Catalog)];
            }

            return removed;
        }
    }
}
