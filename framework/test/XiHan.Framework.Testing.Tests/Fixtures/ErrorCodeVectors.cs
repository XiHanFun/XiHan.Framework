// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Testing.Tests.Fixtures;

/// <summary>
/// 规范向量 <c>primitives/error-codes.json</c> 的对象模型
/// </summary>
/// <param name="Valid">合文法的错误码</param>
/// <param name="Invalid">不合文法的错误码</param>
internal sealed record ErrorCodeVectors(string[] Valid, string[] Invalid);
