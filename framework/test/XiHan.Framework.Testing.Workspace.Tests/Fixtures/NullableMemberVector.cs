// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Testing.Workspace.Tests.Fixtures;

/// <summary>
/// 带可空成员与不可空成员的向量模型
/// </summary>
/// <param name="Name">不可空的构造参数</param>
/// <param name="Note">可空的构造参数，文件里要出现、可以是 <see langword="null"/></param>
internal sealed record NullableMemberVector(string Name, string? Note)
{
    /// <summary>
    /// 可空、可以不出现的成员
    /// </summary>
    public string? Tag { get; init; }
}
