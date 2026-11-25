// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Json;

/// <summary>
/// JSON 文本没有通过读取检查的种类
/// </summary>
/// <remarks>
/// 照 RFC-0001「JSON 文本」的四条规则分：前四种是规范所说的语法错误（不论出现在哪里都优先于重复成员，路径只写 <c>$</c>，向量里的 <c>syntax</c>），它们之间按在文本里出现的先后报第一处；<see cref="DuplicateMember"/> 只在文本没有语法错误时报出（ADR-0291）。
/// 按种类分支时给其余的种类留缺省分支：规范以后加检查时这里跟着加成员
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public enum JsonTextProblemKind
{
    /// <summary>
    /// 不合 RFC 8259 的文法
    /// </summary>
    /// <remarks>
    /// 空文本与只有空白、BOM、注释、尾逗号、单引号、<c>NaN</c> / <c>Infinity</c>、前导零、正号、多个根值、未闭合的值、字符串里原始的控制字符、RFC 8259 之外的转义，以及字符串之外的非法字节
    /// </remarks>
    Syntax = 0,

    /// <summary>
    /// 嵌套超过 <see cref="JsonText.MaxDepth"/> 层
    /// </summary>
    /// <remarks>
    /// 最外层的对象或数组是第 1 层，第一个落在第 65 层的对象或数组即是
    /// </remarks>
    NestingTooDeep = 1,

    /// <summary>
    /// 字符串或成员名里有不合法的 UTF-8 字节序列
    /// </summary>
    /// <remarks>
    /// 含过长编码、编码成 UTF-8 的代理项与截断的多字节字符
    /// </remarks>
    InvalidUtf8 = 2,

    /// <summary>
    /// 字符串或成员名里有不成对的代理项转义
    /// </summary>
    /// <remarks>
    /// <c>\uD800</c>–<c>\uDBFF</c> 后没有紧跟 <c>\uDC00</c>–<c>\uDFFF</c>，或单独的低代理项
    /// </remarks>
    LoneSurrogate = 3,

    /// <summary>
    /// 某一层有重复的成员名
    /// </summary>
    /// <remarks>
    /// 名字按转义还原后逐个 UTF-16 码元比较，<c>"a"</c> 与 <c>"\u0061"</c> 重复，大小写不同不算
    /// </remarks>
    DuplicateMember = 4,
}
