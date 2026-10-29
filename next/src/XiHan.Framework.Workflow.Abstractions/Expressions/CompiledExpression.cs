// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;

namespace XiHan.Framework.Workflow.Abstractions.Expressions;

/// <summary>
/// 编译好的表达式。
/// </summary>
/// <remarks>
/// 在作用域上求值；运行期错误抛 <see cref="ExpressionException"/>。
/// </remarks>
internal delegate JsonNode? CompiledExpression(ExpressionScope scope);
