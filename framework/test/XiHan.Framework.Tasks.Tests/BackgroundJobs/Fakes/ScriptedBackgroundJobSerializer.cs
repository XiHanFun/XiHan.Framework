// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

/// <summary>
/// 可编排失败的作业参数序列化器替身
/// </summary>
/// <remarks>
/// 用于制造"反序列化失败"这类致命错误：Worker 对这类错误的处置与业务失败不同，
/// 必须直接放弃而不是进入退避重试。
/// </remarks>
public sealed class ScriptedBackgroundJobSerializer : IBackgroundJobSerializer
{
    private readonly object _gate = new();
    private int _deserializeCallCount;

    /// <summary>
    /// 反序列化时是否抛异常
    /// </summary>
    public bool ThrowOnDeserialize { get; set; }

    /// <summary>
    /// 反序列化调用次数
    /// </summary>
    public int DeserializeCallCount
    {
        get
        {
            lock (_gate)
            {
                return _deserializeCallCount;
            }
        }
    }

    /// <summary>
    /// 序列化
    /// </summary>
    /// <param name="obj">对象</param>
    /// <returns>序列化字符串</returns>
    public string Serialize(object obj)
    {
        return "{}";
    }

    /// <summary>
    /// 反序列化
    /// </summary>
    /// <param name="value">序列化字符串</param>
    /// <param name="type">目标类型</param>
    /// <returns>对象</returns>
    public object Deserialize(string value, Type type)
    {
        lock (_gate)
        {
            _deserializeCallCount++;
        }

        if (ThrowOnDeserialize)
        {
            throw new FormatException("模拟反序列化失败");
        }

        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"无法实例化类型：{type.FullName}");
    }
}
