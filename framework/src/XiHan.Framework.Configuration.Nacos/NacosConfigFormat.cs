// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Nacos;

/// <summary>
/// Nacos 配置内容的格式：决定整份内容怎样展平成 <c>IConfiguration</c> 的键
/// </summary>
/// <remarks>
/// 两种格式展平后的键都不区分大小写，同一个键出现两次（含只差大小写）时文档不合法（<c>CONFIGURATION_VALIDATION_001</c>），不取其中任何一个；逐条的规则见规范 RFC-0014「Nacos Provider」的「文档格式」（ADR-0286）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public enum NacosConfigFormat
{
    /// <summary>
    /// JSON 对象：嵌套对象按 <c>A:B</c>、数组按下标展平，<c>null</c> 保留为空值
    /// </summary>
    /// <remarks>
    /// 与 .NET 的 JSON 配置提供者同样的读法：照收 <c>//</c>、<c>/* */</c> 注释与尾逗号，字符串取值、数字取原文、布尔写成 <c>True</c> / <c>False</c>；顶层不是对象、嵌套超过 64 层不合法。空对象与空数组不产生键
    /// </remarks>
    Json = 0,

    /// <summary>
    /// properties：每行 <c>a.b=c</c>（<c>a.b:c</c> 亦可），键里的点换成 <c>:</c>，<c>#</c> 与 <c>!</c> 开头的行是注释
    /// </summary>
    /// <remarks>
    /// 行、键、值两侧的空白去掉，第一个 <c>=</c> 或 <c>:</c> 分隔键值，没有分隔符的行值为空串；不认转义与续行；键为空不合法
    /// </remarks>
    Properties = 1,
}
