// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives;

/// <summary>
/// 公开 API 的稳定性级别。级别含义与弃用流程是跨语言规范的一部分。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public enum Stability
{
    /// <summary>
    /// 大版本内不破坏；原语层在此之上再加"只增不改"的冻结承诺。
    /// </summary>
    Stable = 0,

    /// <summary>
    /// 可能变更，变更必须在文档中标注；不建议生产使用。
    /// </summary>
    Preview = 1,

    /// <summary>
    /// 可能随时移除，需要反馈；孵化区包只能使用此级别。
    /// </summary>
    Experimental = 2,

    /// <summary>
    /// 不对外承诺兼容；分析器阻止外部引用。
    /// </summary>
    Internal = 3,

    /// <summary>
    /// 已弃用，将在下一个大版本移除；至少跨两个小版本后升级为编译错误。
    /// </summary>
    Deprecated = 4,
}
