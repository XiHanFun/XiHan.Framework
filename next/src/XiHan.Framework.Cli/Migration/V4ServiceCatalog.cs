// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 第一遍扫描到的类：基类名、<c>[DynamicApi]</c> / <c>[Authorize]</c> / <c>[AllowAnonymous]</c> / 类级 <c>[PermissionAuthorize]</c>（V4-06），
/// 以及注册相关的事实：自己声明的存期（标记接口或 <c>[Dependency]</c>）、实现的接口、是否可注册（V4-03 沿基类链解析到 4.x 框架基类的存期）；
/// 都按基类链向上解析（4.x 的 <c>[DynamicApi]</c> 与注入标记常标在抽象基类上）。
/// </summary>
internal sealed class V4ServiceCatalog
{
    /// <summary>
    /// 存期的优先级，与 4.x <c>ConventionalRegistrarBase.GetServiceLifetimeFromClassHierarchy</c> 一致：Transient、Singleton、Scoped。
    /// </summary>
    private static readonly string[] _lifetimePriority = ["Transient", "Singleton", "Scoped"];

    /// <summary>
    /// 类名 → 条目；partial 类分散在多个文件时合并。
    /// </summary>
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// 登记一个类的动态 API 与授权事实（V4-06 的扫描）。
    /// </summary>
    public void Add(string name, IReadOnlyList<string> bases, bool dynamicApi, bool disabled, bool authorize, bool anonymous, string? permission)
    {
        var entry = Get(name);
        entry.Bases.UnionWith(bases);
        entry.DynamicApi |= dynamicApi;
        entry.Disabled |= disabled;
        entry.Authorize |= authorize;
        entry.Anonymous |= anonymous;
        entry.Permission ??= permission;
    }

    /// <summary>
    /// 登记一个类的注册事实（V4-03 的扫描）。
    /// </summary>
    /// <param name="name">类名。</param>
    /// <param name="bases">基类与接口的简单名。</param>
    /// <param name="ns">所在命名空间。</param>
    /// <param name="projectDirectory">所在项目目录；不在任何项目下为 <see langword="null"/>。</param>
    /// <param name="typeParameters">类型参数名；非泛型为空。</param>
    /// <param name="interfaces">基类列表里像接口的项的原文（含类型实参）。</param>
    /// <param name="abstractOrStatic">抽象或静态类。</param>
    /// <param name="declaredLifetime">自己声明的存期（<c>[Dependency]</c> 优先于标记接口）；没有为 <see langword="null"/>。</param>
    /// <param name="dependencyAttribute">自己标了 <c>[Dependency]</c>。</param>
    public void Describe(string name, IReadOnlyList<string> bases, string ns, string? projectDirectory, IReadOnlyList<string> typeParameters, IReadOnlyList<string> interfaces, bool abstractOrStatic, string? declaredLifetime, bool dependencyAttribute)
    {
        var entry = Get(name);
        entry.Bases.UnionWith(bases);
        entry.Described = true;
        entry.Namespace ??= ns;
        entry.ProjectDirectory ??= projectDirectory;
        entry.TypeParameters.UnionWith(typeParameters);
        foreach (var @interface in interfaces)
        {
            if (!entry.Interfaces.Contains(@interface, StringComparer.Ordinal))
            {
                entry.Interfaces.Add(@interface);
            }
        }

        entry.AbstractOrStatic |= abstractOrStatic;
        // [Dependency] 的存期优先；都是标记接口时按优先级取
        if (dependencyAttribute)
        {
            entry.DependencyAttribute = true;
            entry.DeclaredLifetime = declaredLifetime;
        }
        else if (declaredLifetime is not null && !entry.DependencyAttribute)
        {
            entry.DeclaredLifetime = Prefer(entry.DeclaredLifetime, declaredLifetime);
        }
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
    /// 自己没有声明存期、但沿基类链（迁移范围内的基类，直到 4.x 框架基类）继承到存期的具体类（非抽象、非静态、非泛型、在项目目录下），按名字排序。
    /// </summary>
    public IEnumerable<string> InheritedRegistrations()
        => _entries
            .Where(static pair => pair.Value is { Described: true, AbstractOrStatic: false, DeclaredLifetime: null, ProjectDirectory: not null } && pair.Value.TypeParameters.Count == 0)
            .Select(static pair => pair.Key)
            .Where(name => InheritedLifetime(name) is not null)
            .OrderBy(static name => name, StringComparer.Ordinal);

    /// <summary>
    /// 沿基类链解析存期：最近的 <c>[Dependency]</c> 优先，否则在全链（含 4.x 框架基类表）的标记里按 Transient、Singleton、Scoped 取；解析不到为 <see langword="null"/>。
    /// </summary>
    public string? InheritedLifetime(string name)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>([name]);
        string? marked = null;
        while (queue.TryDequeue(out var current))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            if (!_entries.TryGetValue(current, out var entry) || !entry.Described)
            {
                // 范围外的名字：是 4.x 框架基类就取表里的存期
                if (V4ServiceBases.TryGetLifetime(current, out var framework))
                {
                    marked = Prefer(marked, framework);
                }

                continue;
            }

            // 4.x 取类型层次上最近的 [Dependency]（GetCustomAttribute 带继承）
            if (entry.DependencyAttribute)
            {
                return entry.DeclaredLifetime;
            }

            marked = Prefer(marked, entry.DeclaredLifetime);
            foreach (var baseName in entry.Bases)
            {
                queue.Enqueue(baseName);
            }
        }

        return marked;
    }

    /// <summary>
    /// 命名空间、项目目录与默认暴露的服务（4.x <c>ExposeServicesAttribute.GetDefaultServices</c>：类名以「接口名去 I」结尾的接口，泛型接口按去反引号的名字比）；
    /// 接口取自己的基类列表，以及范围内基类上不含该基类类型参数的接口。
    /// </summary>
    public (string Namespace, string ProjectDirectory, IReadOnlyList<string> Services) Registration(string name)
    {
        var entry = _entries[name];
        var services = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>([name]);
        while (queue.TryDequeue(out var current))
        {
            if (!visited.Add(current) || !_entries.TryGetValue(current, out var ancestor) || !ancestor.Described)
            {
                continue;
            }

            foreach (var @interface in ancestor.Interfaces)
            {
                // 基类上的泛型接口若带着基类自己的类型参数，派生类这边不知道实参，跳过
                if (ancestor.TypeParameters.Any(parameter => Mentions(@interface, parameter)))
                {
                    continue;
                }

                var stem = Stem(@interface);
                if (stem.Length > 0 && name.EndsWith(stem, StringComparison.Ordinal) && !services.Contains(@interface, StringComparer.Ordinal))
                {
                    services.Add(@interface);
                }
            }

            foreach (var baseName in ancestor.Bases)
            {
                queue.Enqueue(baseName);
            }
        }

        return (entry.Namespace ?? string.Empty, entry.ProjectDirectory!, services);
    }

    /// <summary>
    /// 接口原文去掉类型实参与前导 <c>I</c>：<c>IRoleRepository</c> → <c>RoleRepository</c>，<c>IHandler&lt;SysRole&gt;</c> → <c>Handler</c>。
    /// </summary>
    private static string Stem(string @interface)
    {
        var angle = @interface.IndexOf('<');
        var simple = angle >= 0 ? @interface[..angle] : @interface;
        var dot = simple.LastIndexOf('.');
        simple = dot >= 0 ? simple[(dot + 1)..] : simple;
        return simple.Length > 1 && simple[0] == 'I' && char.IsUpper(simple[1]) ? simple[1..] : string.Empty;
    }

    /// <summary>
    /// 类型原文里是否以独立标识符出现 <paramref name="identifier"/>。
    /// </summary>
    private static bool Mentions(string type, string identifier)
    {
        var index = 0;
        while ((index = type.IndexOf(identifier, index, StringComparison.Ordinal)) >= 0)
        {
            var before = index == 0 || !IsIdentifierChar(type[index - 1]);
            var after = index + identifier.Length == type.Length || !IsIdentifierChar(type[index + identifier.Length]);
            if (before && after)
            {
                return true;
            }

            index += identifier.Length;
        }

        return false;
    }

    /// <summary>
    /// 标识符字符。
    /// </summary>
    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>
    /// 两个存期里优先级高的（<see langword="null"/> 最低）。
    /// </summary>
    private static string? Prefer(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return left ?? right;
        }

        return Array.IndexOf(_lifetimePriority, right) < Array.IndexOf(_lifetimePriority, left) ? right : left;
    }

    /// <summary>
    /// 取或建条目。
    /// </summary>
    private Entry Get(string name)
    {
        if (!_entries.TryGetValue(name, out var entry))
        {
            entry = new Entry();
            _entries[name] = entry;
        }

        return entry;
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

        /// <summary>V4-03 的扫描登记过注册事实。</summary>
        public bool Described { get; set; }

        /// <summary>所在命名空间。</summary>
        public string? Namespace { get; set; }

        /// <summary>所在项目目录。</summary>
        public string? ProjectDirectory { get; set; }

        /// <summary>类型参数名。</summary>
        public HashSet<string> TypeParameters { get; } = new(StringComparer.Ordinal);

        /// <summary>基类列表里像接口的项的原文。</summary>
        public List<string> Interfaces { get; } = [];

        /// <summary>抽象或静态类。</summary>
        public bool AbstractOrStatic { get; set; }

        /// <summary>自己声明的存期；没有为 <see langword="null"/>。</summary>
        public string? DeclaredLifetime { get; set; }

        /// <summary>自己标了 <c>[Dependency]</c>。</summary>
        public bool DependencyAttribute { get; set; }
    }
}
