// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 术语表条目：领域词及其在能力 ID、配置节、错误码域中的形态。
/// </summary>
/// <param name="Word">PascalCase 领域词。</param>
/// <param name="CapabilityPrefix">能力 ID 前缀；没有对应形态时为 <c>—</c>。</param>
/// <param name="ConfigSection">配置节；没有对应形态时为 <c>—</c>。</param>
/// <param name="ErrorDomain">错误码域；没有对应形态时为 <c>—</c>。</param>
internal sealed record VocabularyEntry(string Word, string CapabilityPrefix, string ConfigSection, string ErrorDomain);
