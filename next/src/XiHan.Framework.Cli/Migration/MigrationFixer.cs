// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 一条迁移规则（<c>spec/migration/README.md</c>）。
/// </summary>
/// <remarks>
/// 按路径决定是否处理，对文本施加改写并把结果记进会话。
/// </remarks>
internal abstract class MigrationFixer
{
    /// <summary>
    /// 规则编号，如 <c>V5-01</c>。
    /// </summary>
    /// <remarks>
    /// 同一编号可以由多个规则实例承担，报告按编号合并。
    /// </remarks>
    public abstract string Id { get; }

    /// <summary>
    /// 是否处理该文件。
    /// </summary>
    /// <remarks>
    /// 只看路径，此时文件还没读入。
    /// </remarks>
    public abstract bool Handles(MigrationFile file);

    /// <summary>
    /// 第一遍。
    /// </summary>
    /// <remarks>
    /// 在改写任何文件之前看一遍原文，收集跨文件事实；默认什么都不做。
    /// </remarks>
    public virtual void Scan(MigrationFile file, string text, MigrationSession session)
    {
    }

    /// <summary>
    /// 扫描完全部文件、改写任何文件之前调用一次。
    /// </summary>
    /// <remarks>
    /// 按跨文件事实做决定；默认什么都不做。
    /// </remarks>
    public virtual void Prepare(MigrationSession session)
    {
    }

    /// <summary>
    /// 施加改写，返回新文本。
    /// </summary>
    /// <remarks>
    /// 没有改动时返回原文本的同一实例。
    /// </remarks>
    public abstract string Apply(MigrationFile file, string text, MigrationSession session);
}
