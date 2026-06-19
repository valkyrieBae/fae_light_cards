using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Dalamud.Plugin.Services;

namespace FaeLightCards
{
    public class AudioManager : IDisposable
    {
        private readonly WaveOutEvent? outputDevice;
        private readonly MixingSampleProvider? mixer;
        private readonly string soundsDir;
        private readonly IPluginLog log;
        private readonly ICommandManager commandManager;
        private bool isDisposed;

        public AudioManager(string assemblyDir, IPluginLog log, ICommandManager commandManager)
        {
            this.log = log;
            this.commandManager = commandManager;
            this.soundsDir = Path.Combine(assemblyDir, "sounds");

            try
            {
                var mixerFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
                this.mixer = new MixingSampleProvider(mixerFormat) { ReadFully = true };
                this.outputDevice = new WaveOutEvent();
                this.outputDevice.Init(this.mixer);
                this.outputDevice.Play();
                this.log.Information("AudioManager initialized successfully with NAudio.");
            }
            catch (Exception ex)
            {
                this.log.Error(ex, "Failed to initialize NAudio AudioManager.");
            }
        }

        public void PlaySound(string soundFile)
        {
            if (string.IsNullOrEmpty(soundFile) || soundFile.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Handle in-game sound effect placeholders
            if (soundFile.StartsWith("game:", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(soundFile.AsSpan(5), out int seId) && seId >= 1 && seId <= 16)
                {
                    try
                    {
                        this.log.Debug($"Playing native FFXIV chat sound effect ID: {seId}");
                        unsafe
                        {
                            FFXIVClientStructs.FFXIV.Client.UI.UIGlobals.PlayChatSoundEffect((uint)seId);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.log.Error(ex, $"Failed to play native FFXIV chat sound effect ID: {seId}");
                    }
                }
                return;
            }

            // Handle in-game system sound effect IDs
            if (soundFile.StartsWith("sfx:", StringComparison.OrdinalIgnoreCase))
            {
                if (uint.TryParse(soundFile.AsSpan(4), out uint sfxId))
                {
                    try
                    {
                        this.log.Debug($"Playing native FFXIV system sound effect ID: {sfxId}");
                        unsafe
                        {
                            FFXIVClientStructs.FFXIV.Client.UI.UIGlobals.PlaySoundEffect(sfxId);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.log.Error(ex, $"Failed to play native FFXIV system sound effect ID: {sfxId}");
                    }
                }
                return;
            }

            if (this.outputDevice == null || this.mixer == null)
            {
                this.log.Warning("AudioManager is not initialized. Cannot play custom sound.");
                return;
            }

            try
            {
                string soundPath = Path.Combine(this.soundsDir, soundFile);
                if (!File.Exists(soundPath))
                {
                    this.log.Warning($"Sound file not found: {soundPath}");
                    return;
                }

                // Load and play the sound asynchronously
                Task.Run(() =>
                {
                    try
                    {
                        var reader = new AudioFileReader(soundPath);
                        var converted = ConvertToMixerFormat(reader, this.mixer.WaveFormat);
                        var autoDispose = new AutoDisposeSampleProvider(converted, reader);

                        lock (this.mixer)
                        {
                            this.mixer.AddMixerInput(autoDispose);
                        }
                        this.log.Debug($"Queued custom sound to NAudio mixer: {soundFile}");
                    }
                    catch (Exception ex)
                    {
                        this.log.Error(ex, $"Error loading or playing sound file: {soundPath}");
                    }
                });
            }
            catch (Exception ex)
            {
                this.log.Error(ex, $"Failed to start custom sound play task: {soundFile}");
            }
        }

        private static ISampleProvider ConvertToMixerFormat(ISampleProvider input, WaveFormat mixerFormat)
        {
            ISampleProvider current = input;

            // 1. Resample if necessary
            if (current.WaveFormat.SampleRate != mixerFormat.SampleRate)
            {
                current = new WdlResamplingSampleProvider(current, mixerFormat.SampleRate);
            }

            // 2. Convert channels if necessary (mono to stereo)
            if (current.WaveFormat.Channels != mixerFormat.Channels)
            {
                if (current.WaveFormat.Channels == 1 && mixerFormat.Channels == 2)
                {
                    current = new MonoToStereoSampleProvider(current);
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported channel conversion from {current.WaveFormat.Channels} to {mixerFormat.Channels}");
                }
            }

            return current;
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            try
            {
                this.outputDevice?.Stop();
                this.outputDevice?.Dispose();
                this.log.Information("AudioManager disposed successfully.");
            }
            catch (Exception ex)
            {
                this.log.Error(ex, "Error disposing AudioManager.");
            }

            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Wraps an ISampleProvider and disposes the underlying IDisposable resource
    /// when the audio reaches its end.
    /// </summary>
    public class AutoDisposeSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly IDisposable disposable;
        private bool isDisposed;

        public AutoDisposeSampleProvider(ISampleProvider source, IDisposable disposable)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.disposable = disposable ?? throw new ArgumentNullException(nameof(disposable));
        }

        public WaveFormat WaveFormat => this.source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            if (this.isDisposed)
            {
                return 0;
            }

            int read = this.source.Read(buffer, offset, count);
            if (read == 0)
            {
                Dispose();
            }
            return read;
        }

        private void Dispose()
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                this.disposable.Dispose();
            }
        }
    }
}
