// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Conformance;

/// <summary>
/// 确定性的词袋哈希嵌入：每个词按 SHA-256 落到一个维度（正负号由哈希决定），向量归一化；词重叠越多余弦相似度越高。只用于测试与一致性套件。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class HashingEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    /// <summary>
    /// 默认维度。
    /// </summary>
    public const int DefaultDimensions = 64;

    /// <summary>
    /// 分词用的分隔符。
    /// </summary>
    private static readonly char[] _separators = [' ', '\n', '\r', '\t', ',', '.', ';', ':', '!', '?', '(', ')', '[', ']', '"', '\''];

    /// <summary>
    /// 维度。
    /// </summary>
    private readonly int _dimensions;

    /// <summary>
    /// 创建。
    /// </summary>
    public HashingEmbeddingGenerator(int dimensions = DefaultDimensions)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(dimensions, 1);
        _dimensions = dimensions;
    }

    /// <summary>
    /// 已生成的次数（供测试断言缓存与批量）。
    /// </summary>
    public int Calls { get; private set; }

    /// <summary>
    /// 一段文本的向量。
    /// </summary>
    public float[] Embed(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var vector = new float[_dimensions];
        foreach (var token in text.ToLowerInvariant().Split(_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            var index = (int)(BitConverter.ToUInt32(hash, 0) % (uint)_dimensions);
            vector[index] += (hash[4] & 1) == 0 ? 1f : -1f;
        }

        var norm = MathF.Sqrt(vector.Sum(static v => v * v));
        if (norm > 0)
        {
            for (var i = 0; i < vector.Length; i++)
            {
                vector[i] /= norm;
            }
        }

        return vector;
    }

    /// <inheritdoc />
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        Calls++;
        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(values.Select(value => new Embedding<float>(Embed(value)))));
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is null && serviceType?.IsInstanceOfType(this) == true ? this : null;

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
