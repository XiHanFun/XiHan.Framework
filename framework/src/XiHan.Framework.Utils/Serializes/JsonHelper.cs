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

namespace XiHan.Framework.Utils.Serializes;

/// <summary>
/// JsonHelper
/// </summary>
/// <remarks>
/// 构造函数
/// </remarks>
/// <param name="jsonFilePath">文件路径</param>
public class JsonHelper(string jsonFilePath)
{
    private readonly string _jsonFilePath = jsonFilePath;

    /// <summary>
    /// 加载整个文件并反序列化为一个对象
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <returns></returns>
    public TEntity? Get<TEntity>()
    {
        if (!File.Exists(_jsonFilePath)) return default;

        var jsonStr = File.ReadAllText(_jsonFilePath, Encoding.UTF8);
        var result = JsonSerializer.Deserialize<TEntity>(jsonStr, SerializeHelper.JsonSerializerOptionsInstance);
        return result;
    }

    /// <summary>
    /// 从Json文件中读取对象
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="keyLink">对象的键名链，例如 Order.User</param>
    /// <returns>类型为TEntity的对象</returns>
    public TEntity? Get<TEntity>(string keyLink)
    {
        if (!File.Exists(_jsonFilePath)) return default;

        using StreamReader streamReader = new(_jsonFilePath);
        var jsonStr = streamReader.ReadToEnd();
        dynamic? obj = JsonSerializer.Deserialize<TEntity>(jsonStr, SerializeHelper.JsonSerializerOptionsInstance);
        obj ??= JsonDocument.Parse(JsonSerializer.Serialize(new object()));
        var keys = keyLink.Split(':');
        var currentObject = obj;
        foreach (var key in keys)
        {
            currentObject = currentObject[key];
            if (currentObject == null) return default;
        }

        var result = JsonSerializer.Deserialize<TEntity>(currentObject.ToString(), SerializeHelper.JsonSerializerOptionsInstance);
        return result;
    }

    /// <summary>
    /// 设置对象值
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <typeparam name="TREntity"></typeparam>
    /// <param name="keyLink">对象的键名链，例如 Order.User，当其上级不存在时将创建</param>
    /// <param name="value"></param>
    public void Set<TEntity, TREntity>(string keyLink, TREntity value)
    {
        var jsonStr = File.ReadAllText(_jsonFilePath, Encoding.UTF8);
        dynamic? jsoObj = JsonSerializer.Deserialize<TEntity>(jsonStr, SerializeHelper.JsonSerializerOptionsInstance);
        jsoObj ??= JsonDocument.Parse(JsonSerializer.Serialize(new object()));

        var keys = keyLink.Split(':');
        var currentObject = jsoObj;
        for (var i = 0; i < keys.Length; i++)
        {
            var oldObject = currentObject;
            currentObject = currentObject[keys[i]];
            var isValueType = value!.GetType().IsValueType;
            if (i == keys.Length - 1)
            {
                oldObject[keys[i]] = isValueType || value is string ? (dynamic)JsonSerializer.Serialize(value) : value;
            }
            else
            {
                //如果不存在，新建
                if (currentObject != null) continue;

                var obj = JsonDocument.Parse(JsonSerializer.Serialize(new object()));
                oldObject[keys[i]] = obj;
                currentObject = oldObject[keys[i]];
            }
        }

        Save<dynamic>(jsoObj);
    }

    /// <summary>
    /// 保存Json文件
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="jsoObj"></param>
    private void Save<TEntity>(TEntity jsoObj)
    {
        var jsonStr = JsonSerializer.Serialize(jsoObj, SerializeHelper.JsonSerializerOptionsInstance);
        File.WriteAllText(_jsonFilePath, jsonStr, Encoding.UTF8);
    }
}