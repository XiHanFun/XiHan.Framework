#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DictionaryExtensionsTest
// Guid:bc2346d6-09f3-43a8-86b8-884e94f4c797
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/16 4:37:47
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Utils.Test.Extensions;

/// <summary>
/// DictionaryExtensionsTest
/// </summary>
public class DictionaryExtensionsTest
{
    /// <summary>
    /// 测试 TryGetValue 扩展方法
    /// </summary>
    public static void TryGetValueTest()
    {
        Console.WriteLine("TryGetValue Test");

        var dictionary = new Dictionary<string, object>
        {
            { "key1", "value1" },
            { "key2", 123 }
        };

        if (dictionary.TryGetValue("key1", out string? value1))
        {
            Console.WriteLine($"key1: {value1}");
        }
        else
        {
            Console.WriteLine("key1 not found");
        }

        if (dictionary.TryGetValue("key2", out int value2))
        {
            Console.WriteLine($"key2: {value2}");
        }
        else
        {
            Console.WriteLine("key2 not found");
        }
    }

    /// <summary>
    /// 测试 GetOrDefault 扩展方法
    /// </summary>
    public static void GetOrDefaultTest()
    {
        Console.WriteLine("GetOrDefault Test");

        var dictionary = new Dictionary<string, string>
        {
            { "key1", "value1" }
        };

        var value1 = dictionary.GetOrDefault("key1");
        Console.WriteLine($"key1: {value1}");

        var value2 = dictionary.GetOrDefault("key2");
        Console.WriteLine($"key2: {value2 ?? "default"}");
    }

    /// <summary>
    /// 测试 GetOrAdd 扩展方法
    /// </summary>
    public static void GetOrAddTest()
    {
        Console.WriteLine("GetOrAdd Test");

        var dictionary = new Dictionary<string, string>
        {
            { "key1", "value1" }
        };

        var value1 = dictionary.GetOrAdd("key1", () => "newValue1");
        Console.WriteLine($"key1: {value1}");

        var value2 = dictionary.GetOrAdd("key2", () => "newValue2");
        Console.WriteLine($"key2: {value2}");
    }

    /// <summary>
    /// 测试 ConvertToDynamicObject 扩展方法
    /// </summary>
    public static void ConvertToDynamicObjectTest()
    {
        Console.WriteLine("ConvertToDynamicObject Test");

        var dictionary = new Dictionary<string, object>
        {
            { "key1", "value1" },
            { "key2", 123 }
        };

        dynamic dynamicObject = dictionary.ConvertToDynamicObject();
        Console.WriteLine($"key1: {dynamicObject.key1}");
        Console.WriteLine($"key2: {dynamicObject.key2}");
    }
}