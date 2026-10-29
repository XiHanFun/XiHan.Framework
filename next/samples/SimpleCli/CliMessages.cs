// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace SimpleCli;

/// <summary>
/// 示例的消息键。
/// </summary>
/// <remarks>
/// 各语言文案在 <c>Localization/&lt;区域&gt;.json</c>。
/// </remarks>
public static class CliMessages
{
    /// <summary>
    /// MCP tool <c>greetings_create</c> 的描述（约定键 <c>&lt;操作 ID&gt;.description</c>）。
    /// </summary>
    public const string GreetDescription = "greetings.create.description";

    /// <summary>
    /// 重复次数超出范围。
    /// </summary>
    /// <remarks>
    /// 参数：<c>times</c>。
    /// </remarks>
    public const string TimesInvalid = "samples.greet.times_invalid";
}
