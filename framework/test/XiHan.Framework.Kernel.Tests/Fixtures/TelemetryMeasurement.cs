// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 仪表上记下的一个值与它的标签
/// </summary>
/// <param name="Instrument">仪表名</param>
/// <param name="Value">值</param>
/// <param name="Tags">标签</param>
internal sealed record TelemetryMeasurement(string Instrument, double Value, IReadOnlyDictionary<string, object?> Tags);
