// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 术语表条目：领域词及其在能力 ID、配置节、错误码域中的形态。
/// </summary>
internal sealed record VocabularyEntry(string Word, string CapabilityPrefix, string ConfigSection, string ErrorDomain);
