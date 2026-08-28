// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Utils.Serialization.Json.Converters;

namespace XiHan.Framework.Utils.Tests.Serialization.Json.Converters;

/// <summary>
/// 日期时间转换器的区域性无关性测试
/// </summary>
/// <remarks>
/// JSON 是对外协议，输出格式不应随部署环境的区域设置漂移。
/// 自定义格式串里的 ':' 是"时间分隔符占位符"、'/' 是"日期分隔符占位符"，
/// 用当前区域性格式化时会被替换成该区域自己的分隔符；修复前转换器正是这么做的。
/// 这里刻意造一个分隔符异常的区域性来放大差异，并且整段逻辑放到独立线程内执行，
/// 避免改动 CurrentCulture 泄漏到并行执行的其它用例。
/// </remarks>
public class DateTimeConverterCultureTests
{
    /// <summary>
    /// 用给定转换器构造一个干净的序列化选项
    /// </summary>
    /// <param name="converters">要挂载的转换器</param>
    private static JsonSerializerOptions CreateOptions(params JsonConverter[] converters)
    {
        var options = new JsonSerializerOptions();
        foreach (var converter in converters)
        {
            options.Converters.Add(converter);
        }

        return options;
    }

    /// <summary>
    /// 构造一个日期与时间分隔符都被改掉的区域性
    /// </summary>
    private static CultureInfo CreateExoticSeparatorCulture()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
        culture.DateTimeFormat.DateSeparator = "#";
        culture.DateTimeFormat.TimeSeparator = "@";
        return culture;
    }

    /// <summary>
    /// 在指定区域性的独立线程内执行，执行完线程即销毁，不污染其它用例
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="culture">要应用的区域性</param>
    /// <param name="action">要执行的逻辑</param>
    /// <returns>执行结果</returns>
    private static T RunWithCulture<T>(CultureInfo culture, Func<T> action)
    {
        var result = default(T)!;
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                result = action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new InvalidOperationException("在指定区域性的线程内执行失败", failure);
        }

        return result;
    }

    /// <summary>
    /// DateTime 输出不随当前区域性的日期/时间分隔符漂移
    /// </summary>
    [Fact]
    public void DateTimeConverter_UnderExoticCulture_KeepsInvariantOutput()
    {
        var json = RunWithCulture(CreateExoticSeparatorCulture(), () =>
        {
            var options = CreateOptions(new DateTimeJsonConverter("yyyy/MM/dd HH:mm:ss", false));
            return JsonSerializer.Serialize(new DateTime(2024, 5, 6, 7, 8, 9), options);
        });

        Assert.Equal("\"2024/05/06 07:08:09\"", json);
    }

    /// <summary>
    /// 可空 DateTime 输出同样不随当前区域性漂移
    /// </summary>
    [Fact]
    public void DateTimeNullableConverter_UnderExoticCulture_KeepsInvariantOutput()
    {
        var json = RunWithCulture(CreateExoticSeparatorCulture(), () =>
        {
            var options = CreateOptions(new DateTimeNullableConverter("yyyy/MM/dd HH:mm:ss", false));
            return JsonSerializer.Serialize<DateTime?>(new DateTime(2024, 5, 6, 7, 8, 9), options);
        });

        Assert.Equal("\"2024/05/06 07:08:09\"", json);
    }

    /// <summary>
    /// 默认格式在异常区域性下仍然输出 yyyy-MM-dd HH:mm:ss
    /// </summary>
    /// <remarks>
    /// 默认格式里的 '-' 是字面量不受影响，但 ':' 是占位符，修复前会被换成该区域性的时间分隔符。
    /// </remarks>
    [Fact]
    public void DateTimeConverter_UnderExoticCulture_KeepsDefaultFormatSeparators()
    {
        var json = RunWithCulture(CreateExoticSeparatorCulture(), () =>
        {
            var options = CreateOptions(new DateTimeJsonConverter());
            return JsonSerializer.Serialize(new DateTime(2024, 5, 6, 7, 8, 9), options);
        });

        Assert.Equal("\"2024-05-06 07:08:09\"", json);
    }

    /// <summary>
    /// 异常区域性下仍能读回不变区域性写出的文本
    /// </summary>
    [Fact]
    public void DateTimeConverter_UnderExoticCulture_ParsesInvariantText()
    {
        var value = RunWithCulture(CreateExoticSeparatorCulture(), () =>
        {
            var options = CreateOptions(new DateTimeJsonConverter());
            return JsonSerializer.Deserialize<DateTime>("\"2024-05-06 07:08:09\"", options);
        });

        Assert.Equal(new DateTime(2024, 5, 6, 7, 8, 9), value);
    }

    /// <summary>
    /// 可空 DateTime 在异常区域性下的 null 与有值往返
    /// </summary>
    [Fact]
    public void DateTimeNullableConverter_UnderExoticCulture_RoundTripsNullAndValue()
    {
        var culture = CreateExoticSeparatorCulture();

        var nullJson = RunWithCulture(culture, () =>
            JsonSerializer.Serialize<DateTime?>(null, CreateOptions(new DateTimeNullableConverter())));
        var parsed = RunWithCulture(culture, () =>
            JsonSerializer.Deserialize<DateTime?>("\"2024-05-06 07:08:09\"", CreateOptions(new DateTimeNullableConverter())));

        Assert.Equal("null", nullJson);
        Assert.Equal(new DateTime(2024, 5, 6, 7, 8, 9), parsed);
    }
}
