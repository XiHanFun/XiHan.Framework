// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Nacos;

/// <summary>
/// Nacos 配置内容的格式：决定整份内容怎样展平成 <c>IConfiguration</c> 的键。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public enum NacosConfigFormat
{
    /// <summary>
    /// JSON 对象：嵌套对象按 <c>A:B</c>、数组按下标展平，<c>null</c> 保留为空值。
    /// </summary>
    Json = 0,

    /// <summary>
    /// properties：每行 <c>a.b=c</c>（<c>a.b:c</c> 亦可），键里的点换成 <c>:</c>，<c>#</c> 与 <c>!</c> 开头的行是注释。
    /// </summary>
    Properties = 1,
}
