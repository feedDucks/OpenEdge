using NAudio.Wave;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Speech.Synthesis;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OpenEdge.helper;

internal sealed class TtsService
{
    private readonly object gate = new object();
    private SpeechSynthesizer synth;
    private bool initFailed;

    private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly string XttsDir = Path.Combine(RuntimePaths.RuntimeRoot, "tts", "xtts");
    private static readonly string XttsPython = Path.Combine(XttsDir, "venv", "Scripts", "pythonw.exe");
    private static readonly string XttsScript = Path.Combine(XttsDir, "tts_server.py");
    private static readonly string XttsSample = Path.Combine(XttsDir, "voice.wav");
    private static readonly string CacheDir = Path.Combine(RuntimePaths.RuntimeRoot, "tts", "cache");
    private const string XttsUrl = "http://127.0.0.1:5002";

    private int latest;
    private Process xttsProcess;
    private DateTime xttsCheckedAt = DateTime.MinValue;
    private bool xttsUp;

    private bool XttsAvailable()
    {
        if (!File.Exists(XttsPython) || !File.Exists(XttsScript) || !File.Exists(XttsSample)) return false;
        if ((DateTime.UtcNow - xttsCheckedAt).TotalSeconds < 10) return xttsUp;
        xttsCheckedAt = DateTime.UtcNow;
        try
        {
            using HttpClient quick = new HttpClient { Timeout = TimeSpan.FromMilliseconds(500) };
            xttsUp = quick.GetAsync(XttsUrl + "/health").GetAwaiter().GetResult().IsSuccessStatusCode;
        }
        catch { xttsUp = false; }
        if (!xttsUp && xttsProcess == null)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(XttsPython)
                {
                    WorkingDirectory = XttsDir,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add(XttsScript);
                psi.ArgumentList.Add(XttsSample);
                psi.ArgumentList.Add(Environment.ProcessId.ToString());
                xttsProcess = Process.Start(psi);
                SessionTraceLogger.Info("tts", "started XTTS server (model load takes a while; falling back meanwhile)");
            }
            catch (Exception ex) { SessionTraceLogger.Error("tts", "could not start XTTS server", ex); }
        }
        return xttsUp;
    }

    private byte[] RenderXtts(string text)
    {
        if (!XttsAvailable()) return null;
        try
        {
            // cache key includes the sample's timestamp so changing voice.wav invalidates it
            string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text + "|" + File.GetLastWriteTimeUtc(XttsSample).Ticks)));
            string cached = Path.Combine(CacheDir, key + ".wav");
            if (File.Exists(cached)) return File.ReadAllBytes(cached);

            string json = JsonSerializer.Serialize(new { text = text.Replace("\r", " ").Replace("\n", ". "), speed = 1.0 });
            using StringContent body = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage resp = http.PostAsync(XttsUrl + "/tts", body).GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
            {
                SessionTraceLogger.Error("tts", "XTTS returned " + (int)resp.StatusCode);
                return null;
            }
            byte[] wav = resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            Directory.CreateDirectory(CacheDir);
            File.WriteAllBytes(cached, wav);
            return wav;
        }
        catch (Exception ex)
        {
            SessionTraceLogger.Error("tts", "XTTS request failed", ex);
            return null;
        }
    }

    private SpeechSynthesizer GetSynth()
    {
        if (synth != null) return synth;
        if (initFailed) return null;
        try
        {
            SpeechSynthesizer s = new SpeechSynthesizer();
            var voices = s.GetInstalledVoices().Where(v => v.Enabled).ToList();
            SessionTraceLogger.Info("tts", "installed voices: " + string.Join(", ", voices.Select(v => v.VoiceInfo.Name)));
            if (voices.Count == 0)
            {
                SessionTraceLogger.Error("tts", "no SAPI voices installed; TTS disabled");
                s.Dispose();
                initFailed = true;
                return null;
            }
            try { s.SelectVoiceByHints(VoiceGender.Female); }
            catch (Exception ex) { SessionTraceLogger.Info("tts", "no female voice, using default: " + ex.Message); }
            s.Rate = 0;
            synth = s;
            return s;
        }
        catch (Exception ex)
        {
            SessionTraceLogger.Error("tts", "synthesizer init failed; TTS disabled", ex);
            initFailed = true;
            return null;
        }
    }

    private byte[] RenderSapi(string text)
    {
        lock (gate)
        {
            SpeechSynthesizer s = GetSynth();
            if (s == null) return null;
            using MemoryStream ms = new MemoryStream();
            s.SetOutputToWaveStream(ms);
            s.Speak(text);
            s.SetOutputToNull();
            return ms.ToArray();
        }
    }

    // leftEar: true = left only, false = right only, null = both
    public void Speak(string text, float volume, bool? leftEar = null, bool dropIfStale = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        int ticket = Interlocked.Increment(ref latest);
        Task.Run(delegate
        {
            try
            {
                byte[] wav = RenderXtts(text) ?? RenderSapi(text);
                if (wav == null) return;
                if (dropIfStale && ticket != Volatile.Read(ref latest))
                {
                    SessionTraceLogger.Info("tts", "dropping stale TTS request for text=" + text);
                    return;
                }
                Play(wav, volume, leftEar);
            }
            catch (Exception ex)
            {
                SessionTraceLogger.Error("tts", "speak failed text=" + text, ex);
            }
        });
    }

    private static void Play(byte[] wav, float volume, bool? leftEar)
    {
        WaveFileReader reader = new WaveFileReader(new MemoryStream(wav));
        MonoToStereoProvider16 stereo = new MonoToStereoProvider16(reader);
        stereo.LeftVolume = leftEar == false ? 0f : volume;
        stereo.RightVolume = leftEar == true ? 0f : volume;
        WaveOutEvent output = new WaveOutEvent();
        output.PlaybackStopped += delegate { output.Dispose(); reader.Dispose(); };
        output.Init(stereo);
        output.Play();
    }
}