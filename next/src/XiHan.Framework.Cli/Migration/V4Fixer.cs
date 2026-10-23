// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 一条迁移规则（<c>spec/migration/v4.md</c>）：按路径决定是否处理，对文本施加改写并把结果记进会话。
/// </summary>
internal abstract class V4Fixer
{
    /// <summary>
    /// 规则编号，如 <c>V4-05</c>。
    /// </summary>
    public abstract string Id { get; }

    /// <summary>
    /// 是否处理该文件。
    /// </summary>
    public abstract bool Handles(MigrationFile file);

    /// <summary>
    /// 第一遍：在改写任何文件之前看一遍原文，收集跨文件事实（如基类上的特性）；默认什么都不做。
    /// </summary>
    public virtual void Scan(MigrationFile file, string text, MigrationSession session)
    {
    }

    /// <summary>
    /// 施加改写，返回新文本；没有改动时返回原文本的同一实例。
    /// </summary>
    public abstract string Apply(MigrationFile file, string text, MigrationSession session);
}
