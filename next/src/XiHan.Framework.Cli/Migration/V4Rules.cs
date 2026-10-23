// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 全部规则，按执行顺序（<c>spec/migration/v4.md</c> §1.4：后一条看到的是前一条改过的文件）。
/// </summary>
internal static class V4Rules
{
    /// <summary>
    /// 执行顺序的规则实例；规则无状态，结果都记在会话里。
    /// </summary>
    public static IReadOnlyList<V4Fixer> All { get; } =
    [
        new ModuleToFeatureFixer(),
        new EntityProtocolFixer(),
        new DependencyMarkerFixer(),
        new ClockFixer(),
        new UnitOfWorkFixer(),
        new DynamicApiFixer(),
        new CurrentContextFixer(),
        new LocalEventFixer(),
        new LocalizationResourceFixer(),
        new PackageReferenceFixer(),
        new LegacyUsingFixer(),
    ];

    /// <summary>
    /// 规则编号，去重后按执行顺序。
    /// </summary>
    public static IReadOnlyList<string> Ids { get; } = All.Select(static fixer => fixer.Id).Distinct(StringComparer.Ordinal).ToList();
}
