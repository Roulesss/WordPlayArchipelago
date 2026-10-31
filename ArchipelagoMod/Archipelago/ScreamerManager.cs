using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace WordPlayArchipelago.Archipelago
{
    public class ScreamerManager : MonoBehaviour
    {
        [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool PlaySound(string? pszSound, IntPtr hmod, uint fdwSound);

        private const uint SND_ASYNC = 0x0001;
        private const uint SND_FILENAME = 0x00020000;

        public static ScreamerManager Instance { get; private set; } = null!;

        private readonly List<Texture2D> _gifFrames = new List<Texture2D>();
        private string? _gifPath;
        private string? _wavPath;

        private bool _isPlaying = false;
        private float _screamerTimer = 0f;
        private const float ScreamerDuration = 4.5f;
        private const float GifFps = 25f;

        private void Awake()
        {
            Instance = this;
            FindAssetPaths();
            LoadGifFrames();
            Plugin.Logger?.LogInfo("[Screamer] ScreamerManager initialized.");
        }

        private void FindAssetPaths()
        {
            string? baseDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
            string assetsDir = Path.Combine(baseDir ?? ".", "Assets");

            string gifPath = Path.Combine(assetsDir, "freddy.gif");
            if (File.Exists(gifPath))
            {
                _gifPath = gifPath;
            }

            string wavPath = Path.Combine(assetsDir, "fnaf-meme-har-har.wav");
            if (File.Exists(wavPath))
            {
                _wavPath = wavPath;
            }

            Plugin.Logger?.LogInfo($"[Screamer] Resolved GIF: '{_gifPath}', WAV: '{_wavPath}'");
        }

        private void LoadGifFrames()
        {
            if (string.IsNullOrEmpty(_gifPath) || !File.Exists(_gifPath))
            {
                Plugin.Logger?.LogWarning($"[Screamer] GIF file not found at '{_gifPath}'");
                return;
            }

            try
            {
                Assembly drawingAss;
                try
                {
                    drawingAss = Assembly.Load("System.Drawing");
                }
                catch
                {
                    string managedPath = Path.Combine(Application.dataPath, "Managed", "System.Drawing.dll");
                    drawingAss = Assembly.LoadFrom(managedPath);
                }

                Type imageType = drawingAss.GetType("System.Drawing.Image")!;
                Type frameDimType = drawingAss.GetType("System.Drawing.Imaging.FrameDimension")!;
                Type imageFormatType = drawingAss.GetType("System.Drawing.Imaging.ImageFormat")!;

                object pngFormat = imageFormatType.GetProperty("Png")!.GetValue(null)!;
                object timeDim = frameDimType.GetProperty("Time")!.GetValue(null)!;

                MethodInfo fromFileMethod = imageType.GetMethod("FromFile", new[] { typeof(string) })!;
                MethodInfo selectActiveFrame = imageType.GetMethod("SelectActiveFrame")!;
                MethodInfo getFrameCount = imageType.GetMethod("GetFrameCount")!;
                MethodInfo saveMethod = imageType.GetMethod("Save", new[] { typeof(Stream), imageFormatType })!;

                using IDisposable gif = (IDisposable)fromFileMethod.Invoke(null, new object[] { _gifPath })!;
                int frameCount = (int)getFrameCount.Invoke(gif, new object[] { timeDim })!;

                for (int i = 0; i < frameCount; i++)
                {
                    selectActiveFrame.Invoke(gif, new object[] { timeDim, i });
                    using var ms = new MemoryStream();
                    saveMethod.Invoke(gif, new object[] { ms, pngFormat });

                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    texture.LoadImage(ms.ToArray());
                    _gifFrames.Add(texture);
                }

                Plugin.Logger?.LogInfo($"[Screamer] Successfully loaded {_gifFrames.Count} GIF frames!");
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[Screamer] Failed to decode GIF: {ex.Message}");
            }
        }

        public void TriggerScreamer()
        {
            _isPlaying = true;
            _screamerTimer = ScreamerDuration;

            PlayAudio();

            Plugin.Logger?.LogWarning("[Trap] SCREAMER TRAP ACTIVATED!");
            Plugin.StaticUI?.ShowBanner("TRAP: SCREAMER!", Color.red, ScreamerDuration);
        }

        private void PlayAudio()
        {
            if (!string.IsNullOrEmpty(_wavPath) && File.Exists(_wavPath))
            {
                try
                {
                    bool ok = PlaySound(_wavPath, IntPtr.Zero, SND_ASYNC | SND_FILENAME);
                    Plugin.Logger?.LogInfo($"[Screamer] Win32 PlaySound started for '{_wavPath}' (Success: {ok})");
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogError($"[Screamer] PlaySound failed: {ex.Message}");
                }
            }
            else
            {
                Plugin.Logger?.LogWarning($"[Screamer] WAV file not found at '{_wavPath}'");
            }
        }

        private void StopAudio()
        {
            try
            {
                PlaySound(null, IntPtr.Zero, 0);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogWarning($"[Screamer] Error stopping PlaySound: {ex.Message}");
            }
        }

        private void Update()
        {
            if (_isPlaying)
            {
                _screamerTimer -= Time.unscaledDeltaTime;
                if (_screamerTimer <= 0f)
                {
                    _isPlaying = false;
                    StopAudio();
                    Plugin.Logger?.LogInfo("[Screamer] Screamer visual completed.");
                }
            }
        }

        private void OnGUI()
        {
            if (!_isPlaying || _gifFrames.Count == 0) return;

            GUI.depth = -9999;

            float elapsed = ScreamerDuration - _screamerTimer;
            int frameIndex = (int)(elapsed * GifFps) % _gifFrames.Count;
            Texture2D frame = _gifFrames[frameIndex];

            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), frame, ScaleMode.ScaleAndCrop);
        }

        private void OnDestroy()
        {
            StopAudio();
        }

        private void OnDisable()
        {
            StopAudio();
        }
    }
}
