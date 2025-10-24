#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqidsOptions
// Author:ZhaiFanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/05/10 17:44:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.DistributedIds.Sqids;

/// <summary>
/// Sqids配置选项
/// </summary>
public class SqidsOptions
{
    /// <summary>
    /// 默认字母表
    /// </summary>
    internal const string DefaultAlphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>
    /// 默认最小长度
    /// </summary>
    internal const int DefaultMinLength = 5;

    /// <summary>
    /// 默认屏蔽词列表
    /// </summary>
    private static readonly HashSet<string> DefaultBlocklist = new(StringComparer.OrdinalIgnoreCase)
    {
        // 常见的屏蔽词列表
        "0rgasm", "1d10t", "1d1ot", "1di0t", "1diot", "1njun", "4r5e", "5ex", "5h1t", "5hit", "a55", "anal", "anus",
        "ar5e", "arrse", "arse", "ass", "ass1", "asses", "assfukka", "asshol", "asshole", "asswhole", "a_s_s", "b00b",
        "b00bs", "b17ch", "b1tch", "ballbag", "ballsack", "bastard", "beastial", "beastiality", "bellend", "bestial",
        "bestiality", "bi7ch", "biatch", "bitch", "bitcher", "bitchers", "bitches", "bitchin", "bitching", "bloody",
        "blowjob", "blowjobs", "boiolas", "bollock", "bollok", "boner", "boob", "boobs", "booobs", "boooobs",
        "booooobs", "booooooobs", "breasts", "buceta", "bugger", "bullshit", "bum", "butt", "buttwhole", "buttmuch",
        "buttplug", "c0ck", "c0cksucker", "carpetmuncher", "cawk", "chink", "cipa", "cl1t", "clit", "clits", "cnut",
        "cock", "cockface", "cockhead", "cockmunch", "cockmuncher", "cocks", "cocksuck", "cocksucked", "cocksucker",
        "cocksucking", "cocksucks", "cocksuka", "cocksukka", "cok", "cokmuncher", "coksucka", "coon", "cox", "crap",
        "cum", "cummer", "cumming", "cums", "cumshot", "cunilingus", "cunillingus", "cunnilingus", "cunt", "cuntlick",
        "cuntlicker", "cuntlicking", "cunts", "cyalis", "cyberfuc", "cyberfuck", "cyberfucked", "cyberfucker",
        "cyberfuckers", "cyberfucking", "d1ck", "damn", "dick", "dickhead", "dildo", "dildos", "dink", "dinks",
        "dirsa", "dlck", "doggin", "dogging", "donkeyribber", "doosh", "duche", "dyke", "ejaculat", "ejakulate",
        "f4nny", "fag", "fagging", "faggitt", "faggot", "faggs", "fagot", "fagots", "fags", "fanny", "fannyflaps",
        "fannyfucker", "fanyy", "fatass", "fcuk", "fcuker", "fcuking", "feck", "fecker", "felching", "fellat",
        "fellatio", "fingerfuck", "fingerfucked", "fingerfucker", "fingerfuckers", "fingerfucking", "fingerfucks",
        "fistfuck", "fistfucked", "fistfucker", "fistfuckers", "fistfucking", "fistfuckings", "fistfucks", "flange",
        "fook", "fooker", "fuck", "fucka", "fucked", "fucker", "fuckers", "fuckhead", "fuckheads", "fuckin",
        "fucking", "fuckings", "fuckingshitmotherfucker", "fuckme", "fucks", "fuckwhit", "fuckwit", "fudgepacker",
        "fudgepacker", "fuk", "fuker", "fukker", "fukkin", "fuks", "fukwhit", "fukwit", "fux", "fux0r", "f_u_c_k",
        "gangbang", "gangbanged", "gangbangs", "gaylord", "gaysex", "goatse", "god", "goddamn", "goddamned",
        "hardcoresex", "hell", "heshe", "hoar", "hoare", "hoer", "homo", "hore", "horniest", "horny", "hotsex",
        "jackoff", "jap", "jerk", "jerkoff", "jism", "jiz", "jizm", "jizz", "kawk", "knob", "knobead", "knobed",
        "knobend", "knobhead", "knobjocky", "knobjokey", "kock", "kondum", "kondums", "kum", "kummer", "kumming",
        "kums", "kunilingus", "l3itch", "labia", "lmfao", "lust", "lusting", "m0f0", "m0fo", "m45terbate",
        "ma5terb8", "ma5terbate", "masochist", "masterb8", "masterbat", "masterbat3", "masterbate", "masterbation",
        "masterbations", "masturbate", "mof0", "mofo", "mothafuck", "mothafucka", "mothafuckas", "mothafuckaz",
        "mothafucked", "mothafucker", "mothafuckers", "mothafuckin", "mothafucking", "mothafuckings", "mothafucks",
        "motherfuck", "motherfucked", "motherfucker", "motherfuckers", "motherfuckin", "motherfucking",
        "motherfuckings", "motherfuckka", "motherfucks", "muff", "mutha", "muthafecker", "muthafuckker", "muther",
        "mutherfucker", "n1gga", "n1gger", "nazi", "nigg3r", "nigg4h", "nigga", "niggah", "niggas", "niggaz",
        "nigger", "niggers", "nob", "nobhead", "nobjocky", "nobjokey", "numbnuts", "nutsack", "orgasim", "orgasims",
        "orgasm", "orgasms", "p0rn", "pawn", "pecker", "penis", "penisfucker", "phonesex", "phuck", "phuk", "phuked",
        "phuking", "phukked", "phukking", "phuks", "phuq", "pigfucker", "pimpis", "piss", "pissed", "pisser",
        "pissers", "pisses", "pissflaps", "pissin", "pissing", "pissoff", "poop", "porn", "porno", "pornography",
        "pornos", "prick", "pricks", "pron", "pube", "pusse", "pussi", "pussies", "pussy", "pussys", "rectum",
        "retard", "rimjaw", "rimming", "shit", "shitdick", "shite", "shited", "shitey", "shitfuck", "shitfull",
        "shithead", "shiting", "shitings", "shits", "shitted", "shitter", "shitters", "shitting", "shittings",
        "shitty", "skank", "slut", "sluts", "smegma", "smut", "snatch", "sonofabitch", "spac", "spunk", "s_h_i_t",
        "t1tt1e5", "t1tties", "teets", "teez", "testical", "testicle", "tit", "titfuck", "tits", "titt", "tittie5",
        "tittiefucker", "titties", "tittyfuck", "tittywank", "titwank", "tosser", "turd", "tw4t", "twat", "twathead",
        "twatty", "twunt", "twunter", "v14gra", "v1gra", "vagina", "viagra", "vulva", "w00se", "wang", "wank",
        "wanker", "wanky", "whoar", "whore", "willies", "willy", "xrated", "xxx"
    };

    /// <summary>
    /// 字母表
    /// </summary>
    public string Alphabet { get; set; } = DefaultAlphabet;

    /// <summary>
    /// 生成唯一标识的最小长度
    /// </summary>
    public int MinLength { get; set; } = DefaultMinLength;

    /// <summary>
    /// 屏蔽词列表
    /// </summary>
    public HashSet<string> BlockList { get; set; } = new HashSet<string>(DefaultBlocklist, StringComparer.OrdinalIgnoreCase);
}
