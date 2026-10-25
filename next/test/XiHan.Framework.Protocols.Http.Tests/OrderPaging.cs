// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Protocols.Http.Tests;

/// <summary>
/// 测试用分页参数：作为嵌套对象出现在请求里。
/// </summary>
/// <param name="Index">页码。</param>
/// <param name="Size">每页条数。</param>
internal sealed record OrderPaging(int Index, int Size);
