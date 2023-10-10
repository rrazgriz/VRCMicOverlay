using System;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Text;

using Valve.VR;
using NAudio.Wave;
using VRC.OSCQuery;

namespace Raz.VRCMicOverlay
{
    internal class Configuration
    {
        public float ICON_MUTED_MAX_ALPHA = 0.50f;
        public float ICON_MUTED_MIN_ALPHA = 0.00f;
        public float ICON_UNMUTED_MAX_ALPHA = 0.75f;
        public float ICON_UNMUTED_MIN_ALPHA = 0.05f;

        public bool USE_CUSTOM_MIC_SFX = false;
        public float CUSTOM_MIC_SFX_VOLUME = 0.65f;

        // VRChat doesn't output the Voice parameter while muted, so we have to read from a device ourselves
        // This adds a lot of dependencies so considering just not doing this
        public string AUDIO_DEVICE_STARTS_WITH = "";
        public float MUTED_MIC_THRESHOLD = 0.1f;

        public float ICON_CHANGE_SCALE_FACTOR = 1.25f; // Scale icon by this factor when changing between mute/unmute
        public float ICON_SIZE = 0.05f; // Size, square, of icon overlay (in meters)
        public float ICON_OFFSET_X = -0.37f; // Distance left/right of head center axis (negative is left)
        public float ICON_OFFSET_Y = -0.26f; // Distance above/below head axis (negative is in front)
        public float ICON_OFFSET_Z = -0.92f; // Distance in front of the head (negative is in front)

        public bool RESTART_FADE_TIMER_ON_STATE_CHANGE = true; // Whether to restart the fade timer when changing from mute/unmute
        public float MIC_MUTED_FADE_START    = 1.0f; // Time to start fading (seconds)
        public float MIC_MUTED_FADE_PERIOD   = 2.0f; // Time to fade to minimum
        public float MIC_UNMUTED_FADE_START  = 0.3f;
        public float MIC_UNMUTED_FADE_PERIOD = 1.0f;
        
        public string FILENAME_SFX_MIC_UNMUTED = "sfx-unmute.wav"; // Must be wav
        public string FILENAME_SFX_MIC_MUTED = "sfx-mute.wav";
        public string FILENAME_IMG_MIC_UNMUTED = "microphone-unmuted.png"; // Should probably only be png
        public string FILENAME_IMG_MIC_MUTED = "microphone-muted.png";

        public bool USE_LEGACY_OSC = false; // Will use OSCQuery otherwise
        public int LEGACY_OSC_LISTEN_PORT = 9001;
    }

    internal class Program
    {
        static string executablePath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) ?? "";

        const string OSC_MUTE_SELF_PARAMETER_PATH = "/avatar/parameters/MuteSelf"; // OSC Parameter Path
        const string OSC_VOICE_PARAMETER_PATH = "/avatar/parameters/Voice";
        const string SETTINGS_FILENAME = "settings.json";

        static string settingsPath = SETTINGS_FILENAME;

        const string APPLICATION_KEY = "one.raz.vrcmicoverlay";
        const string OVERLAY_KEY = "one.raz.vrcmicoverlay.mic";
        const string OVERLAY_NAME = "VRCMicOverlay";
        
        const string MANIFEST_FILENAME = "vrcmicoverlay.vrmanifest";

        // Global State
        static float _iconScaleFactorCurrent = 1.0f;
        static float _iconScaleFactorTarget = 1.0f;
        static float _iconScaleFactorRate = 1.0f;

        static float _iconAlphaFactorCurrent = 0.0f;
        static float _iconAlphaFactorTarget = 1.0f;
        static float _iconAlphaFactorRate = 0.1f;

        static float _deviceMicLevel = 0.0f;
        static float _vrcMicLevel = 0.0f;
        static float _mutedMicLevelTimer = 0.0f;
        static float _unmutedMicLevelTimer = 0.0f;

        static double _updateRate = 1 / 144;
        const float ICON_UNFADE_RATE = 1f / 0.05f; // rate per second, chosen arbitrarily

        enum MuteState { MUTED, UNMUTED }
        static MuteState _muteState = MuteState.MUTED;

        static readonly Stopwatch stopWatch = new();
        static readonly Stopwatch processCheckTimer = new();
        const float PROCESS_CHECK_INTERVAL = 5.0f;
        const bool CHECK_IF_VRC_IS_RUNNING = true;

        static Configuration Config = new();

        static bool _isVRCRunning = false;

        static void Main(string[] args)
        {
            Console.WriteLine("Starting Microphone Overlay...");

#if !DEBUG // This gets annoying when developing
            // Minimize console window (6 corresponds to a native enum or something that means minimize)
            ShowWindow(GetConsoleWindow(), 6);
            Console.WriteLine("Minimizing Window");
#endif

            var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };

            // Settings
            settingsPath = Path.Combine(new string[] { executablePath, SETTINGS_FILENAME });
            if (File.Exists(settingsPath))
            {
                try
                {
                    string jsonString = File.ReadAllText(settingsPath);
                    Config = JsonSerializer.Deserialize<Configuration>(jsonString, options) ?? new Configuration();
                    Console.WriteLine($"Using settings from {settingsPath}");

                    // Write config back, in case it's been updated
                    string newConfigString = JsonSerializer.Serialize(Config, options);
                    File.WriteAllText(settingsPath, newConfigString);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception caught while reading {settingsPath}, using defaults");
                    Console.WriteLine(ex.ToString());
                }
            }
            else
            {
                Console.WriteLine($"No settings file found at {settingsPath}, using defaults");
                string jsonString = JsonSerializer.Serialize(Config, options);
                File.WriteAllText(settingsPath, jsonString);
            }

            Console.WriteLine("Configuration:");
            Console.WriteLine(JsonSerializer.Serialize(Config, options));

            // Texture setup
            string unmutedIconPath = Path.Combine(new string[] { executablePath, Config.FILENAME_IMG_MIC_UNMUTED });
            string mutedIconPath = Path.Combine(new string[] { executablePath, Config.FILENAME_IMG_MIC_MUTED });

            // OpenVR Setup
            var error = new ETrackedPropertyError();
            var initError = EVRInitError.Unknown;

            var ovrApplicationType = EVRApplicationType.VRApplication_Overlay;
            OpenVR.InitInternal(ref initError, ovrApplicationType);

            SetupOpenVRAutostart();

            ulong overlayHandle = 0;
            EVROverlayErrorHandler(OpenVR.Overlay.CreateOverlay(OVERLAY_KEY, OVERLAY_NAME, ref overlayHandle));

            Vector3 offsetVector = new(Config.ICON_OFFSET_X, Config.ICON_OFFSET_Y, Config.ICON_OFFSET_Z);

            var relativeTransform = GetIconTransform(offsetVector).ToHmdMatrix34_t();
            // Set and forget, this locks the overlay to the head using a specified matrix
            EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(overlayHandle, 0, ref relativeTransform));

            EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayFromFile(overlayHandle, mutedIconPath));
            EVROverlayErrorHandler(OpenVR.Overlay.ShowOverlay(overlayHandle));
            EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayWidthInMeters(overlayHandle, Config.ICON_SIZE));
            EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayAlpha(overlayHandle, (float)_iconAlphaFactorCurrent));

            // Run at display frequency 
            _updateRate = 1 / (double)OpenVR.System.GetFloatTrackedDeviceProperty(0, ETrackedDeviceProperty.Prop_DisplayFrequency_Float, ref error); // Device 0 should always be headset

            // Sound setup
            System.Media.SoundPlayer sfxMute = new(Config.FILENAME_SFX_MIC_MUTED);
            System.Media.SoundPlayer sfxUnmute = new(Config.FILENAME_SFX_MIC_UNMUTED);
            SetVolume(Config.CUSTOM_MIC_SFX_VOLUME);

            // OSC Setup
            int oscPort = Config.LEGACY_OSC_LISTEN_PORT;
            if (!Config.USE_LEGACY_OSC)
            {
                oscPort = VRC.OSCQuery.Extensions.GetAvailableUdpPort();
                int oscQueryPort = VRC.OSCQuery.Extensions.GetAvailableTcpPort();
                string oscIP = "127.0.0.1";

                const string OSCQUERY_SERVICE_NAME = "VRCMicOverlay";
                Console.Write($"\nSetting up OSCQuery on {oscIP} UDP:{oscPort} TCP:{oscQueryPort}\n");
                IDiscovery discovery = new MeaModDiscovery();
                var oscQuery = new OSCQueryServiceBuilder()
                    .WithServiceName(OSCQUERY_SERVICE_NAME)
                    .WithUdpPort(oscPort)
                    .WithTcpPort(oscQueryPort)
                    .WithDiscovery(discovery)
                    .StartHttpServer()
                    .AdvertiseOSC()
                    .AdvertiseOSCQuery()
                    .Build();

                oscQuery.RefreshServices();
                oscQuery.AddEndpoint<bool>(OSC_MUTE_SELF_PARAMETER_PATH, Attributes.AccessValues.ReadWrite, new object[] { true });
                oscQuery.AddEndpoint<float>(OSC_VOICE_PARAMETER_PATH, Attributes.AccessValues.ReadWrite, new object[] { true });
            }

            SimpleOSC oscReceiver = new();
            var oscEndpoint = oscReceiver.OpenClient(oscPort);
            List<SimpleOSC.OSCMessage> incomingMessages = new();

            // Sound device setup, for listening to audio levels while muted (VRC doesn't send the Voice parameter when muted)
            SetupMicListener();

            CheckIfVRCIsRunning(true);
            processCheckTimer.Start();
            stopWatch.Start();

            Console.WriteLine("All Set up! Listening...");

#if DEBUG
            Console.WriteLine("Running in DEBUG mode!");
#endif

            // Main Program Loop
            while (true)
            {
                // Main timing loop
                double elapsedTimeSeconds = stopWatch.Elapsed.TotalMilliseconds / 1000;
                if (elapsedTimeSeconds > _updateRate)
                {
                    stopWatch.Restart();

                    CheckIfVRCIsRunning();

                    // Update Title
                    Console.Title = $"VRCMicOverlay : {_muteState}{(_isVRCRunning ? "" : " (waiting)")}";

                    // Handle incoming OSC to get mute state (and unmuted mic level)
                    oscReceiver.GetIncomingOSC(incomingMessages);
                    try
                    {
                        if (incomingMessages.Count > 0)
                        {
                            foreach (var message in incomingMessages)
                            {
                                if (message.path == OSC_MUTE_SELF_PARAMETER_PATH)
                                {
                                    _muteState = (bool)message.arguments[0] ? MuteState.MUTED : MuteState.UNMUTED;

                                    // Scale icon up (bounce)
                                    _iconScaleFactorCurrent = Config.ICON_CHANGE_SCALE_FACTOR;

                                    // Reset timers if configured
                                    if (Config.RESTART_FADE_TIMER_ON_STATE_CHANGE)
                                    {
                                        _mutedMicLevelTimer = 0;
                                        _unmutedMicLevelTimer = 0;
                                    }

                                    if (_muteState == MuteState.MUTED)
                                    {
                                        EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayFromFile(overlayHandle, mutedIconPath));

                                        _iconAlphaFactorCurrent = Config.ICON_MUTED_MAX_ALPHA;

                                        if (Config.USE_CUSTOM_MIC_SFX)
                                        {
                                            sfxMute.Play();
                                        }
                                    }
                                    else
                                    {
                                        EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayFromFile(overlayHandle, unmutedIconPath));

                                        // Bit of a hack to make it not always start at full alpha if not speaking when unmuting
                                        _iconAlphaFactorCurrent = (Config.ICON_UNMUTED_MAX_ALPHA + Config.ICON_UNMUTED_MIN_ALPHA) / 2f;
                                        _unmutedMicLevelTimer = Config.MIC_UNMUTED_FADE_START; // This will be reset if there's voice activity

                                        if (Config.USE_CUSTOM_MIC_SFX)
                                        {
                                            sfxUnmute.Play();
                                        }
                                    }
                                }
                                else if (message.path == OSC_VOICE_PARAMETER_PATH)
                                {
                                    _vrcMicLevel = (float)message.arguments[0];

                                }
                            }

                            incomingMessages.Clear();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }

                    // Speaking Logic
                    if (_muteState == MuteState.MUTED)
                    {
                        _iconAlphaFactorRate = 1 / Config.MIC_MUTED_FADE_PERIOD;

                        if (_deviceMicLevel < Config.MUTED_MIC_THRESHOLD)
                        {
                            _mutedMicLevelTimer += (float)elapsedTimeSeconds;
                        }
                        else
                        {
                            _mutedMicLevelTimer = 0;
                        }

                        if (_mutedMicLevelTimer > Config.MIC_MUTED_FADE_START)
                        {
                            _iconAlphaFactorTarget = 0.0f;
                        }
                        else
                        {
                            _iconAlphaFactorRate = ICON_UNFADE_RATE;
                            _iconAlphaFactorTarget = 1.0f;
                        }
                    }
                    else
                    {
                        _iconAlphaFactorRate = 1 / Config.MIC_UNMUTED_FADE_PERIOD;

                        if (_vrcMicLevel < 0.001f) // Mic level is normalized by VRC so we just need it to be (nearly) zero
                        {
                            _unmutedMicLevelTimer += (float)elapsedTimeSeconds;
                        }
                        else
                        {
                            _unmutedMicLevelTimer = 0f;
                        }

                        if (_unmutedMicLevelTimer > Config.MIC_UNMUTED_FADE_START)
                        {
                            _iconAlphaFactorTarget = 0.0f;
                        }
                        else
                        {
                            _iconAlphaFactorRate = ICON_UNFADE_RATE;
                            _iconAlphaFactorTarget = 1.0f;
                        }
                    }

                    // Calculate icon Alpha
                    float iconAlphaDelta = _iconAlphaFactorRate * (float)elapsedTimeSeconds;
                    if (Math.Abs(_iconAlphaFactorTarget - _iconAlphaFactorCurrent) > iconAlphaDelta)
                    {
                        _iconAlphaFactorCurrent += iconAlphaDelta * Math.Sign(_iconAlphaFactorTarget - _iconAlphaFactorCurrent);
                    }
                    else
                    {
                        // Snap to the target value (so we don't float above zero)
                        _iconAlphaFactorCurrent = _iconAlphaFactorTarget;
                    }

                    // Calculate icon Scaling
                    float iconScaleDelta = _iconScaleFactorRate * (float)elapsedTimeSeconds;
                    if (Math.Abs(_iconScaleFactorTarget - _iconScaleFactorCurrent) > iconScaleDelta)
                    {
                        _iconScaleFactorCurrent += iconScaleDelta * Math.Sign(_iconScaleFactorTarget - _iconScaleFactorCurrent);
                    }
                    else
                    {
                        _iconScaleFactorCurrent = _iconScaleFactorTarget;
                    }

                    // These are inside the timing loop so updates are only sent at the update rate
                    float minAlphaValue = _muteState == MuteState.MUTED ? Config.ICON_MUTED_MIN_ALPHA : Config.ICON_UNMUTED_MIN_ALPHA;
                    float maxAlphaValue = _muteState == MuteState.MUTED ? Config.ICON_MUTED_MAX_ALPHA : Config.ICON_UNMUTED_MAX_ALPHA;
                    float iconAlphaFactorSetting = Saturate(Lerp(minAlphaValue, maxAlphaValue, _iconAlphaFactorCurrent));

#if !DEBUG // Always show when debugging
                    if (CHECK_IF_VRC_IS_RUNNING && !_isVRCRunning)
                    {
                        iconAlphaFactorSetting = 0.0f;
                    }
#endif

                    EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayWidthInMeters(overlayHandle, Config.ICON_SIZE * _iconScaleFactorCurrent));
                    EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayAlpha(overlayHandle, iconAlphaFactorSetting));
                }

                // Give up the rest of our time slice to anything else that needs to run
                // From MSDN: If the value of the millisecondsTimeout argument is zero, the thread relinquishes the remainder of its time slice to any thread of equal priority that is ready to run.
                // https://learn.microsoft.com/en-us/dotnet/api/system.threading.thread.sleep
                Thread.Sleep(0);
            }
        }

        private static Matrix4x4 GetIconTransform(Vector3 offsetVector)
        {
            // A "World" matrix is created incorproating our offset; this skews the icon so it always points toward the head
            var rotMatrix = Matrix4x4.CreateWorld(Vector3.Zero, Vector3.Normalize(offsetVector), new Vector3(0, -1, 0));
            
            // For some reason it's upside down so let's just rotate it around and call it good
            rotMatrix = Matrix4x4.Multiply(rotMatrix, Matrix4x4.CreateFromAxisAngle(new Vector3(0, 0, 1), MathF.PI));
            
            var offsetMatrix = Matrix4x4.CreateTranslation(offsetVector);
            var relativeTransform = Matrix4x4.Multiply(rotMatrix, offsetMatrix);
            return relativeTransform;
        }

        private static float Lerp(float a, float b, float t) => b * t + a * (1f - t);
        private static float Saturate(float v) => Math.Clamp(v, 0f, 1f);

        private static double Frac(double v) => v - Math.Truncate(v);
        private static double PingPong(double v) => (Math.Abs(Frac((v) / (2.0)) * 2.0 - 1) - 0.5) * 2;

        private static EVRApplicationError EVRApplicationErrorHandler(EVRApplicationError error)
        {
            if(error != EVRApplicationError.None)
            {
                Console.WriteLine($"STEAMVR APPLICATION ERROR: {error.ToString()}");
            }

            return error;
        }

        private static EVROverlayError EVROverlayErrorHandler(EVROverlayError error)
        {
            if(error != EVROverlayError.None)
            {
                Console.WriteLine($"STEAMVR OVERLAY ERROR: {error.ToString()}");
            }

            return error;
        }

        // These classes are modified from https://github.com/ValveSoftware/steamvr_unity_plugin/
        // Used with permission under the BSD 3-Clause license
        public class SteamVR_ManifestFile
        {
            public List<SteamVR_ManifestFile_Application> applications;
        }

        public class SteamVR_ManifestFile_Application
        {
            public string app_key;
            public string launch_type;
            public string binary_path_windows;
            public bool is_dashboard_overlay;
            public Dictionary<string, SteamVR_ManifestFile_ApplicationString> strings = new Dictionary<string, SteamVR_ManifestFile_ApplicationString>();
        }

        public class SteamVR_ManifestFile_ApplicationString
        {
            public string name;
            public string description;
        }

        private static void SetupOpenVRAutostart()
        {
            string manifestPath = Path.Combine(executablePath, MANIFEST_FILENAME);

            var manifest = new SteamVR_ManifestFile();
            var manifestApplication = new SteamVR_ManifestFile_Application
            {
                app_key = APPLICATION_KEY,
                launch_type = "binary",
                binary_path_windows = "VRCMicOverlay.exe",
                is_dashboard_overlay = true
            };
            var strings = new SteamVR_ManifestFile_ApplicationString()
            {
                name = OVERLAY_NAME,
                description = "OpenVR Overlay to replace the built in VRChat HUD mic icon"
            };
            manifestApplication.strings.Add("en_us", strings);
            manifest.applications = new List<SteamVR_ManifestFile_Application> {manifestApplication};
            
            var serializerOptions = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };

            string manifestJsonString = JsonSerializer.Serialize(manifest, serializerOptions);
            File.WriteAllText(manifestPath, manifestJsonString);

            // Set up autolaunch
            if (!OpenVR.Applications.IsApplicationInstalled(APPLICATION_KEY))
            {
                // Add our manifest first
                EVRApplicationErrorHandler(OpenVR.Applications.AddApplicationManifest(manifestPath, false));
                EVRApplicationErrorHandler(OpenVR.Applications.SetApplicationAutoLaunch(APPLICATION_KEY, true));
            }
            else
            {
                // Check if the autolaunch is set up with the current program location
                bool isAutostartEnabled = OpenVR.Applications.GetApplicationAutoLaunch(APPLICATION_KEY);
                StringBuilder binaryPath = new();
                EVRApplicationError dummyApplicationError = EVRApplicationError.None;
                OpenVR.Applications.GetApplicationPropertyString(APPLICATION_KEY, EVRApplicationProperty.BinaryPath_String, binaryPath, 255, ref dummyApplicationError);
                string binaryPathTrimmed = Path.GetDirectoryName(binaryPath.ToString());
                
                if (!String.Equals(binaryPathTrimmed, executablePath, StringComparison.Ordinal))
                {
                    EVRApplicationErrorHandler(OpenVR.Applications.RemoveApplicationManifest(Path.Combine($"{binaryPathTrimmed}", MANIFEST_FILENAME)));
                    EVRApplicationErrorHandler(OpenVR.Applications.AddApplicationManifest(manifestPath, false));
                    EVRApplicationErrorHandler(OpenVR.Applications.SetApplicationAutoLaunch(APPLICATION_KEY, isAutostartEnabled));
                }
            }
        }

        private static void SetupMicListener()
        {
            Console.WriteLine("\nAudio Device Selection:");
            int deviceID = -1;
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var deviceCapabilities = WaveInEvent.GetCapabilities(i);
                string deviceName = deviceCapabilities.ProductName;
                if (deviceName.StartsWith(Config.AUDIO_DEVICE_STARTS_WITH) && Config.AUDIO_DEVICE_STARTS_WITH != "")
                {
                    Console.Write($"✓");
                    deviceID = i;
                }
                else
                {
                    Console.Write($" ");
                }
                Console.WriteLine($" {i} : {deviceName}");
            }
            Console.WriteLine();
            if(deviceID < 0) Console.WriteLine($"Audio Device \"{Config.AUDIO_DEVICE_STARTS_WITH}\" not matched, using default recording device.\n");

            var waveIn = new WaveInEvent
            {
                DeviceNumber = deviceID,
                WaveFormat = new WaveFormat(rate: 44100, bits: 16, channels: 2),
                BufferMilliseconds = 50
            };

            waveIn.DataAvailable += CalculatePeakMicLevel;
            waveIn.StartRecording();
        }

        private static void CalculatePeakMicLevel(object sender, WaveInEventArgs args)
        {
            const float maxValue = 32767;
            const int bytesPerSample = 2;
            
            int peakValue = 0;
            for (int index = 0; index < args.BytesRecorded; index += bytesPerSample)
            {
                int value = BitConverter.ToInt16(args.Buffer, index);
                peakValue = Math.Max(peakValue, value);
            }

            _deviceMicLevel = peakValue / maxValue;
        }
    
        private static bool IsProcessRunning(string processName)
        {
            Process[] pname = Process.GetProcessesByName(processName);
            return pname.Length > 0;
        }

        private static void CheckIfVRCIsRunning(bool isFirstCheck = false)
        {
            if (CHECK_IF_VRC_IS_RUNNING && (isFirstCheck || processCheckTimer.Elapsed.TotalSeconds > PROCESS_CHECK_INTERVAL))
            {
                bool isVRCRunningNow = IsProcessRunning("VRChat");

                if (isVRCRunningNow != _isVRCRunning || isFirstCheck)
                {
                    if (isVRCRunningNow)
                        Console.WriteLine("VRChat Process Detected, showing Mic!");
                    else
                        Console.WriteLine("VRChat Process NOT Detected, hiding Mic!");
                }

                _isVRCRunning = isVRCRunningNow;
                processCheckTimer.Restart();
            }
        }

        [DllImport("User32.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow([In] IntPtr hWnd, [In] int nCmdShow);
        
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        // thanks to https://stackoverflow.com/questions/34277066/how-do-i-fade-out-the-audio-of-a-wav-file-using-soundplayer-instead-of-stopping
        [DllImport("winmm.dll", EntryPoint = "waveOutGetVolume")]
        private static extern int WaveOutGetVolume(IntPtr hwo, out uint dwVolume);

        [DllImport("winmm.dll", EntryPoint = "waveOutSetVolume")]
        private static extern int WaveOutSetVolume(IntPtr hwo, uint dwVolume);

        private static int SetVolume(float volume)
        {
            float clampedVolume = MathF.Min(MathF.Max(volume, 0f), 1f);
            ushort channelVolume = (ushort)Lerp(0, ushort.MaxValue, clampedVolume);
            uint vol = (uint)channelVolume | ((uint)channelVolume << 16);
            return WaveOutSetVolume(IntPtr.Zero, vol);
        }
    }

    // From https://github.com/OVRTools/OVRSharp
    // Under the MIT license (Copyright 2020-2021 TJ Horner)
    public static class MatrixExtension
    {
        /// <summary>
        /// Converts a <see cref="Matrix4x4"/> to a <see cref="HmdMatrix34_t"/>.
        /// <br/>
        /// <br/>
        /// From: <br/>
        /// 11 12 13 14 <br/>
        /// 21 22 23 24 <br/>
        /// 31 32 33 34 <br/>
        /// 41 42 43 44
        /// <br/><br/>
        /// To: <br/>
        /// 11 12 13 41 <br/>
        /// 21 22 23 42 <br/>
        /// 31 32 33 43
        /// </summary>
        public static HmdMatrix34_t ToHmdMatrix34_t(this Matrix4x4 matrix)
        {
            return new HmdMatrix34_t()
            {
                m0 = matrix.M11,
                m1 = matrix.M12,
                m2 = matrix.M13,
                m3 = matrix.M41,
                m4 = matrix.M21,
                m5 = matrix.M22,
                m6 = matrix.M23,
                m7 = matrix.M42,
                m8 = matrix.M31,
                m9 = matrix.M32,
                m10 = matrix.M33,
                m11 = matrix.M43,
            };
        }

        /// <summary>
        /// Converts a <see cref="HmdMatrix34_t"/> to a <see cref="Matrix4x4"/>.
        /// <br/>
        /// <br/>
        /// From: <br/>
        /// 11 12 13 14 <br/>
        /// 21 22 23 24 <br/>
        /// 31 32 33 34
        /// <br/><br/>
        /// To: <br/>
        /// 11 12 13 XX <br/>
        /// 21 22 23 XX <br/>
        /// 31 32 33 XX <br/>
        /// 14 24 34 XX
        /// </summary>
        public static Matrix4x4 ToMatrix4x4(this HmdMatrix34_t matrix)
        {
            return new Matrix4x4(
                matrix.m0, matrix.m1, matrix.m2, 0,
                matrix.m4, matrix.m5, matrix.m6, 0,
                matrix.m8, matrix.m9, matrix.m10, 0,
                matrix.m3, matrix.m7, matrix.m11, 1
            );
        }
    }
}