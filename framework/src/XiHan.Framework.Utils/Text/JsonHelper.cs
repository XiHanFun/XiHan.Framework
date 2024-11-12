#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2022 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JsonHelper
// Guid:227522db-7512-4a80-972c-bbedb715da02
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-11-21 上午 01:06:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;
using System.Text.Json;

namespace XiHan.Framework.Utils.Text;

/// <summary>
/// JsonHelper
/// </summary>
public class JsonHelper
{
    private readonly string _jsonFilePath;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="jsonFilePath">文件路径</param>
    public JsonHelper(string jsonFilePath)
    {
        _jsonFilePath = jsonFilePath;
    }

    /// <summary>
    /// 加载整个文件并反序列化为一个对象
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T? Get<T>()
    {
        if (!File.Exists(_jsonFilePath))
        {
            return default;
        }

        string? jsonStr = File.ReadAllText(_jsonFilePath, Encoding.UTF8);
        T? result = JsonSerializer.Deserialize<T>(jsonStr, JsonSerializerOptionsHelper.DefaultJsonSerializerOptions);
        return result;
    }

    /// <summary>
    /// 从Json文件中读取对象
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="keyLink">对象的键名链，例如 Order.User</param>
    /// <returns>类型为T的对象</returns>
    public T? Get<T>(string keyLink)
    {
        if (!File.Exists(_jsonFilePath))
        {
            return default;
        }

        using StreamReader streamReader = new(_jsonFilePath);
        string? jsonStr = streamReader.ReadToEnd();
        dynamic? obj = JsonSerializer.Deserialize<T>(jsonStr, JsonSerializerOptionsHelper.DefaultJsonSerializerOptions);
        obj ??= JsonDocument.Parse(JsonSerializer.Serialize(new object()));
        string[]? keys = keyLink.Split(':');
        dynamic? currentObject = obj;
        foreach (string? key in keys)
        {
            currentObject = currentObject[key];
            if (currentObject == null)
            {
                return default;
            }
        }

        dynamic? result = JsonSerializer.Deserialize<T>(currentObject.ToString(), JsonSerializerOptionsHelper.DefaultJsonSerializerOptions);
        return result;
    }

    /// <summary>
    /// 设置对象值
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="keyLink">对象的键名链，例如 Order.User，当其上级不存在时将创建</param>
    /// <param name="value"></param>
    public void Set<T, TValue>(string keyLink, TValue value)
    {
        string? jsonStr = File.ReadAllText(_jsonFilePath, Encoding.UTF8);
        dynamic? jsoObj = JsonSerializer.Deserialize<T>(jsonStr, JsonSerializerOptionsHelper.DefaultJsonSerializerOptions);
        jsoObj ??= JsonDocument.Parse(JsonSerializer.Serialize(new object()));

        string[]? keys = keyLink.Split(':');
        dynamic? currentObject = jsoObj;
        for (int i = 0; i < keys.Length; i++)
        {
            dynamic? oldObject = currentObject;
            currentObject = currentObject[keys[i]];
            bool isValueType = value!.GetType().IsValueType;
            if (i == keys.Length - 1)
            {
                oldObject[keys[i]] = isValueType || value is string ? (dynamic)JsonSerializer.Serialize(value) : value;
            }
            else
            {
                //如果不存在，新建
                if (currentObject != null)
                {
                    continue;
                }

                JsonDocument? obj = JsonDocument.Parse(JsonSerializer.Serialize(new object()));
                oldObject[keys[i]] = obj;
                currentObject = oldObject[keys[i]];
            }
        }

        Save<dynamic>(jsoObj);
    }

    /// <summary>
    /// 保存Json文件
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="jsoObj"></param>
    private void Save<T>(T jsoObj)
    {
        string? jsonStr = JsonSerializer.Serialize(jsoObj, JsonSerializerOptionsHelper.DefaultJsonSerializerOptions);
        File.WriteAllText(_jsonFilePath, jsonStr, Encoding.UTF8);
    }
}
