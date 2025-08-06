#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExtensionPropertyLookupConfiguration
// Guid:1775979c-8f49-44f9-8a92-2e5406af4745
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 7:25:59
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
