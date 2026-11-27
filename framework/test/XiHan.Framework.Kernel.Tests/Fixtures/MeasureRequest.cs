// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 带浮点、文本与对象链的请求
/// </summary>
/// <remarks>
/// 类型化调用为 Schema 校验编码它时可能写不出：浮点是 NaN 或无穷，或对象链让请求超过写出深度。回声操作把它原样当作响应写出
/// </remarks>
/// <param name="Ratio">浮点值</param>
/// <param name="Nest">一层套一层的对象；没有为 <see langword="null"/></param>
/// <param name="Label">文本；没有为 <see langword="null"/></param>
internal sealed record MeasureRequest(double Ratio = 0, DepthNode? Nest = null, string? Label = null);
