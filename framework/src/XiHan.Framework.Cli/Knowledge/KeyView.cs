// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 一个配置键的说明
/// </summary>
/// <param name="Path">完整的键路径</param>
/// <param name="Type">类型</param>
/// <param name="Default">缺省值</param>
/// <param name="Required">是否必填</param>
/// <param name="Minimum">闭区间下限</param>
/// <param name="ExclusiveMinimum">开区间下限</param>
/// <param name="Maximum">闭区间上限</param>
/// <param name="Values">枚举允许的取值</param>
/// <param name="Summary">说明</param>
internal sealed record KeyView(string Path, string Type, string? Default, bool Required, double? Minimum, double? ExclusiveMinimum, double? Maximum, IReadOnlyList<string>? Values, string Summary);
