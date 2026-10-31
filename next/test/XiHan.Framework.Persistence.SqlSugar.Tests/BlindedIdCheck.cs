// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 让下一条按某个标识的查询查不到它。
/// </summary>
/// <remarks>
/// 模拟「仓储核对标识时对方尚未提交、插入时对方已提交」：SQLite 单写者，本事务持锁时别的连接写不进来，所以先提交一行，再让仓储新增前的核对查不到它——之后的插入撞上真实的主键约束，另开连接的核对已不受影响，能查到它。挂在 SqlSugar 的 <c>OnExecutingChangeSql</c> 上，命中一次就解除：把查询文本与参数里的该标识换成随机标识。
/// </remarks>
internal sealed class BlindedIdCheck
{
    /// <summary>
    /// 等着被藏起来的标识。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 表示未启用。
    /// </remarks>
    private Guid? _armed;

    /// <summary>
    /// 是否已经藏过一次。
    /// </summary>
    public bool Fired { get; private set; }

    /// <summary>
    /// 让下一条按 <paramref name="id"/> 的查询查不到它。
    /// </summary>
    public void Arm(Guid id) => _armed = id;

    /// <summary>
    /// 作为 <c>AopEvents.OnExecutingChangeSql</c> 的回调。
    /// </summary>
    public KeyValuePair<string, SugarParameter[]> Rewrite(string sql, SugarParameter[] parameters)
    {
        if (_armed is not { } id || !sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return new(sql, parameters);
        }

        var decoy = Guid.NewGuid();
        var hit = false;
        foreach (var parameter in parameters)
        {
            if (parameter.Value is Guid guid && guid == id)
            {
                parameter.Value = decoy;
                hit = true;
            }
            else if (parameter.Value is string text && Guid.TryParse(text, out var parsed) && parsed == id)
            {
                parameter.Value = decoy.ToString();
                hit = true;
            }
        }

        foreach (var form in new[] { id.ToString(), id.ToString().ToUpperInvariant() })
        {
            if (sql.Contains(form, StringComparison.Ordinal))
            {
                sql = sql.Replace(form, decoy.ToString(), StringComparison.Ordinal);
                hit = true;
            }
        }

        if (hit)
        {
            _armed = null;
            Fired = true;
        }

        return new(sql, parameters);
    }
}
