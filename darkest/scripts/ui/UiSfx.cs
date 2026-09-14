using System;
using System.Collections.Generic;
using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 `ui_spec §12.2` **最小音效三类**（策划 `#321`⑥ 定的触发点 + 占位音类型）：
/// ```
/// ① 命中（**区分我方 / 敌方**）  ← `DamageEvent`（打敌人 / 打我方）
/// ② 受击 · 死门 · 阵亡          ← `DamageEvent`(我方被击) / `DeathDoorEvent` / `DeathEvent`
/// ③ 结算（胜 / 败）             ← 战斗结束（`BattleEndEvent` ⇒ UI 侧以 `GameOver` 跃迁为信号）
/// 占位音：命中=**方波短促**（我方高音 / 敌方低音）· 受击=**噪声** · 结算=**下行正弦**
/// ```
/// 🔴 **"没有音源时怎么办" —— 裁定 = 用【占位音】，不是静音**（`§12.2`）：
///   理由：静音会让"没做"和"做了但没声"**无法区分**（红线 21 同源）；而且**音源缺失时必须能跑** ✓
/// ⇒ 本类两手都做：**优先**读 `res://resources/audio/<kind>.ogg`（真音源掉落式替换：**放文件即生效**），
///   **没有就程序生成**占位音（**零外部文件依赖** ⇒ 永远能跑）✓
/// </summary>
public static class UiSfx
{
    /// <summary>音效类别（触发点见类注释；占位音参数集中在 <see cref="Generate"/>）。</summary>
    public enum Kind
    {
        /// <summary>① 命中（我方打出 ⇒ 高音方波）</summary>
        HitAlly,

        /// <summary>① 命中（敌方打出 ⇒ 低音方波）</summary>
        HitEnemy,

        /// <summary>② 受击（我方被击 ⇒ 噪声）</summary>
        Hurt,

        /// <summary>② 进死门</summary>
        DeathDoor,

        /// <summary>② 阵亡</summary>
        Death,

        /// <summary>③ 结算（胜 / 败 ⇒ 下行正弦）</summary>
        Settle,
    }

    private const int MixRate = 22050;
    private const int MaxVoices = 6;
    private const string AudioDir = "res://resources/audio/";

    private static readonly Dictionary<Kind, AudioStream> Streams = new();
    private static readonly List<AudioStreamPlayer> Voices = new();
    private static readonly Dictionary<Kind, int> Played = new();
    private static readonly Dictionary<Kind, string> Source = new();
    private static Node? _host;
    private static int _next;
    private static bool _ready;

    /// <summary>注入播放宿主（各屏在 `_Ready`/`Build` 里各调一次即可；播放器作为它的子节点 ⇒ 随场景回收）。</summary>
    public static void Attach(Node host)
    {
        _host = host;
        EnsureStreams();
    }

    /// <summary>播放（**任何情况下都不抛**：宿主不在/无音频设备时只记数，不影响玩法）✓</summary>
    public static void Play(Kind kind)
    {
        Played[kind] = Played.GetValueOrDefault(kind) + 1;
        EnsureStreams();

        if (_host is null || !GodotObject.IsInstanceValid(_host))
        {
            return;
        }

        AudioStreamPlayer player;
        if (Voices.Count < MaxVoices)
        {
            player = new AudioStreamPlayer { Name = $"Sfx{Voices.Count}" };
            _host.AddChild(player);
            Voices.Add(player);
        }
        else
        {
            player = Voices[_next++ % MaxVoices];
        }

        player.Stream = Streams[kind];
        player.Play();
    }

    /// <summary>🔴 可断言的取证行（冒烟打印）：每类各播了几次 + **音源出处**（真音源 / 程序生成占位）。</summary>
    public static string Audit()
    {
        EnsureStreams();
        var parts = new List<string>();
        foreach (Kind k in Enum.GetValues<Kind>())
        {
            parts.Add($"{Name(k)} {Played.GetValueOrDefault(k)}");
        }

        int real = 0;
        foreach (string s in Source.Values)
        {
            if (s.StartsWith("真音源", StringComparison.Ordinal))
            {
                real++;
            }
        }

        return $"音效（§12.2）：{string.Join(" ／ ", parts)}　｜　音源：真音源 {real} 类 ／ " +
               $"{Source.Count - real} 类为**程序生成占位音**（{AudioDir} 放 <kind>.ogg 即自动替换）" +
               $"　｜　播放器 {Voices.Count} 个（无外部文件依赖 ⇒ **音源缺失也能跑**）✓";
    }

    private static string Name(Kind k) => k switch
    {
        Kind.HitAlly => "命中(我)",
        Kind.HitEnemy => "命中(敌)",
        Kind.Hurt => "受击",
        Kind.DeathDoor => "死门",
        Kind.Death => "阵亡",
        _ => "结算",
    };

    private static void EnsureStreams()
    {
        if (_ready)
        {
            return;
        }

        _ready = true;
        foreach (Kind k in Enum.GetValues<Kind>())
        {
            string path = $"{AudioDir}{k}.ogg";
            if (ResourceLoader.Exists(path))
            {
                Streams[k] = ResourceLoader.Load<AudioStream>(path);
                Source[k] = $"真音源 {path}";
                continue;
            }

            // 🔴 没有真音源 ⇒ **占位音**（不是静音）：程序生成，零外部依赖 ✓
            Streams[k] = Generate(k);
            Source[k] = "占位音（程序生成）";
        }
    }

    /// <summary>占位音（`#321`⑥：命中=方波短促[我方高音/敌方低音] · 受击=噪声 · 结算=下行正弦）。</summary>
    private static AudioStreamWav Generate(Kind kind) => kind switch
    {
        Kind.HitAlly => Square(720f, 0.07f, 0.35f),
        Kind.HitEnemy => Square(320f, 0.09f, 0.35f),
        Kind.Hurt => Noise(0.10f, 0.30f),
        Kind.DeathDoor => Tone(220f, 0.35f, 110f, 0.35f),
        Kind.Death => Tone(180f, 0.45f, 70f, 0.35f),
        _ => Tone(520f, 0.40f, 180f, 0.30f),
    };

    /// <summary>方波（短促）。</summary>
    private static AudioStreamWav Square(float freq, float seconds, float amp)
    {
        int n = Samples(seconds);
        var data = new byte[n * 2];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            phase += 2 * Math.PI * freq / MixRate;
            float v = Math.Sin(phase) >= 0 ? 1f : -1f;
            Write(data, i, v * amp * Envelope(i, n));
        }

        return Wav(data);
    }

    /// <summary>正弦（可带下滑频：`to` &lt; `from` ⇒ 下行）。</summary>
    private static AudioStreamWav Tone(float from, float seconds, float to, float amp)
    {
        int n = Samples(seconds);
        var data = new byte[n * 2];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            phase += 2 * Math.PI * Mathf.Lerp(from, to, t) / MixRate;
            Write(data, i, (float)Math.Sin(phase) * amp * Envelope(i, n));
        }

        return Wav(data);
    }

    /// <summary>噪声（受击）。⚠️ 用**局部固定种子**的 `System.Random`，**不碰内核 RNG**（不产生任何抽取）✓</summary>
    private static AudioStreamWav Noise(float seconds, float amp)
    {
        int n = Samples(seconds);
        var rng = new Random(20260914);
        var data = new byte[n * 2];
        for (int i = 0; i < n; i++)
        {
            Write(data, i, (float)((rng.NextDouble() * 2 - 1) * amp * Envelope(i, n)));
        }

        return Wav(data);
    }

    private static int Samples(float seconds) => Math.Max(1, (int)(MixRate * seconds));

    /// <summary>线性衰减包络（"短促"）。</summary>
    private static float Envelope(int i, int n) => 1f - ((float)i / n);

    private static void Write(byte[] data, int i, float v)
    {
        short s = (short)(Mathf.Clamp(v, -1f, 1f) * short.MaxValue);
        data[i * 2] = (byte)(s & 0xFF);
        data[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
    }

    private static AudioStreamWav Wav(byte[] data) => new()
    {
        Format = AudioStreamWav.FormatEnum.Format16Bits,
        MixRate = MixRate,
        Stereo = false,
        Data = data,
    };
}
