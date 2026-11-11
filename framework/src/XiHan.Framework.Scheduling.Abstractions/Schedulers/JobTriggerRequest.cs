// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

namespace XiHan.Framework.Scheduling.Abstractions.Schedulers;

/// <summary>
/// 手动触发的请求
/// </summary>
/// <remarks>
/// 作业名经构造给出，其余成员经初始化器给出，以后加字段只加属性、不改构造签名（ADR-0142、ADR-0148）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JobTriggerRequest
{
    /// <summary>
    /// 创建手动触发的请求
    /// </summary>
    /// <param name="name">作业名</param>
    public JobTriggerRequest(JobName name)
    {
        Name = name;
    }

    /// <summary>
    /// 作业名
    /// </summary>
    public JobName Name { get; }

    /// <summary>
    /// 覆盖作业数据的数据
    /// </summary>
    /// <remarks>
    /// 不覆盖为 <see langword="null"/>
    /// </remarks>
    public JsonObject? Data { get; init; }

    /// <summary>
    /// 从作业名隐式转换
    /// </summary>
    public static implicit operator JobTriggerRequest(JobName name) => new(name);

    /// <summary>
    /// 从作业名创建
    /// </summary>
    public static JobTriggerRequest FromName(JobName name) => new(name);
}
