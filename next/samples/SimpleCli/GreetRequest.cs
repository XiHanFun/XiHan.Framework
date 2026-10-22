// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace SimpleCli;

/// <summary>
/// 问候请求。
/// </summary>
/// <param name="Name">被问候者。</param>
/// <param name="Times">重复次数。</param>
public sealed record GreetRequest(string Name, int Times = 1);
