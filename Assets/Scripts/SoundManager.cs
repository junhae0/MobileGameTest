using System;
using UnityEngine;

/// <summary>
/// 직접 만든 사운드 파일로 바꾸고 싶을 때 GameManager 인스펙터의 Custom Sounds 에 넣으세요.
/// 비어 있는 칸은 코드로 자동 생성한 효과음/배경음을 사용합니다.
/// </summary>
[Serializable]
public class SoundClips
{
    public AudioClip bgm, engineLoop, chargeLoop;
    public AudioClip lane, jump, boost, hit, smash, coin, countdown, go, clear, fail, click;
}

public enum Sfx { Lane, Jump, Boost, Hit, Smash, Coin, Countdown, Go, Clear, Fail, Click }

/// <summary>
/// 배경음, 엔진음(속도에 따라 음높이 변화), 차징음(게이지에 따라 음높이 상승), 효과음을 관리합니다.
/// 음악/효과음 ON·OFF 설정은 저장됩니다.
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager I { get; private set; }

    const float MusicBase = 0.5f;   // 음악 볼륨 100% 일 때 실제 볼륨 (효과음보다 작게)
    float musicVol, sfxVol;
    float lastPreviewTime;

    AudioSource bgmSrc, engineSrc, chargeSrc, sfxSrc;
    readonly AudioClip[] sfx = new AudioClip[Enum.GetValues(typeof(Sfx)).Length];

    /// <summary>음악 볼륨 0 ~ 1 (저장됨)</summary>
    public float MusicVolume
    {
        get => musicVol;
        set
        {
            musicVol = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat("opt_music_vol", musicVol);
            ApplyMusic();
        }
    }

    /// <summary>효과음 볼륨 0 ~ 1 (저장됨)</summary>
    public float SfxVolume
    {
        get => sfxVol;
        set
        {
            sfxVol = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat("opt_sfx_vol", sfxVol);
            // 슬라이더를 움직이는 동안 크기를 바로 들어볼 수 있게 미리듣기
            if (Time.unscaledTime - lastPreviewTime > 0.15f)
            {
                lastPreviewTime = Time.unscaledTime;
                Play(Sfx.Coin, 0.7f);
            }
        }
    }

    public void SaveSettings() => PlayerPrefs.Save();

    public void Init(SoundClips custom)
    {
        I = this;
        if (custom == null) custom = new SoundClips();
        musicVol = PlayerPrefs.GetFloat("opt_music_vol", 0.7f);
        sfxVol = PlayerPrefs.GetFloat("opt_sfx_vol", 0.9f);

        bgmSrc = NewSource(true);
        engineSrc = NewSource(true);
        chargeSrc = NewSource(true);
        sfxSrc = NewSource(false);

        bgmSrc.clip = Pick(custom.bgm, Synth.Bgm);
        engineSrc.clip = Pick(custom.engineLoop, Synth.EngineLoop);
        chargeSrc.clip = Pick(custom.chargeLoop, Synth.ChargeLoop);

        sfx[(int)Sfx.Lane] = Pick(custom.lane, Synth.Whoosh);
        sfx[(int)Sfx.Jump] = Pick(custom.jump, Synth.Jump);
        sfx[(int)Sfx.Boost] = Pick(custom.boost, Synth.Boost);
        sfx[(int)Sfx.Hit] = Pick(custom.hit, Synth.Hit);
        sfx[(int)Sfx.Smash] = Pick(custom.smash, Synth.Smash);
        sfx[(int)Sfx.Coin] = Pick(custom.coin, Synth.Coin);
        sfx[(int)Sfx.Countdown] = Pick(custom.countdown, Synth.Beep);
        sfx[(int)Sfx.Go] = Pick(custom.go, Synth.Go);
        sfx[(int)Sfx.Clear] = Pick(custom.clear, Synth.Clear);
        sfx[(int)Sfx.Fail] = Pick(custom.fail, Synth.Fail);
        sfx[(int)Sfx.Click] = Pick(custom.click, Synth.Click);

        bgmSrc.volume = musicVol * MusicBase;
        ApplyMusic();
    }

    static AudioClip Pick(AudioClip c, Func<AudioClip> generate) => c != null ? c : generate();

    AudioSource NewSource(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 0f;
        return s;
    }

    void ApplyMusic()
    {
        if (bgmSrc == null) return;
        if (musicVol > 0.001f) { if (!bgmSrc.isPlaying) bgmSrc.Play(); }
        else bgmSrc.Pause();
    }

    /// <summary>어디서든 SoundManager.Play(Sfx.Jump) 처럼 호출</summary>
    public static void Play(Sfx s, float volume = 1f)
    {
        if (I == null || I.sfxVol <= 0.001f) return;
        var clip = I.sfx[(int)s];
        if (clip != null) I.sfxSrc.PlayOneShot(clip, volume * I.sfxVol);
    }

    void Update()
    {
        var gm = GameManager.I;
        if (gm == null || gm.Player == null) return;
        float dt = Time.unscaledDeltaTime;

        // 엔진음: 카운트다운 중엔 공회전, 주행 중엔 속도에 따라 음높이 변화
        bool engineOn = sfxVol > 0.001f && !gm.IsPaused &&
                        (gm.State == GameState.Playing || gm.State == GameState.Countdown);
        if (engineOn)
        {
            if (!engineSrc.isPlaying) { engineSrc.pitch = 0.5f; engineSrc.Play(); }
            float ratio = gm.State == GameState.Playing
                ? gm.CurrentSpeed / Mathf.Max(0.01f, gm.CurrentStage.baseSpeed)
                : 0f;
            engineSrc.pitch = Mathf.Lerp(engineSrc.pitch, 0.5f + 0.5f * ratio, dt * 6f);
            engineSrc.volume = 0.2f * sfxVol;
        }
        else if (engineSrc.isPlaying) engineSrc.Stop();

        // 차징음: 게이지가 찰수록 음이 올라감
        var p = gm.Player;
        bool chargeOn = engineOn && gm.State == GameState.Playing && p.IsCharging;
        if (chargeOn)
        {
            if (!chargeSrc.isPlaying) chargeSrc.Play();
            chargeSrc.pitch = 0.7f + p.Charge * 1.3f;
            chargeSrc.volume = (0.1f + 0.15f * p.Charge) * sfxVol;
        }
        else if (chargeSrc.isPlaying) chargeSrc.Stop();

        // 일시정지 중엔 배경음 작게
        float targetBgm = musicVol * MusicBase * (gm.IsPaused ? 0.5f : 1f);
        bgmSrc.volume = Mathf.MoveTowards(bgmSrc.volume, targetBgm, dt * 2f);
    }
}

/// <summary>
/// 사운드 파일 없이 코드로 8비트 느낌의 소리를 만들어 주는 간단한 신시사이저.
/// </summary>
public static class Synth
{
    const int Rate = 22050;
    static readonly System.Random rng = new System.Random(1234);

    enum Wave { Sine, Square, Saw, Triangle }

    static float Rand() => (float)(rng.NextDouble() * 2.0 - 1.0);
    static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);
    static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

    static float Osc(Wave w, float phase, float duty)
    {
        switch (w)
        {
            case Wave.Sine: return Mathf.Sin(phase * 2f * Mathf.PI);
            case Wave.Square: return phase < duty ? 1f : -1f;
            case Wave.Saw: return phase * 2f - 1f;
            default: return 1f - 4f * Mathf.Abs(phase - 0.5f);
        }
    }

    static void Tone(float[] buf, float start, float dur, float f0, float f1, Wave w, float vol,
                     float decay = 0f, float attack = 0.005f, float release = 0.04f, float duty = 0.5f)
    {
        int s0 = (int)(start * Rate);
        int n = (int)(dur * Rate);
        double phase = 0;
        for (int i = 0; i < n && s0 + i < buf.Length; i++)
        {
            float time = i / (float)Rate;
            float f = Mathf.Lerp(f0, f1, i / (float)n);
            phase += f / Rate;
            phase -= Math.Floor(phase);
            float env = Mathf.Min(1f, time / attack) * Mathf.Clamp01((dur - time) / release);
            if (decay > 0f) env *= Mathf.Exp(-time * decay);
            buf[s0 + i] += Osc(w, (float)phase, duty) * vol * env;
        }
    }

    static void Noise(float[] buf, float start, float dur, float vol, float cutoff,
                      float decay = 0f, float attack = 0.003f, float release = 0.03f)
    {
        int s0 = (int)(start * Rate);
        int n = (int)(dur * Rate);
        float lp = 0f;
        for (int i = 0; i < n && s0 + i < buf.Length; i++)
        {
            float time = i / (float)Rate;
            lp += (Rand() - lp) * cutoff; // 간단한 로우패스 필터
            float env = Mathf.Min(1f, time / attack) * Mathf.Clamp01((dur - time) / release);
            if (decay > 0f) env *= Mathf.Exp(-time * decay);
            buf[s0 + i] += lp * vol * env;
        }
    }

    static AudioClip Make(string name, float[] data)
    {
        float max = 0f;
        foreach (float v in data) max = Mathf.Max(max, Mathf.Abs(v));
        if (max > 0.95f) for (int i = 0; i < data.Length; i++) data[i] *= 0.95f / max;
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ───────── 효과음 ─────────
    public static AudioClip Whoosh()
    {
        var b = Buffer(0.16f);
        Noise(b, 0, 0.16f, 0.9f, 0.25f, 0f, 0.06f, 0.1f);
        return Make("Lane", b);
    }

    public static AudioClip Jump()
    {
        var b = Buffer(0.2f);
        Tone(b, 0, 0.2f, 320f, 780f, Wave.Square, 0.25f, 0f, 0.005f, 0.07f, 0.25f);
        return Make("Jump", b);
    }

    public static AudioClip Boost()
    {
        var b = Buffer(0.6f);
        Noise(b, 0, 0.6f, 1.2f, 0.35f, 4f);
        Tone(b, 0, 0.5f, 180f, 950f, Wave.Saw, 0.25f, 0f, 0.01f, 0.15f);
        return Make("Boost", b);
    }

    public static AudioClip Hit()
    {
        var b = Buffer(0.45f);
        Noise(b, 0, 0.45f, 2.2f, 0.15f, 9f);
        Tone(b, 0, 0.3f, 150f, 45f, Wave.Square, 0.3f, 8f);
        return Make("Hit", b);
    }

    public static AudioClip Smash()
    {
        var b = Buffer(0.3f);
        Noise(b, 0, 0.3f, 1.2f, 0.45f, 10f);
        Tone(b, 0, 0.22f, 700f, 160f, Wave.Square, 0.2f, 10f);
        return Make("Smash", b);
    }

    public static AudioClip Coin()
    {
        var b = Buffer(0.3f);
        Tone(b, 0, 0.07f, 988f, 988f, Wave.Square, 0.16f, 0f, 0.002f, 0.01f);
        Tone(b, 0.07f, 0.22f, 1319f, 1319f, Wave.Square, 0.16f, 6f, 0.002f, 0.08f);
        return Make("Coin", b);
    }

    public static AudioClip Beep()
    {
        var b = Buffer(0.16f);
        Tone(b, 0, 0.15f, 880f, 880f, Wave.Sine, 0.4f, 0f, 0.005f, 0.05f);
        Tone(b, 0, 0.15f, 880f, 880f, Wave.Square, 0.06f, 0f, 0.005f, 0.05f);
        return Make("Countdown", b);
    }

    public static AudioClip Go()
    {
        var b = Buffer(0.45f);
        Tone(b, 0, 0.42f, 1320f, 1320f, Wave.Sine, 0.4f, 2f, 0.005f, 0.12f);
        Tone(b, 0, 0.42f, 660f, 660f, Wave.Square, 0.1f, 2f, 0.005f, 0.12f);
        return Make("Go", b);
    }

    public static AudioClip Clear()
    {
        var b = Buffer(1.0f);
        int[] notes = { 72, 76, 79, 84 };
        for (int i = 0; i < notes.Length; i++)
        {
            float dur = i == notes.Length - 1 ? 0.55f : 0.12f;
            float f = Midi(notes[i]);
            Tone(b, i * 0.11f, dur, f, f, Wave.Square, 0.18f, i == 3 ? 2.5f : 0f, 0.005f, 0.05f, 0.25f);
        }
        Tone(b, 0.33f, 0.6f, Midi(48), Midi(48), Wave.Triangle, 0.3f, 2.5f);
        return Make("Clear", b);
    }

    public static AudioClip Fail()
    {
        var b = Buffer(1.1f);
        Tone(b, 0.0f, 0.2f, Midi(67), Midi(67), Wave.Triangle, 0.35f);
        Tone(b, 0.2f, 0.2f, Midi(64), Midi(64), Wave.Triangle, 0.35f);
        Tone(b, 0.4f, 0.65f, Midi(60), Midi(55), Wave.Triangle, 0.35f, 1.5f, 0.005f, 0.2f);
        Tone(b, 0.4f, 0.65f, Midi(48), Midi(43), Wave.Square, 0.07f, 1.5f, 0.005f, 0.2f);
        return Make("Fail", b);
    }

    public static AudioClip Click()
    {
        var b = Buffer(0.06f);
        Tone(b, 0, 0.05f, 1100f, 1100f, Wave.Sine, 0.3f, 0f, 0.002f, 0.03f);
        return Make("Click", b);
    }

    // ───────── 반복 재생용 ─────────
    // 주파수 × 길이가 정수가 되도록 맞춰서 이음새 없이 반복됨
    public static AudioClip EngineLoop()
    {
        const float dur = 0.5f;
        var b = Buffer(dur);
        double p1 = 0, p2 = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate;
            p1 += 70.0 / Rate; p1 -= Math.Floor(p1);
            p2 += 140.0 / Rate; p2 -= Math.Floor(p2);
            float am = 0.75f + 0.25f * Mathf.Sin(t * 24f * 2f * Mathf.PI);
            b[i] = (Osc(Wave.Square, (float)p1, 0.3f) * 0.25f + Osc(Wave.Saw, (float)p2, 0.5f) * 0.12f) * am;
        }
        return Make("EngineLoop", b);
    }

    public static AudioClip ChargeLoop()
    {
        const float dur = 0.5f;
        var b = Buffer(dur);
        double p1 = 0, p2 = 0;
        for (int i = 0; i < b.Length; i++)
        {
            float t = i / (float)Rate;
            float vib = 1f + 0.02f * Mathf.Sin(t * 8f * 2f * Mathf.PI);
            p1 += 220.0 * vib / Rate; p1 -= Math.Floor(p1);
            p2 += 330.0 * vib / Rate; p2 -= Math.Floor(p2);
            b[i] = Osc(Wave.Saw, (float)p1, 0.5f) * 0.2f + Osc(Wave.Square, (float)p2, 0.5f) * 0.08f;
        }
        return Make("ChargeLoop", b);
    }

    // ───────── 배경음 (150BPM, 8마디 반복) ─────────
    public static AudioClip Bgm()
    {
        const float eighth = 0.2f; // 150 BPM 8분음표
        int[][] melody =
        {
            new[] { 76, -1, 74, 76, 72, -1, 69, -1 },
            new[] { 72, -1, 74, 72, 69, -1, 67, 69 },
            new[] { 72, -1, 76, 79, 76, -1, 74, 72 },
            new[] { 74, -1, -1, 71, 74, -1, 79, -1 },
            new[] { 81, -1, 79, 76, 79, -1, 76, 74 },
            new[] { 72, 74, 76, -1, 77, -1, 76, 74 },
            new[] { 76, -1, 72, -1, 79, -1, 76, 72 },
            new[] { 74, -1, 71, -1, 67, -1, -1, -1 },
        };
        int[] bassRoot = { 45, 41, 48, 43, 45, 41, 48, 43 }; // Am F C G

        var b = Buffer(melody.Length * 8 * eighth);
        for (int bar = 0; bar < melody.Length; bar++)
        {
            for (int s = 0; s < 8; s++)
            {
                float t = (bar * 8 + s) * eighth;

                int m = melody[bar][s];
                if (m > 0)
                {
                    float f = Midi(m);
                    Tone(b, t, eighth * 0.9f, f, f, Wave.Square, 0.1f, 3f, 0.005f, 0.04f, 0.25f);
                }

                float bf = Midi(bassRoot[bar] + (s % 2 == 0 ? 0 : 12));
                Tone(b, t, eighth * 0.95f, bf, bf, Wave.Triangle, 0.22f, 2f, 0.005f, 0.03f);

                if (s == 0 || s == 4) Tone(b, t, 0.14f, 120f, 45f, Wave.Sine, 0.5f, 20f);      // 킥
                if (s == 2 || s == 6) Noise(b, t, 0.13f, 0.6f, 0.6f, 22f);                   // 스네어
                if (s % 2 == 1) Noise(b, t, 0.04f, 0.18f, 0.95f, 60f);                       // 하이햇
            }
        }
        return Make("BGM", b);
    }
}
