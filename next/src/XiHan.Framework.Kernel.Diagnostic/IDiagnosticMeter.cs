// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Diagnostic;

/// <summary>
/// 诊断指标接口。
/// </summary>
[ApiLevel(Stability.Preview, "1.0")]
public interface IDiagnosticMeter
{
    /// <summary>
    /// 记录一个计数器增量。
    /// </summary>
    void Increment(string name, long value = 1, params (string Key, object? Value)[] tags);

    /// <summary>
    /// 记录一个测量值。
    /// </summary>
    void Record(string name, double value, params (string Key, object? Value)[] tags);
}
