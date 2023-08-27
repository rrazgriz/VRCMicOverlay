using BOLL7708;
using Valve.VR;
using System.Diagnostics;
using System.Media;
using NAudio.Wave;
using System.Text.Json;
using VRC.OSCQuery;
using System.Runtime.InteropServices;

// Uses code from the following:
// EasyOpenVR, by BOLL7708 (license pending) : https://github.com/BOLL7708/EasyOpenVR
// Av3Emulator (SimpleOSC), by Lyuma (MIT): https://github.com/lyuma/Av3Emulator/

namespace Raz.VRCMicOverlay
{
    internal class Configuration
    {
        public float ICON_MUTED_MAX_ALPHA = 0.50f;
        public float ICON_MUTED_MIN_ALPHA = 0.00f;
        public float ICON_UNMUTED_MAX_ALPHA = 0.75f;
        public float ICON_UNMUTED_MIN_ALPHA = 0.05f;

        public bool USE_CUSTOM_MIC_SFX = false;

        // VRChat doesn't output the Voice parameter while muted, so we have to read from a device ourselves
        // This adds a lot of dependencies so considering just not doing this
        public string AUDIO_DEVICE_STARTS_WITH = "";
        public float MUTED_MIC_THRESHOLD = 0.15f;

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

        public bool USE_LEGACY_OSC = true; // Will use OSCQuery otherwise
        public int LEGACY_OSC_LISTEN_PORT = 9001;
    }

    internal class Program
    {
        const string OSC_MUTE_SELF_PARAMETER_PATH = "/avatar/parameters/MuteSelf"; // OSC Parameter Path
        const string OSC_VOICE_PARAMETER_PATH = "/avatar/parameters/Voice";
        static string SETTINGS_FILENAME = "settings.json";

        // State management
        // ----------------------
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

        static Configuration Config;

        static void Main(string[] args)
        {
            Console.WriteLine("Starting Microphone Overlay...");

            var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };

            // Settings
            if (File.Exists(SETTINGS_FILENAME))
            {
                try
                {
                    string jsonString = File.ReadAllText(SETTINGS_FILENAME);
                    Config = JsonSerializer.Deserialize<Configuration>(jsonString, options) ?? new Configuration();
                    Console.WriteLine($"Using settings from {SETTINGS_FILENAME}");

                    // Write config back, in case it's been updated
                    string newConfigString = JsonSerializer.Serialize(Config, options);
                    File.WriteAllText(SETTINGS_FILENAME, newConfigString);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception caught while reading {SETTINGS_FILENAME}, using defaults");
                    Console.WriteLine(ex.ToString());
                    Config = new Configuration();
                }
            }
            else
            {
                Console.WriteLine($"No settings file found at {SETTINGS_FILENAME}, using defaults");
                Config = new Configuration();
                string jsonString = JsonSerializer.Serialize(Config, options);
                File.WriteAllText(SETTINGS_FILENAME, jsonString);
            }

            Console.WriteLine("Configuration:");
            Console.WriteLine(JsonSerializer.Serialize(Config, options));

            // Texture setup
            string unmutedIconPath = Path.Combine(new string[] { Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), Config.FILENAME_IMG_MIC_UNMUTED });
            string mutedIconPath = Path.Combine(new string[] { Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), Config.FILENAME_IMG_MIC_MUTED });

            // OpenVR Setup
            EasyOpenVRSingleton vr = EasyOpenVRSingleton.Instance;
            vr.SetApplicationType(EVRApplicationType.VRApplication_Overlay);
            vr.Init();

            var overlay = vr.CreateOverlay("VRCMicOverlayKeyHello", "VRCMicOverlay", CalculateIconTransform(vr));
            vr.SetOverlayTextureFromFile(overlay, mutedIconPath);
            vr.SetOverlayVisibility(overlay, true);
            vr.SetOverlayWidth(overlay, Config.ICON_SIZE);
            vr.SetOverlayAlpha(overlay, (float)_iconAlphaFactorCurrent);

            _updateRate = 1 / (double)vr.GetFloatTrackedDeviceProperty(0, ETrackedDeviceProperty.Prop_DisplayFrequency_Float);

            // Sound setup
            System.Media.SoundPlayer sfxMute = new System.Media.SoundPlayer(Config.FILENAME_SFX_MIC_MUTED);
            System.Media.SoundPlayer sfxUnmute = new System.Media.SoundPlayer(Config.FILENAME_SFX_MIC_UNMUTED);

            // OSC Setup
            int oscPort = Config.USE_LEGACY_OSC ? Config.LEGACY_OSC_LISTEN_PORT : VRC.OSCQuery.Extensions.GetAvailableUdpPort();
            if (!Config.USE_LEGACY_OSC)
            {
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

            // Minimize console window (6 corresponds to a native enum or something that means minimize)
            ShowWindow(GetConsoleWindow(), 6);

            stopWatch.Start();

            Console.WriteLine("All Set up! Listening...");

            // Main Program Loop
            while (true)
            {
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
                                    vr.SetOverlayTextureFromFile(overlay, mutedIconPath);

                                    _iconAlphaFactorCurrent = Config.ICON_MUTED_MAX_ALPHA;

                                    if (Config.USE_CUSTOM_MIC_SFX)
                                    {
                                        sfxMute.Play();
                                    }
                                }
                                else
                                {
                                    vr.SetOverlayTextureFromFile(overlay, unmutedIconPath);

                                    // Bit of a hack to make it not always start at full alpha if not speaking when unmuting
                                    _iconAlphaFactorCurrent = (Config.ICON_UNMUTED_MAX_ALPHA + Config.ICON_UNMUTED_MIN_ALPHA) / 2f;
                                    _unmutedMicLevelTimer = Config.MIC_UNMUTED_FADE_START; // This will be reset if there's voice activity

                                    if (Config.USE_CUSTOM_MIC_SFX)
                                    {
                                        sfxUnmute.Play();
                                    }
                                }
                            }
                            else if(message.path == OSC_VOICE_PARAMETER_PATH)
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

                // Main timing loop
                double elapsedTimeSeconds = stopWatch.Elapsed.TotalMilliseconds / 1000;
                if (elapsedTimeSeconds > _updateRate)
                {
                    // Update Title
                    Console.Title = $"VRCMicOverlay : {_muteState}";

                    stopWatch.Restart();

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
                }

                float minAlphaValue = _muteState == MuteState.MUTED ? Config.ICON_MUTED_MIN_ALPHA : Config.ICON_UNMUTED_MIN_ALPHA;
                float maxAlphaValue = _muteState == MuteState.MUTED ? Config.ICON_MUTED_MAX_ALPHA : Config.ICON_UNMUTED_MAX_ALPHA;
                float iconAlphaFactorSetting = Saturate(Lerp(minAlphaValue, maxAlphaValue, _iconAlphaFactorCurrent));

                vr.SetOverlayTransform(overlay, CalculateIconTransform(vr));
                vr.SetOverlayWidth(overlay, Config.ICON_SIZE * _iconScaleFactorCurrent);
                vr.SetOverlayAlpha(overlay, iconAlphaFactorSetting);

                // Give up the rest of our time slice to anything else that needs to run
                // From MSDN: If the value of the millisecondsTimeout argument is zero, the thread relinquishes the remainder of its time slice to any thread of equal priority that is ready to run.
                // https://learn.microsoft.com/en-us/dotnet/api/system.threading.thread.sleep
                Thread.Sleep(0);
            }
        }

        private static float Lerp(float a, float b, float t) => b * t + a * (1f - t);
        private static float Saturate(float v) => Math.Clamp(v, 0f, 1f);

        private static double Frac(double v) => v - Math.Truncate(v);
        private static double PingPong(double v) => (Math.Abs(Frac((v) / (2.0)) * 2.0 - 1) - 0.5) * 2;

        private static void SetupMicListener()
        {
            Console.WriteLine("\nAudio Device Selection:");
            int deviceID = -1;
            for (int i = 0; i < NAudio.Wave.WaveInEvent.DeviceCount; i++)
            {
                var deviceCapabilities = NAudio.Wave.WaveInEvent.GetCapabilities(i);
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

            var waveIn = new NAudio.Wave.WaveInEvent
            {
                DeviceNumber = deviceID,
                WaveFormat = new NAudio.Wave.WaveFormat(rate: 44100, bits: 16, channels: 2),
                BufferMilliseconds = 50
            };

            waveIn.DataAvailable += CalculatePeakMicLevel;
            waveIn.StartRecording();
        }

        private static void CalculatePeakMicLevel(object sender, NAudio.Wave.WaveInEventArgs args)
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

        private static HmdMatrix34_t CalculateIconTransform(EasyOpenVRSingleton vr)
        {
            const double RAD_TO_DEG = 180 / Math.PI;

            var transform = vr.GetDeviceToAbsoluteTrackingPose()[0].mDeviceToAbsoluteTracking; // Device 0 should *always* be the hmd
            var pOffset = new HmdVector3_t
            {
                v0 = Config.ICON_OFFSET_X,
                v1 = Config.ICON_OFFSET_Y,
                v2 = Config.ICON_OFFSET_Z
            };
            transform = BOLL7708.Extensions.Translate(transform, pOffset);

            // Rotate the icon to point at the head
            double rX = Math.Tan((Config.ICON_OFFSET_Y) / (Math.Sqrt(Config.ICON_OFFSET_X * Config.ICON_OFFSET_X + Config.ICON_OFFSET_Z * Config.ICON_OFFSET_Z))) * RAD_TO_DEG;
            double rY = Math.Tan((Config.ICON_OFFSET_Z) / (Config.ICON_OFFSET_X)) * RAD_TO_DEG;
            transform = BOLL7708.Extensions.Rotate(transform, rX, -rY, 0); // Shouldn't need to spin it in Z

            return transform;
        }

        [DllImport("User32.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow([In] IntPtr hWnd, [In] int nCmdShow);
        
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();
    }
}