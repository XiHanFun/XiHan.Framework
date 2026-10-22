// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Generators;

/// <summary>
/// 与 <c>System.Text.Json.Serialization.JsonKnownNamingPolicy</c> 同值的命名策略。
/// </summary>
internal enum JsonNamingPolicyKind
{
    /// <summary>不改名。</summary>
    Unspecified = 0,

    /// <summary>camelCase。</summary>
    CamelCase = 1,

    /// <summary>snake_case。</summary>
    SnakeCaseLower = 2,

    /// <summary>SNAKE_CASE。</summary>
    SnakeCaseUpper = 3,

    /// <summary>kebab-case。</summary>
    KebabCaseLower = 4,

    /// <summary>KEBAB-CASE。</summary>
    KebabCaseUpper = 5,
}
