// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.ObjectMapping.Modularity;

/// <summary>
/// 扩展属性查找配置
/// </summary>
public class ExtensionPropertyLookupConfiguration
{
    /// <summary>
    /// 查找数据的请求地址
    /// </summary>
    public string Url { get; set; } = default!;

    /// <summary>
    /// 结果列表属性名称
    /// 默认值: "items"
    /// </summary>
    public string ResultListPropertyName { get; set; } = "items";

    /// <summary>
    /// 显示属性名称
    /// 默认值: "text"
    /// </summary>
    public string DisplayPropertyName { get; set; } = "text";

    /// <summary>
    /// 值属性名称
    /// 默认值: "id"
    /// </summary>
    public string ValuePropertyName { get; set; } = "id";

    /// <summary>
    /// 过滤参数名称
    /// 默认值: "filter"
    /// </summary>
    public string FilterParamName { get; set; } = "filter";
}
