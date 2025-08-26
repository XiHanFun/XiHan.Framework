#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:YamlExtensions
// Guid:3e0d9f34-1771-4b90-bf9b-6ce71dc6d808
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 8:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>


#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:YamlExtensions
// Guid:3e0d9f34-1771-4b90-bf9b-6ce71dc6d808
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 8:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Text.Yaml;

namespace XiHan.Framework.Utils.Serialization.Yaml;

/// <summary>
/// YAML 扩展方法
/// </summary>
public static class YamlExtensions
{
    #region 对象扩展

    /// <summary>
    /// 将对象转换为 YAML 字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要转换的对象</param>
    /// <param name="options">序列化选项</param>
    /// <returns>YAML 字符串</returns>
    public static string ToYaml<T>(this T obj, YamlSerializeOptions? options = null)
    {
        return YamlHelper.Serialize(obj, options);
    }

    /// <summary>
    /// 异步将对象转换为 YAML 字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要转换的对象</param>
    /// <param name="options">序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>YAML 字符串</returns>
    public static async Task<string> ToYamlAsync<T>(this T obj, YamlSerializeOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await YamlHelper.SerializeAsync(obj, options, cancellationToken);
    }

    #endregion 对象扩展

    #region 字符串扩展

    /// <summary>
    /// 从 YAML 字符串反序列化为对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>反序列化的对象</returns>
    public static T FromYaml<T>(this string yaml, YamlDeserializeOptions? options = null)
    {
        return YamlHelper.Deserialize<T>(yaml, options);
    }

    /// <summary>
    /// 异步从 YAML 字符串反序列化为对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="options">反序列化选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>反序列化的对象</returns>
    public static async Task<T> FromYamlAsync<T>(this string yaml, YamlDeserializeOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await YamlHelper.DeserializeAsync<T>(yaml, options, cancellationToken);
    }

    /// <summary>
    /// 检查字符串是否为有效的 YAML
    /// </summary>
    /// <param name="yaml">YAML 字符串</param>
    /// <returns>是否有效</returns>
    public static bool IsValidYaml(this string yaml)
    {
        return YamlHelper.IsValidYaml(yaml);
    }

    /// <summary>
    /// 检查字符串是否为有效的 YAML
    /// </summary>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="errorMessage">错误信息</param>
    /// <returns>是否有效</returns>
    public static bool IsValidYaml(this string yaml, out string? errorMessage)
    {
        return YamlHelper.IsValidYaml(yaml, out errorMessage);
    }

    /// <summary>
    /// 格式化 YAML 字符串
    /// </summary>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="options">序列化选项</param>
    /// <returns>格式化后的 YAML 字符串</returns>
    public static string FormatYaml(this string yaml, YamlSerializeOptions? options = null)
    {
        return YamlHelper.FormatYaml(yaml, options);
    }

    /// <summary>
    /// 解析 YAML 字符串为字典
    /// </summary>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="options">解析选项</param>
    /// <returns>键值对字典</returns>
    public static Dictionary<string, string> ParseYaml(this string yaml, YamlParseOptions? options = null)
    {
        return YamlHelper.ParseYaml(yaml, options);
    }

    /// <summary>
    /// 解析多层级的 YAML（扁平化处理）
    /// </summary>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="options">解析选项</param>
    /// <returns>扁平化的键值对字典</returns>
    public static Dictionary<string, string> ParseNestedYaml(this string yaml, YamlParseOptions? options = null)
    {
        return YamlHelper.ParseNestedYaml(yaml, options);
    }

    /// <summary>
    /// YAML 转 JSON 字符串
    /// </summary>
    /// <param name="yaml">YAML 字符串</param>
    /// <param name="options">转换选项</param>
    /// <returns>JSON 字符串</returns>
    public static string ToJson(this string yaml, YamlDeserializeOptions? options = null)
    {
        return YamlHelper.YamlToJson(yaml, options);
    }

    #endregion 字符串扩展

    #region 字典扩展

    /// <summary>
    /// 将字典转换为 YAML 字符串
    /// </summary>
    /// <param name="dictionary">字典</param>
    /// <param name="options">序列化选项</param>
    /// <returns>YAML 字符串</returns>
    public static string ToYaml(this Dictionary<string, string> dictionary, YamlSerializeOptions? options = null)
    {
        return YamlHelper.ConvertToYaml(dictionary, options);
    }

    /// <summary>
    /// 从字典中获取值，如果不存在则返回默认值
    /// </summary>
    /// <param name="dictionary">字典</param>
    /// <param name="key">键</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>值或默认值</returns>
    public static string GetValueOrDefault(this Dictionary<string, string> dictionary, string key, string defaultValue = "")
    {
        return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// 从字典中获取嵌套键的值
    /// </summary>
    /// <param name="dictionary">字典</param>
    /// <param name="nestedKey">嵌套键（如 "parent.child.grandchild"）</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>值或默认值</returns>
    public static string GetNestedValue(this Dictionary<string, string> dictionary, string nestedKey, string defaultValue = "")
    {
        return dictionary.GetValueOrDefault(nestedKey, defaultValue);
    }

    /// <summary>
    /// 设置嵌套键的值
    /// </summary>
    /// <param name="dictionary">字典</param>
    /// <param name="nestedKey">嵌套键</param>
    /// <param name="value">值</param>
    public static void SetNestedValue(this Dictionary<string, string> dictionary, string nestedKey, string value)
    {
        dictionary[nestedKey] = value;
    }

    /// <summary>
    /// 获取指定前缀的所有键值对
    /// </summary>
    /// <param name="dictionary">字典</param>
    /// <param name="prefix">前缀</param>
    /// <param name="separator">分隔符</param>
    /// <param name="removePrefix">是否移除前缀</param>
    /// <returns>匹配的键值对</returns>
    public static Dictionary<string, string> GetByPrefix(this Dictionary<string, string> dictionary, string prefix, string separator = ".", bool removePrefix = false)
    {
        var result = new Dictionary<string, string>();
        var searchPrefix = prefix.EndsWith(separator) ? prefix : prefix + separator;

        foreach (var kvp in dictionary)
        {
            if (kvp.Key.StartsWith(searchPrefix))
            {
                var key = removePrefix ? kvp.Key[searchPrefix.Length..] : kvp.Key;
                result[key] = kvp.Value;
            }
        }

        return result;
    }

    /// <summary>
    /// 合并两个字典
    /// </summary>
    /// <param name="dictionary">目标字典</param>
    /// <param name="other">要合并的字典</param>
    /// <param name="overwrite">是否覆盖已存在的键</param>
    /// <returns>合并后的字典</returns>
    public static Dictionary<string, string> Merge(this Dictionary<string, string> dictionary, Dictionary<string, string> other, bool overwrite = true)
    {
        var result = new Dictionary<string, string>(dictionary);

        foreach (var kvp in other)
        {
            if (overwrite || !result.ContainsKey(kvp.Key))
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }

    /// <summary>
    /// 过滤字典
    /// </summary>
    /// <param name="dictionary">字典</param>
    /// <param name="predicate">过滤条件</param>
    /// <returns>过滤后的字典</returns>
    public static Dictionary<string, string> Filter(this Dictionary<string, string> dictionary, Func<KeyValuePair<string, string>, bool> predicate)
    {
        return dictionary.Where(predicate).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// 转换字典值
    /// </summary>
    /// <param name="dictionary">字典</param>
    /// <param name="valueTransform">值转换函数</param>
    /// <returns>转换后的字典</returns>
    public static Dictionary<string, string> TransformValues(this Dictionary<string, string> dictionary, Func<string, string> valueTransform)
    {
        return dictionary.ToDictionary(kvp => kvp.Key, kvp => valueTransform(kvp.Value));
    }

    /// <summary>
    /// 转换字典键
    /// </summary>
    /// <param name="dictionary">字典</param>
    /// <param name="keyTransform">键转换函数</param>
    /// <returns>转换后的字典</returns>
    public static Dictionary<string, string> TransformKeys(this Dictionary<string, string> dictionary, Func<string, string> keyTransform)
    {
        return dictionary.ToDictionary(kvp => keyTransform(kvp.Key), kvp => kvp.Value);
    }

    #endregion 字典扩展

    #region 类型转换扩展

    /// <summary>
    /// 尝试将字符串值转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="value">字符串值</param>
    /// <param name="result">转换结果</param>
    /// <returns>是否转换成功</returns>
    public static bool TryConvertTo<T>(this string value, out T? result)
    {
        result = default;

        try
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            var targetType = typeof(T);
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlyingType == typeof(string))
            {
                result = (T)(object)value;
                return true;
            }

            if (underlyingType == typeof(bool))
            {
                if (bool.TryParse(value, out var boolValue))
                {
                    result = (T)(object)boolValue;
                    return true;
                }
                return false;
            }

            if (underlyingType == typeof(int))
            {
                if (int.TryParse(value, out var intValue))
                {
                    result = (T)(object)intValue;
                    return true;
                }
                return false;
            }

            if (underlyingType == typeof(double))
            {
                if (double.TryParse(value, out var doubleValue))
                {
                    result = (T)(object)doubleValue;
                    return true;
                }
                return false;
            }

            if (underlyingType == typeof(decimal))
            {
                if (decimal.TryParse(value, out var decimalValue))
                {
                    result = (T)(object)decimalValue;
                    return true;
                }
                return false;
            }

            if (underlyingType == typeof(DateTime))
            {
                if (DateTime.TryParse(value, out var dateTimeValue))
                {
                    result = (T)(object)dateTimeValue;
                    return true;
                }
                return false;
            }

            // 尝试使用 Convert.ChangeType
            result = (T)Convert.ChangeType(value, underlyingType);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 将字符串值转换为指定类型，失败时返回默认值
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="value">字符串值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换结果或默认值</returns>
    public static T ConvertToOrDefault<T>(this string value, T defaultValue = default!)
    {
        return value.TryConvertTo<T>(out var result) ? result! : defaultValue;
    }

    #endregion 类型转换扩展
}
