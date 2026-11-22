using UnityEngine;

namespace Logistics
{
    public enum Sfx { Deliver, Place, Erase, Click, Sting, Warning, GameOver, Spawn, Deny }

    /// <summary>
    /// Synthesized audio (no asset downloads): delivery blip, construction thunks, UI click, weekly sting,
    /// warning, a city ambience bed and a soft generative pad. Volumes go through a static Music/SFX gain
    /// layer (headless floor — an AudioMixer with MusicVol/SFXVol is the editor-side upgrade).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        const int Rate = 44100;
        AudioSource music, ambient;
        AudioSource[] pool;
        int next;
        AudioClip[] clips;
        float[] lastPlayed;

        public float MusicVolume { get; private set; } = 0.6f;
        public float SfxVolume { get; private set; } = 0.8f;

        void Awake()
        {
            MusicVolume = PlayerPrefs.GetFloat("vol.music", 0.6f);
            SfxVolume = PlayerPrefs.GetFloat("vol.sfx", 0.8f);
            clips = new AudioClip[System.Enum.GetValues(typeof(Sfx)).Length];
            lastPlayed = new float[clips.Length];
            clips[(int)Sfx.Deliver] = Make("Deliver", 0.32f, Deliver);
            clips[(int)Sfx.Place] = Make("Place", 0.12f, Place);
            clips[(int)Sfx.Erase] = Make("Erase", 0.16f, Erase);
            clips[(int)Sfx.Click] = Make("Click", 0.03f, Click);
            clips[(int)Sfx.Sting] = Make("Sting", 0.75f, Sting);
            clips[(int)Sfx.Warning] = Make("Warning", 0.42f, Warning);
            clips[(int)Sfx.GameOver] = Make("GameOver", 1.4f, GameOverTune);
            clips[(int)Sfx.Spawn] = Make("Spawn", 0.5f, Spawn);
            clips[(int)Sfx.Deny] = Make("Deny", 0.14f, Deny);

            pool = new AudioSource[6];
            for (int i = 0; i < pool.Length; i++) pool[i] = NewSource("Sfx" + i, false);
            ambient = NewSource("Ambient", true);
            ambient.clip = MakeLoop("CityBed", 8f, CityBed);
            music = NewSource("Music", true);
            music.clip = MakeLoop("Pad", 32f, Pad);
            ApplyVolumes();
            ambient.Play();
            music.Play();
        }

        AudioSource NewSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        public void SetMusicVolume(float v) { MusicVolume = v; PlayerPrefs.SetFloat("vol.music", v); ApplyVolumes(); }
        public void SetSfxVolume(float v) { SfxVolume = v; PlayerPrefs.SetFloat("vol.sfx", v); ApplyVolumes(); }

        void ApplyVolumes()
        {
            if (music != null) music.volume = 0.22f * MusicVolume;
            if (ambient != null) ambient.volume = 0.35f * SfxVolume;
        }

        public void Play(Sfx s, float volume = 1f, float pitchJitter = 0.06f)
        {
            int i = (int)s;
            if (Time.unscaledTime - lastPlayed[i] < 0.045f) return;
            lastPlayed[i] = Time.unscaledTime;
            var src = pool[next];
            next = (next + 1) % pool.Length;
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.PlayOneShot(clips[i], volume * SfxVolume);
        }

        // ───────────────────────────── Synthesis ─────────────────────────────

        delegate float Gen(float t, float dur);

        static AudioClip Make(string name, float dur, Gen g)
        {
            int n = Mathf.CeilToInt(dur * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Min(1f, t / 0.005f) * Mathf.Min(1f, (dur - t) / 0.005f);
                data[i] = Mathf.Clamp(g(t, dur) * env, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeLoop(string name, float dur, Gen g)
        {
            int n = Mathf.CeilToInt(dur * Rate);
            int fade = Rate / 2;
            var data = new float[n + fade];
            for (int i = 0; i < data.Length; i++) data[i] = g(i / (float)Rate, dur);
            // crossfade the tail into the head for a seamless loop
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] = data[i] * k + data[n + i] * (1f - k);
            }
            var outData = new float[n];
            System.Array.Copy(data, outData, n);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(outData, 0);
            return clip;
        }

        static float Sin(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);
        static float Noise(int seed, float t) => Mathf.PerlinNoise(t * 9000f, seed) * 2f - 1f;
        static readonly System.Random rng = new System.Random(3);
        static float White() => (float)rng.NextDouble() * 2f - 1f;

        static float Deliver(float t, float d)
        {
            float f = Mathf.Lerp(880f, 1320f, Mathf.Clamp01(t / 0.09f));
            float a = Mathf.Exp(-t * 14f);
            float b = t > 0.08f ? Sin(1760f, t) * Mathf.Exp(-(t - 0.08f) * 12f) * 0.35f : 0f;
            return (Sin(f, t) * 0.5f + Sin(f * 2f, t) * 0.12f) * a + b;
        }

        static float lpState;
        static float Place(float t, float d)
        {
            lpState += (White() - lpState) * 0.25f;
            float thump = Sin(Mathf.Lerp(150f, 70f, t / d), t) * Mathf.Exp(-t * 32f);
            return lpState * Mathf.Exp(-t * 60f) * 0.8f + thump * 0.7f;
        }

        static float Erase(float t, float d)
        {
            lpState += (White() - lpState) * Mathf.Lerp(0.4f, 0.05f, t / d);
            return lpState * Mathf.Exp(-t * 18f) * 0.9f + Sin(Mathf.Lerp(300f, 120f, t / d), t) * 0.2f * Mathf.Exp(-t * 20f);
        }

        static float Click(float t, float d)
        {
            lpState += (White() - lpState) * 0.5f;
            return lpState * 0.6f + Sin(2400f, t) * 0.15f;
        }

        static float Sting(float t, float d)
        {
            float[] notes = { 523.25f, 659.25f, 783.99f };
            float s = 0f;
            for (int i = 0; i < 3; i++)
            {
                float st = i * 0.11f;
                if (t < st) continue;
                float lt = t - st;
                float env = Mathf.Min(1f, lt / 0.01f) * Mathf.Exp(-lt * 3.2f);
                s += (Sin(notes[i], lt) + 0.25f * Sin(notes[i] * 2f, lt) + 0.1f * Sin(notes[i] * 3f, lt)) * env * 0.33f;
            }
            return s;
        }

        static float Warning(float t, float d)
        {
            float s = 0f;
            for (int i = 0; i < 2; i++)
            {
                float st = i * 0.2f;
                if (t < st || t > st + 0.16f) continue;
                float lt = t - st;
                s += Sin(660f, lt) * Mathf.Exp(-lt * 10f) * 0.5f + Sin(990f, lt) * Mathf.Exp(-lt * 14f) * 0.15f;
            }
            return s;
        }

        static float GameOverTune(float t, float d)
        {
            float[] notes = { 392f, 329.63f, 261.63f };
            float s = 0f;
            for (int i = 0; i < 3; i++)
            {
                float st = i * 0.32f;
                if (t < st) continue;
                float lt = t - st;
                float env = Mathf.Min(1f, lt / 0.02f) * Mathf.Exp(-lt * 2.2f);
                s += (Sin(notes[i], lt) + 0.3f * Sin(notes[i] * 0.5f, lt)) * env * 0.35f;
            }
            return s;
        }

        static float Spawn(float t, float d)
        {
            float env = Mathf.Exp(-t * 7f);
            return (Sin(587.33f, t) * 0.4f + Sin(880f, t) * 0.25f + Sin(1174.66f, t) * 0.1f) * env;
        }

        static float Deny(float t, float d) => (Sin(196f, t) * 0.5f + Sin(207.65f, t) * 0.4f) * Mathf.Exp(-t * 14f);

        static float brown;
        static float CityBed(float t, float d)
        {
            brown = Mathf.Clamp(brown + White() * 0.02f, -1f, 1f) * 0.998f;
            float rumble = brown * 0.9f;
            float swell = Sin(55f, t) * 0.05f * (0.6f + 0.4f * Sin(0.125f, t));
            // occasional faint bird chirps
            float chirp = 0f;
            float ct = Mathf.Repeat(t, 2.7f);
            if (ct < 0.12f && Mathf.Floor(t / 2.7f) % 2 == 0)
                chirp = Sin(Mathf.Lerp(3200f, 4200f, ct / 0.12f), t) * 0.04f * Mathf.Sin(ct / 0.12f * Mathf.PI);
            return rumble * 0.5f + swell + chirp;
        }

        static float Pad(float t, float d)
        {
            // Cmaj7 → Am7 → Fmaj7 → G6, 8 s each, soft triangle-ish pads plus sparse pentatonic plucks.
            float[][] chords =
            {
                new[] { 130.81f, 196f, 246.94f, 329.63f },
                new[] { 110f, 164.81f, 196f, 261.63f },
                new[] { 87.31f, 174.61f, 220f, 329.63f },
                new[] { 98f, 146.83f, 246.94f, 329.63f },
            };
            int ci = Mathf.FloorToInt(t / 8f) % 4;
            float lt = Mathf.Repeat(t, 8f);
            float env = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, lt / 1.5f)) * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, (8f - lt) / 1.5f));
            float s = 0f;
            foreach (var f in chords[ci])
                s += (Sin(f, t) + 0.18f * Sin(f * 2f, t) + 0.07f * Sin(f * 3f, t)) * 0.16f;
            s *= 0.55f + 0.45f * env;
            float[] penta = { 523.25f, 587.33f, 659.25f, 783.99f, 880f };
            float step = Mathf.Repeat(t, 2f);
            int pi = (Mathf.FloorToInt(t / 2f) * 7 + ci * 3) % penta.Length;
            if (Mathf.FloorToInt(t / 2f) % 3 != 2)
                s += Sin(penta[pi], t) * Mathf.Exp(-step * 3f) * 0.12f;
            return s * 0.8f;
        }
    }
}
