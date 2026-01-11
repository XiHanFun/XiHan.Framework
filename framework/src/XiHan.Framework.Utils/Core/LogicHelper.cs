#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LogicHelper
// Guid:7h6g5f4e-3d2c-1b0a-9f8e-7d6c5b4a3f2e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/20 0:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Core;

/// <summary>
/// 逻辑运算帮助类
/// </summary>
public static class LogicHelper
{
    #region 条件选择

    /// <summary>
    /// 条件选择（三元运算符的方法版本）
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="condition">条件</param>
    /// <param name="trueValue">条件为真时的值</param>
    /// <param name="falseValue">条件为假时的值</param>
    /// <returns>根据条件选择的值</returns>
    public static T If<T>(bool condition, T trueValue, T falseValue)
    {
        return condition ? trueValue : falseValue;
    }

    /// <summary>
    /// 延迟评估的条件选择
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="condition">条件</param>
    /// <param name="trueValueFactory">条件为真时的值工厂</param>
    /// <param name="falseValueFactory">条件为假时的值工厂</param>
    /// <returns>根据条件选择的值</returns>
    public static T If<T>(bool condition, Func<T> trueValueFactory, Func<T> falseValueFactory)
    {
        ArgumentNullException.ThrowIfNull(trueValueFactory);
        ArgumentNullException.ThrowIfNull(falseValueFactory);

        return condition ? trueValueFactory() : falseValueFactory();
    }

    /// <summary>
    /// 延迟评估的条件选择（支持条件工厂）
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="conditionFactory">条件工厂</param>
    /// <param name="trueValueFactory">条件为真时的值工厂</param>
    /// <param name="falseValueFactory">条件为假时的值工厂</param>
    /// <returns>根据条件选择的值</returns>
    public static T If<T>(Func<bool> conditionFactory, Func<T> trueValueFactory, Func<T> falseValueFactory)
    {
        ArgumentNullException.ThrowIfNull(conditionFactory);
        ArgumentNullException.ThrowIfNull(trueValueFactory);
        ArgumentNullException.ThrowIfNull(falseValueFactory);

        return conditionFactory() ? trueValueFactory() : falseValueFactory();
    }

    /// <summary>
    /// 多分支选择
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    /// <param name="key">要匹配的键</param>
    /// <param name="cases">分支字典</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>匹配的值或默认值</returns>
    public static TValue Switch<TKey, TValue>(TKey key, IDictionary<TKey, TValue> cases, TValue defaultValue = default!)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(cases);

        return cases.TryGetValue(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// 多分支选择（支持延迟评估）
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    /// <param name="key">要匹配的键</param>
    /// <param name="cases">分支字典</param>
    /// <param name="defaultValueFactory">默认值工厂</param>
    /// <returns>匹配的值或默认值</returns>
    public static TValue Switch<TKey, TValue>(TKey key, IDictionary<TKey, Func<TValue>> cases, Func<TValue>? defaultValueFactory = null)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(cases);

        if (cases.TryGetValue(key, out var valueFactory))
        {
            return valueFactory();
        }

        return defaultValueFactory != null ? defaultValueFactory.Invoke() : default!;
    }

    /// <summary>
    /// 条件匹配
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="value">要匹配的值</param>
    /// <param name="cases">匹配条件和结果的集合</param>
    /// <param name="defaultResult">默认结果</param>
    /// <returns>匹配的结果或默认结果</returns>
    public static TResult Case<T, TResult>(T value, IEnumerable<(Predicate<T> condition, TResult result)> cases, TResult defaultResult = default!)
    {
        ArgumentNullException.ThrowIfNull(cases);

        foreach (var (condition, result) in cases)
        {
            if (condition(value))
            {
                return result;
            }
        }

        return defaultResult;
    }

    #endregion

    #region 逻辑运算

    /// <summary>
    /// 判断所有条件是否都为真
    /// </summary>
    /// <param name="conditions">条件集合</param>
    /// <returns>如果所有条件都为真则返回 true，否则返回 false</returns>
    public static bool All(params bool[] conditions)
    {
        if (conditions == null || conditions.Length == 0)
        {
            return true; // 空集合默认为真
        }

        return conditions.All(c => c);
    }

    /// <summary>
    /// 判断所有条件是否都为真（延迟评估）
    /// </summary>
    /// <param name="conditions">条件工厂集合</param>
    /// <returns>如果所有条件都为真则返回 true，否则返回 false</returns>
    public static bool All(params Func<bool>[] conditions)
    {
        if (conditions == null || conditions.Length == 0)
        {
            return true; // 空集合默认为真
        }

        return conditions.All(condition => condition());
    }

    /// <summary>
    /// 判断任何条件是否为真
    /// </summary>
    /// <param name="conditions">条件集合</param>
    /// <returns>如果任何条件为真则返回 true，否则返回 false</returns>
    public static bool Any(params bool[] conditions)
    {
        if (conditions == null || conditions.Length == 0)
        {
            return false; // 空集合默认为假
        }

        return conditions.Any(c => c);
    }

    /// <summary>
    /// 判断任何条件是否为真（延迟评估）
    /// </summary>
    /// <param name="conditions">条件工厂集合</param>
    /// <returns>如果任何条件为真则返回 true，否则返回 false</returns>
    public static bool Any(params Func<bool>[] conditions)
    {
        if (conditions == null || conditions.Length == 0)
        {
            return false; // 空集合默认为假
        }

        return conditions.Any(condition => condition());
    }

    /// <summary>
    /// 判断所有条件是否都为假
    /// </summary>
    /// <param name="conditions">条件集合</param>
    /// <returns>如果所有条件都为假则返回 true，否则返回 false</returns>
    public static bool None(params bool[] conditions)
    {
        if (conditions == null || conditions.Length == 0)
        {
            return true; // 空集合默认为真
        }

        return !conditions.Any(c => c);
    }

    /// <summary>
    /// 判断所有条件是否都为假（延迟评估）
    /// </summary>
    /// <param name="conditions">条件工厂集合</param>
    /// <returns>如果所有条件都为假则返回 true，否则返回 false</returns>
    public static bool None(params Func<bool>[] conditions)
    {
        if (conditions == null || conditions.Length == 0)
        {
            return true; // 空集合默认为真
        }

        return !conditions.Any(condition => condition());
    }

    /// <summary>
    /// 异或运算（有且仅有一个条件为真）
    /// </summary>
    /// <param name="conditions">条件集合</param>
    /// <returns>如果有且仅有一个条件为真则返回 true，否则返回 false</returns>
    public static bool ExactlyOne(params bool[] conditions)
    {
        if (conditions == null || conditions.Length == 0)
        {
            return false;
        }

        return conditions.Count(c => c) == 1;
    }

    /// <summary>
    /// 异或运算（有且仅有一个条件为真，延迟评估）
    /// </summary>
    /// <param name="conditions">条件工厂集合</param>
    /// <returns>如果有且仅有一个条件为真则返回 true，否则返回 false</returns>
    public static bool ExactlyOne(params Func<bool>[] conditions)
    {
        if (conditions == null || conditions.Length == 0)
        {
            return false;
        }

        return conditions.Count(condition => condition()) == 1;
    }

    #endregion

    #region 条件执行

    /// <summary>
    /// 如果条件为真则执行操作
    /// </summary>
    /// <param name="condition">条件</param>
    /// <param name="action">要执行的操作</param>
    public static void ExecuteIf(bool condition, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (condition)
        {
            action();
        }
    }

    /// <summary>
    /// 如果条件为真则执行操作（延迟评估条件）
    /// </summary>
    /// <param name="conditionFactory">条件工厂</param>
    /// <param name="action">要执行的操作</param>
    public static void ExecuteIf(Func<bool> conditionFactory, Action action)
    {
        ArgumentNullException.ThrowIfNull(conditionFactory);
        ArgumentNullException.ThrowIfNull(action);

        if (conditionFactory())
        {
            action();
        }
    }

    /// <summary>
    /// 如果条件为假则执行操作
    /// </summary>
    /// <param name="condition">条件</param>
    /// <param name="action">要执行的操作</param>
    public static void ExecuteUnless(bool condition, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!condition)
        {
            action();
        }
    }

    /// <summary>
    /// 如果条件为假则执行操作（延迟评估条件）
    /// </summary>
    /// <param name="conditionFactory">条件工厂</param>
    /// <param name="action">要执行的操作</param>
    public static void ExecuteUnless(Func<bool> conditionFactory, Action action)
    {
        ArgumentNullException.ThrowIfNull(conditionFactory);
        ArgumentNullException.ThrowIfNull(action);

        if (!conditionFactory())
        {
            action();
        }
    }

    /// <summary>
    /// 安全执行操作，捕获异常并返回执行结果
    /// </summary>
    /// <param name="action">要执行的操作</param>
    /// <param name="onException">异常处理器</param>
    /// <returns>执行结果（成功、异常信息）</returns>
    public static (bool Success, Exception? Exception) TryExecute(Action action, Action<Exception>? onException = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            action();
            return (true, null);
        }
        catch (Exception ex)
        {
            onException?.Invoke(ex);
            return (false, ex);
        }
    }

    /// <summary>
    /// 安全执行操作并返回结果
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="func">要执行的函数</param>
    /// <param name="defaultValue">默认值（发生异常时返回）</param>
    /// <param name="onException">异常处理器</param>
    /// <returns>执行结果（成功、结果值、异常信息）</returns>
    public static (bool Success, T? Result, Exception? Exception) TryExecute<T>(Func<T> func, T? defaultValue = default, Action<Exception>? onException = null)
    {
        ArgumentNullException.ThrowIfNull(func);

        try
        {
            var result = func();
            return (true, result, null);
        }
        catch (Exception ex)
        {
            onException?.Invoke(ex);
            return (false, defaultValue, ex);
        }
    }

    #endregion

    #region 空值处理

    /// <summary>
    /// 返回第一个非空值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="values">值集合</param>
    /// <returns>第一个非空值，如果所有值都为空则返回默认值</returns>
    public static T? Coalesce<T>(params T?[] values)
    {
        if (values == null || values.Length == 0)
        {
            return default;
        }

        foreach (var value in values)
        {
            if (value != null)
            {
                return value;
            }
        }

        return default;
    }

    /// <summary>
    /// 返回第一个非空值（延迟评估）
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="valueFactories">值工厂集合</param>
    /// <returns>第一个非空值，如果所有值都为空则返回默认值</returns>
    public static T? Coalesce<T>(params Func<T?>[] valueFactories)
    {
        if (valueFactories == null || valueFactories.Length == 0)
        {
            return default;
        }

        foreach (var factory in valueFactories)
        {
            try
            {
                var value = factory();
                if (value != null)
                {
                    return value;
                }
            }
            catch
            {
                // 忽略异常，继续尝试下一个
                continue;
            }
        }

        return default;
    }

    /// <summary>
    /// 如果值为空则返回默认值
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="value">值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>原值或默认值</returns>
    public static T DefaultIfNull<T>(T? value, T defaultValue)
    {
        return value ?? defaultValue;
    }

    /// <summary>
    /// 如果值为空则返回默认值（延迟评估）
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="value">值</param>
    /// <param name="defaultValueFactory">默认值工厂</param>
    /// <returns>原值或默认值</returns>
    public static T DefaultIfNull<T>(T? value, Func<T> defaultValueFactory)
    {
        ArgumentNullException.ThrowIfNull(defaultValueFactory);

        return value ?? defaultValueFactory();
    }

    /// <summary>
    /// 如果字符串为空或空白则返回默认值
    /// </summary>
    /// <param name="value">字符串值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>原值或默认值</returns>
    public static string DefaultIfNullOrWhiteSpace(string? value, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    #endregion

    #region 函数式编程支持

    /// <summary>
    /// 函数组合（将两个函数组合为一个）
    /// </summary>
    /// <typeparam name="T1">第一个函数的输入类型</typeparam>
    /// <typeparam name="T2">中间类型</typeparam>
    /// <typeparam name="T3">最终输出类型</typeparam>
    /// <param name="first">第一个函数</param>
    /// <param name="second">第二个函数</param>
    /// <returns>组合后的函数</returns>
    public static Func<T1, T3> Compose<T1, T2, T3>(Func<T1, T2> first, Func<T2, T3> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return input => second(first(input));
    }

    /// <summary>
    /// 管道操作（将值传递给函数）
    /// </summary>
    /// <typeparam name="T">输入类型</typeparam>
    /// <typeparam name="TResult">输出类型</typeparam>
    /// <param name="value">输入值</param>
    /// <param name="func">处理函数</param>
    /// <returns>处理结果</returns>
    public static TResult Pipe<T, TResult>(T value, Func<T, TResult> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        return func(value);
    }

    /// <summary>
    /// 管道操作（连续处理多个函数）
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="value">初始值</param>
    /// <param name="functions">处理函数链</param>
    /// <returns>最终处理结果</returns>
    public static T Pipe<T>(T value, params Func<T, T>[] functions)
    {
        if (functions == null || functions.Length == 0)
        {
            return value;
        }

        return functions.Aggregate(value, (current, func) => func(current));
    }

    /// <summary>
    /// 柯里化（将多参数函数转换为单参数函数链）
    /// </summary>
    /// <typeparam name="T1">第一个参数类型</typeparam>
    /// <typeparam name="T2">第二个参数类型</typeparam>
    /// <typeparam name="TResult">返回值类型</typeparam>
    /// <param name="func">原函数</param>
    /// <returns>柯里化后的函数</returns>
    public static Func<T1, Func<T2, TResult>> Curry<T1, T2, TResult>(Func<T1, T2, TResult> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        return arg1 => arg2 => func(arg1, arg2);
    }

    /// <summary>
    /// 柯里化（三个参数）
    /// </summary>
    /// <typeparam name="T1">第一个参数类型</typeparam>
    /// <typeparam name="T2">第二个参数类型</typeparam>
    /// <typeparam name="T3">第三个参数类型</typeparam>
    /// <typeparam name="TResult">返回值类型</typeparam>
    /// <param name="func">原函数</param>
    /// <returns>柯里化后的函数</returns>
    public static Func<T1, Func<T2, Func<T3, TResult>>> Curry<T1, T2, T3, TResult>(Func<T1, T2, T3, TResult> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        return arg1 => arg2 => arg3 => func(arg1, arg2, arg3);
    }

    #endregion

    #region 重试机制

    /// <summary>
    /// 重试执行操作
    /// </summary>
    /// <param name="action">要执行的操作</param>
    /// <param name="maxAttempts">最大重试次数</param>
    /// <param name="delay">重试间隔</param>
    /// <param name="backoffFactor">退避因子</param>
    /// <returns>执行结果</returns>
    public static async Task<(bool Success, Exception? LastException)> RetryAsync(
        Func<Task> action,
        int maxAttempts = 3,
        TimeSpan? delay = null,
        double backoffFactor = 1.0)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "最大重试次数必须大于0");
        }

        Exception? lastException = null;
        var currentDelay = delay ?? TimeSpan.FromMilliseconds(100);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await action();
                return (true, null);
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt == maxAttempts)
                {
                    break;
                }

                if (currentDelay > TimeSpan.Zero)
                {
                    await Task.Delay(currentDelay);
                    currentDelay = TimeSpan.FromMilliseconds(currentDelay.TotalMilliseconds * backoffFactor);
                }
            }
        }

        return (false, lastException);
    }

    /// <summary>
    /// 重试执行函数
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="func">要执行的函数</param>
    /// <param name="maxAttempts">最大重试次数</param>
    /// <param name="delay">重试间隔</param>
    /// <param name="backoffFactor">退避因子</param>
    /// <returns>执行结果</returns>
    public static async Task<(bool Success, T? Result, Exception? LastException)> RetryAsync<T>(
        Func<Task<T>> func,
        int maxAttempts = 3,
        TimeSpan? delay = null,
        double backoffFactor = 1.0)
    {
        ArgumentNullException.ThrowIfNull(func);

        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "最大重试次数必须大于0");
        }

        Exception? lastException = null;
        var currentDelay = delay ?? TimeSpan.FromMilliseconds(100);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var result = await func();
                return (true, result, null);
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt == maxAttempts)
                {
                    break;
                }

                if (currentDelay > TimeSpan.Zero)
                {
                    await Task.Delay(currentDelay);
                    currentDelay = TimeSpan.FromMilliseconds(currentDelay.TotalMilliseconds * backoffFactor);
                }
            }
        }

        return (false, default, lastException);
    }

    #endregion
}
