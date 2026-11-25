// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Testing.Workspace.Tests.Fixtures;

/// <summary>
/// 成员都由构造参数给出的向量模型
/// </summary>
/// <param name="Name">没有缺省值的构造参数</param>
/// <param name="Count">没有缺省值的构造参数</param>
internal sealed record ConstructorVector(string Name, int Count);
