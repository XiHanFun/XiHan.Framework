// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 枚举载荷用例的请求
/// </summary>
/// <param name="Level">级别</param>
/// <param name="Mode">模式</param>
/// <param name="Levels">级别列表</param>
/// <param name="Count">数量</param>
internal sealed record ShapeRequest(ShapeLevel Level, ShapeMode Mode, List<ShapeLevel>? Levels, int Count);
