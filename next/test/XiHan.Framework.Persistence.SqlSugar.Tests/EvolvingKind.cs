// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 演进用例的枚举：没有零值成员，验证缺省值取的是 CLR 缺省（0）而不是第一个成员。
/// </summary>
public enum EvolvingKind
{
    /// <summary>
    /// 一。
    /// </summary>
    One = 1,

    /// <summary>
    /// 二。
    /// </summary>
    Two = 2,
}
