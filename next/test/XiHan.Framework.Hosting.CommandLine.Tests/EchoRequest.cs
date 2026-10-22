// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Hosting.CommandLine.Tests;

/// <summary>
/// 测试用请求：名字为 <c>fail</c> 时操作返回冲突。
/// </summary>
/// <param name="Name">名字。</param>
/// <param name="Page">页码；验证字符串到数字的绑定。</param>
/// <param name="Tags">标签；验证重复选项成数组。</param>
internal sealed record EchoRequest(string? Name, int Page = 0, string[]? Tags = null);
