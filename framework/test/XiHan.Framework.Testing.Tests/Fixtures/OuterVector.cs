// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Testing.Tests.Fixtures;

/// <summary>
/// 嵌着一个对象的向量模型
/// </summary>
/// <param name="Inner">嵌着的对象</param>
internal sealed record OuterVector(ConstructorVector Inner);
