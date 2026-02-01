#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JsonSerializerOptionsHelper
// Guid:86f5669e-8854-4105-8073-6147be5d7b7e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/04/22 03:05:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Serialization;

/// <summary>
/// 序列化参数帮助类
/// </summary>
public static class JsonSerializerOptionsHelper
{
    /// <summary>
    /// 使用 baseOptions 作为基础，移除 removeConverter，并添加 addConverters 中的转换器(如果它们尚不存在)
    /// </summary>
    /// <param name="baseOptions"></param>
    /// <param name="removeConverter"></param>
    /// <param name="addConverters"></param>
    /// <returns></returns>
    public static JsonSerializerOptions Create(JsonSerializerOptions baseOptions, JsonConverter removeConverter, params JsonConverter[] addConverters)
    {
        return Create(baseOptions, x => x == removeConverter, addConverters);
    }

    /// <summary>
    /// 使用 baseOptions 作为基础，移除匹配 removeConverterPredicate 谓词的转换器，并添加 addConverters 中的转换器(如果它们尚不存在)
    /// </summary>
    /// <param name="baseOptions"></param>
    /// <param name="removeConverterPredicate"></param>
    /// <param name="addConverters"></param>
    /// <returns></returns>
    public static JsonSerializerOptions Create(JsonSerializerOptions baseOptions, Func<JsonConverter, bool> removeConverterPredicate, params JsonConverter[] addConverters)
    {
        JsonSerializerOptions options = new(baseOptions);
        options.Converters.RemoveAllWhere(removeConverterPredicate);
        options.Converters.AddIfNotContains(addConverters);
        return options;
    }
}
