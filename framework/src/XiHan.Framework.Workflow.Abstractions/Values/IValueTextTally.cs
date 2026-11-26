// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;

namespace XiHan.Framework.Workflow.Abstractions.Values;

/// <summary>
/// 写数组与对象的文本时逐个记下读到的值
/// </summary>
/// <remarks>
/// <see cref="WorkflowValueText"/> 写容器的文本时，每写一个值（容器里的每个值，最外层是容器自己）先记它，再记写进文本的成员名、字符串与数字。表达式的工作量由它记（RFC-0021「长度与上限」）：量规模与写文本走同一遍，每个字符串只取一次文本，量的与写的是同一份；累计超过上限时抛出，写到一半即停
/// </remarks>
internal interface IValueTextTally
{
    /// <summary>
    /// 记一个值
    /// </summary>
    void Value();

    /// <summary>
    /// 记一个成员名
    /// </summary>
    /// <param name="name">成员名</param>
    void Name(string name);

    /// <summary>
    /// 记一个字符串的文本
    /// </summary>
    /// <param name="text">写进文本的那一份</param>
    void Text(string text);

    /// <summary>
    /// 记一个数字
    /// </summary>
    /// <param name="value">JSON 类型是数字的标量</param>
    void Number(JsonValue value);
}
