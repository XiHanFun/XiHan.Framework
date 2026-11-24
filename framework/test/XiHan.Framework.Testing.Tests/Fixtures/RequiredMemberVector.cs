// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Testing.Tests.Fixtures;

/// <summary>
/// 带 <see langword="required"/> 成员与可缺省成员的向量模型
/// </summary>
internal sealed record RequiredMemberVector
{
    /// <summary>
    /// 必须出现在文件里的成员
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 可以不出现的成员
    /// </summary>
    public int Count { get; init; }
}
