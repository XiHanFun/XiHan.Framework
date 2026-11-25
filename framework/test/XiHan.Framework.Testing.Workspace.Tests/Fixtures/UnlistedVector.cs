// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Testing.Workspace.Tests.Fixtures;

/// <summary>
/// 不在源生成上下文里的向量模型
/// </summary>
/// <remarks>
/// <see cref="VectorJsonContext"/> 不认得它，也没有哪个登记的模型带着它：给按源生成读取时类型信息缺失的用例用
/// </remarks>
/// <param name="Name">名字</param>
internal sealed record UnlistedVector(string Name);
