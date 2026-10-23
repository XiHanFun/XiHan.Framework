// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 第一遍扫描到的类：基类名与 <c>[DynamicApi]</c> / <c>[Authorize]</c> / <c>[AllowAnonymous]</c> / 类级 <c>[PermissionAuthorize]</c>，按基类链向上解析（4.x 的 <c>[DynamicApi]</c> 常标在抽象基类上）。
/// </summary>
internal sealed class V4ServiceCatalog
{
    /// <summary>
    /// 类名 → 条目；partial 类分散在多个文件时合并。
    /// </summary>
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// 登记一个类。
    /// </summary>
    public void Add(string name, IReadOnlyList<string> bases, bool dynamicApi, bool disabled, bool authorize, bool anonymous, string? permission)
    {
        if (!_entries.TryGetValue(name, out var entry))
        {
            entry = new Entry();
            _entries[name] = entry;
        }

        entry.Bases.UnionWith(bases);
        entry.DynamicApi |= dynamicApi;
        entry.Disabled |= disabled;
        entry.Authorize |= authorize;
        entry.Anonymous |= anonymous;
        entry.Permission ??= permission;
    }

    /// <summary>
    /// 自己或某个基类标了 <c>[DynamicApi]</c>，且自己没有 <c>[DynamicApi(false)]</c>。
    /// </summary>
    public bool IsDynamicApi(string name) => Resolve(name, static entry => entry.DynamicApi, static entry => entry.Disabled, []) == true;

    /// <summary>
    /// 自己或某个基类标了 <c>[Authorize]</c>（含 <c>[PermissionAuthorize]</c>）。
    /// </summary>
    public bool IsAuthorized(string name) => Resolve(name, static entry => entry.Authorize, static _ => false, []) == true;

    /// <summary>
    /// 自己或某个基类标了 <c>[AllowAnonymous]</c>。
    /// </summary>
    public bool IsAnonymous(string name) => Resolve(name, static entry => entry.Anonymous, static _ => false, []) == true;

    /// <summary>
    /// 基类链上最近的类级权限表达式。
    /// </summary>
    public string? Permission(string name)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>([name]);
        while (queue.TryDequeue(out var current))
        {
            if (!visited.Add(current) || !_entries.TryGetValue(current, out var entry))
            {
                continue;
            }

            if (entry.Permission is not null)
            {
                return entry.Permission;
            }

            foreach (var baseName in entry.Bases)
            {
                queue.Enqueue(baseName);
            }
        }

        return null;
    }

    /// <summary>
    /// 沿基类链找标志：<paramref name="stop"/> 成立的类切断继承；没登记的名字视为无。
    /// </summary>
    private bool? Resolve(string name, Func<Entry, bool> flag, Func<Entry, bool> stop, HashSet<string> visited)
    {
        if (!visited.Add(name) || !_entries.TryGetValue(name, out var entry))
        {
            return null;
        }

        if (stop(entry))
        {
            return false;
        }

        if (flag(entry))
        {
            return true;
        }

        foreach (var baseName in entry.Bases)
        {
            if (Resolve(baseName, flag, stop, visited) == true)
            {
                return true;
            }
        }

        return null;
    }

    /// <summary>
    /// 一个类的事实。
    /// </summary>
    private sealed class Entry
    {
        /// <summary>基类与接口的简单名。</summary>
        public HashSet<string> Bases { get; } = new(StringComparer.Ordinal);

        /// <summary>标了 <c>[DynamicApi]</c>。</summary>
        public bool DynamicApi { get; set; }

        /// <summary>标了 <c>[DynamicApi(false)]</c>。</summary>
        public bool Disabled { get; set; }

        /// <summary>标了 <c>[Authorize]</c> 或 <c>[PermissionAuthorize]</c>。</summary>
        public bool Authorize { get; set; }

        /// <summary>标了 <c>[AllowAnonymous]</c>。</summary>
        public bool Anonymous { get; set; }

        /// <summary>类级 <c>[PermissionAuthorize]</c> 的第一个参数表达式。</summary>
        public string? Permission { get; set; }
    }
}
