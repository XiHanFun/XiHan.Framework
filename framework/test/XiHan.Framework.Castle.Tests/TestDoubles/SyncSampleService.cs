// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Castle.Tests.TestDoubles;

/// <summary>
/// 同步样例服务契约
/// </summary>
public interface ISyncSampleService
{
    /// <summary>
    /// 把文本重复拼接指定次数
    /// </summary>
    /// <param name="text">被重复的文本</param>
    /// <param name="count">重复次数</param>
    /// <returns>拼接结果</returns>
    string Concat(string text, int count);

    /// <summary>
    /// 追加一条文本，无返回值
    /// </summary>
    /// <param name="text">文本</param>
    void Append(string text);

    /// <summary>
    /// 必定抛异常的同步方法
    /// </summary>
    /// <returns>永不返回</returns>
    int Fail();

    /// <summary>
    /// 泛型方法，返回泛型实参的类型名
    /// </summary>
    /// <typeparam name="TValue">泛型实参</typeparam>
    /// <param name="value">取值</param>
    /// <returns>泛型实参类型名</returns>
    string Describe<TValue>(TValue value);
}

/// <summary>
/// 同步样例服务
/// </summary>
public sealed class SyncSampleService : ISyncSampleService
{
    /// <summary>
    /// <see cref="Fail"/> 抛出的异常消息
    /// </summary>
    public const string FailureMessage = "同步样例故意失败";

    private readonly List<string> _appended = [];

    /// <summary>
    /// 已被追加的文本，用于验证目标方法是否真的被执行
    /// </summary>
    public IReadOnlyList<string> Appended => _appended;

    /// <summary>
    /// <see cref="Concat"/> 的实际执行次数，用于验证拦截器短路
    /// </summary>
    public int ConcatCallCount { get; private set; }

    /// <summary>
    /// 把文本重复拼接指定次数
    /// </summary>
    /// <param name="text">被重复的文本</param>
    /// <param name="count">重复次数</param>
    /// <returns>拼接结果</returns>
    public string Concat(string text, int count)
    {
        ConcatCallCount++;
        return string.Concat(Enumerable.Repeat(text, count));
    }

    /// <summary>
    /// 追加一条文本，无返回值
    /// </summary>
    /// <param name="text">文本</param>
    public void Append(string text)
    {
        _appended.Add(text);
    }

    /// <summary>
    /// 必定抛异常的同步方法
    /// </summary>
    /// <returns>永不返回</returns>
    public int Fail()
    {
        throw new InvalidOperationException(FailureMessage);
    }

    /// <summary>
    /// 泛型方法，返回泛型实参的类型名
    /// </summary>
    /// <typeparam name="TValue">泛型实参</typeparam>
    /// <param name="value">取值</param>
    /// <returns>泛型实参类型名</returns>
    public string Describe<TValue>(TValue value)
    {
        return typeof(TValue).Name;
    }
}
