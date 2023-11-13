using System;
using System.Drawing;
using System.Numerics;
using System.Text.Json;
using System.Diagnostics;

using Valve.VR;
using VRC.OSCQuery;

namespace Raz.VRCMicOverlay
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting Microphone Overlay...");

#if !DEBUG // This gets annoying when developing
            // Minimize console window (6 corresponds to a native enum or something that means minimize)
            WindowsUtilities.SetWindowState(WindowsUtilities.GetConsoleWindow(), WindowsUtilities.CMDSHOW.SW_MINIMIZE);
            Console.WriteLine("Minimizing Window");
#endif

            Configuration Config = new();
            string executablePath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) ?? "";

            var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };

            // Settings
            string settingsPath = Path.Combine(new string[] { executablePath, Config.SETTINGS_FILENAME });
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
            IconState iconState = new IconState();
            var error = new ETrackedPropertyError();
            var initError = EVRInitError.Unknown;

            var ovrApplicationType = EVRApplicationType.VRApplication_Overlay;
            OpenVR.InitInternal(ref initError, ovrApplicationType);

            OVRUtilities.SetupOpenVRAutostart(Config);

            ulong overlayHandle = 0;
            OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.CreateOverlay(Config.OVERLAY_KEY, Config.OVERLAY_NAME, ref overlayHandle));

            Vector3 offsetVector = new(Config.ICON_OFFSET_X, Config.ICON_OFFSET_Y, Config.ICON_OFFSET_Z);

            if (Config.ICON_RANDOMIZED_OFFSET)
            {
                offsetVector += Config.ICON_SIZE * RandomUnitVector();
            }

            var relativeTransform = GetIconTransform(offsetVector).ToHmdMatrix34_t();
            // Set and forget, this locks the overlay to the head using a specified matrix
            OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(overlayHandle, 0, ref relativeTransform));

            OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayFromFile(overlayHandle, mutedIconPath));
            OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.ShowOverlay(overlayHandle));
            OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayWidthInMeters(overlayHandle, Config.ICON_SIZE));
            OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayAlpha(overlayHandle, iconState.iconAlphaFactorCurrent));

            if (Config.ICON_ALWAYS_ON_TOP)
            {
                OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.SetOverlaySortOrder(overlayHandle, uint.MaxValue));
            }

            ColorFloat mutedColor = new ColorFloat(ColorTranslator.FromHtml(Config.ICON_TINT_MUTED));
            ColorFloat unmutedColor = new ColorFloat(ColorTranslator.FromHtml(Config.ICON_TINT_UNMUTED));

            OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayColor(overlayHandle, mutedColor.R, mutedColor.G, mutedColor.B));

            // Run at display frequency
            double updateInterval = 1 / 144;
            updateInterval = 1 / (double)OpenVR.System.GetFloatTrackedDeviceProperty(0, ETrackedDeviceProperty.Prop_DisplayFrequency_Float, ref error); // Device 0 should always be headset

            // Sound setup
            System.Media.SoundPlayer sfxMute = new(Config.FILENAME_SFX_MIC_MUTED);
            System.Media.SoundPlayer sfxUnmute = new(Config.FILENAME_SFX_MIC_UNMUTED);
            WindowsUtilities.SetVolume(Config.CUSTOM_MIC_SFX_VOLUME);

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
                oscQuery.AddEndpoint<bool>(Config.OSC_MUTE_SELF_PARAMETER_PATH, Attributes.AccessValues.ReadWrite, new object[] { true });
                oscQuery.AddEndpoint<float>(Config.OSC_VOICE_PARAMETER_PATH, Attributes.AccessValues.ReadWrite, new object[] { true });
            }

            SimpleOSC oscReceiver = new();
            var oscEndpoint = oscReceiver.OpenClient(oscPort);
            List<SimpleOSC.OSCMessage> incomingMessages = new();

            // Sound device setup, for listening to audio levels while muted (VRC doesn't send the Voice parameter when muted)
            MicListener micListener = new MicListener();
            micListener.SetupMicListener(Config);

            MicState micState = new MicState();

            double deltaTime = 0f;

            ProcessTracker vrcProcessTracker = new ProcessTracker("VRChat", 5.0f);

            Stopwatch stopWatch = new();
            stopWatch.Start();

            Console.WriteLine("All Set up! Listening...");

#if DEBUG
            Console.WriteLine("Running in DEBUG mode!");
#endif

            // Main Program Loop
            while (true)
            {
                // Main timing loop
                deltaTime = stopWatch.Elapsed.TotalMilliseconds / 1000;
                if (deltaTime > updateInterval)
                {
                    stopWatch.Restart();

                    micState.deviceMicLevel = micListener.MicLevel;

                    // Update Title
                    Console.Title = $"VRCMicOverlay : {micState.vrcMuteState}{(vrcProcessTracker.IsProcessRunning ? "" : " (waiting)")}";

                    // Handle incoming OSC to get mute state (and unmuted mic level)
                    oscReceiver.GetIncomingOSC(incomingMessages);
                    try
                    {
                        if (incomingMessages.Count > 0)
                        {
                            foreach (var message in incomingMessages)
                            {
                                if (message.path == Config.OSC_MUTE_SELF_PARAMETER_PATH)
                                {
                                    micState.vrcMuteState = (bool)message.arguments[0] ? MuteState.MUTED : MuteState.UNMUTED;

                                    // Scale icon up (bounce)
                                    iconState.iconScaleFactorCurrent = Config.ICON_CHANGE_SCALE_FACTOR;

                                    // Reset timers if configured
                                    if (Config.RESTART_FADE_TIMER_ON_STATE_CHANGE)
                                    {
                                        micState.mutedMicLevelTimer = 0;
                                        micState.unmutedMicLevelTimer = 0;
                                    }

                                    if (micState.vrcMuteState == MuteState.MUTED)
                                    {
                                        OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayFromFile(overlayHandle, mutedIconPath));
                                        OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayColor(overlayHandle, mutedColor.R, mutedColor.G, mutedColor.B));

                                        iconState.iconAlphaFactorCurrent = Config.ICON_MUTED_MAX_ALPHA;

                                        if (Config.USE_CUSTOM_MIC_SFX)
                                        {
                                            sfxMute.Play();
                                        }
                                    }
                                    else
                                    {
                                        OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayFromFile(overlayHandle, unmutedIconPath));
                                        OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayColor(overlayHandle, unmutedColor.R, unmutedColor.G, unmutedColor.B));

                                        // Bit of a hack to make it not always start at full alpha if not speaking when unmuting
                                        iconState.iconAlphaFactorCurrent = (Config.ICON_UNMUTED_MAX_ALPHA + Config.ICON_UNMUTED_MIN_ALPHA) / 2f;

                                        if (Config.USE_CUSTOM_MIC_SFX)
                                        {
                                            sfxUnmute.Play();
                                        }
                                    }
                                }
                                else if (message.path == Config.OSC_VOICE_PARAMETER_PATH)
                                {
                                    micState.vrcMicLevel = (float)message.arguments[0];

                                }
                            }

                            incomingMessages.Clear();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }

                    // Set timers and values
                    ProcessMicAndIconState(ref micState, ref iconState, Config, (float)deltaTime);

                    iconState.Update((float)deltaTime);

                    // These are inside the timing loop so updates are only sent at the update rate
                    float minAlphaValue = micState.vrcMuteState == MuteState.MUTED ? Config.ICON_MUTED_MIN_ALPHA : Config.ICON_UNMUTED_MIN_ALPHA;
                    float maxAlphaValue = micState.vrcMuteState == MuteState.MUTED ? Config.ICON_MUTED_MAX_ALPHA : Config.ICON_UNMUTED_MAX_ALPHA;
                    float iconAlphaFactorSetting = Saturate(Lerp(minAlphaValue, maxAlphaValue, iconState.iconAlphaFactorCurrent));

#if !DEBUG // Always show when debugging
                    if (!vrcProcessTracker.IsProcessRunning)
                    {
                        iconAlphaFactorSetting = 0.0f;
                    }
#endif

                    OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayWidthInMeters(overlayHandle, Config.ICON_SIZE * iconState.iconScaleFactorCurrent));
                    OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayAlpha(overlayHandle, iconAlphaFactorSetting));
                }

                // Give up the rest of our time slice to anything else that needs to run
                // From MSDN: If the value of the millisecondsTimeout argument is zero, the thread relinquishes the remainder of its time slice to any thread of equal priority that is ready to run.
                // https://learn.microsoft.com/en-us/dotnet/api/system.threading.thread.sleep
                Thread.Sleep(0);
            }
        }

        static void ProcessMicAndIconState(ref MicState micState, ref IconState iconState, Configuration Config, float deltaTime, float voiceLevel = 1.0f)
        {
            // Speaking Logic
            if (micState.vrcMuteState == MuteState.MUTED)
            {
                iconState.iconAlphaFactorRate = 1 / Config.MIC_MUTED_FADE_PERIOD;

                if (micState.deviceMicLevel < Config.MUTED_MIC_THRESHOLD)
                {
                    micState.mutedMicLevelTimer += deltaTime;
                }
                else
                {
                    micState.mutedMicLevelTimer = 0;
                }

                if (micState.mutedMicLevelTimer > Config.MIC_MUTED_FADE_START)
                {
                    iconState.iconAlphaFactorTarget = 0.0f;
                }
                else
                {
                    iconState.iconAlphaFactorRate = 1 / Config.ICON_UNFADE_TIME;
                    iconState.iconAlphaFactorTarget = 1.0f;
                }
            }
            else
            {
                iconState.iconAlphaFactorRate = 1 / Config.MIC_UNMUTED_FADE_PERIOD;

                if (micState.vrcMicLevel < 0.001f) // Mic level is normalized by VRC so we just need it to be (nearly) zero
                {
                    micState.unmutedMicLevelTimer += deltaTime;
                }
                else
                {
                    micState.unmutedMicLevelTimer = 0f;
                }

                if (micState.unmutedMicLevelTimer > Config.MIC_UNMUTED_FADE_START)
                {
                    iconState.iconAlphaFactorTarget = 0.0f;
                }
                else
                {
                    iconState.iconAlphaFactorRate = 1 / Config.ICON_UNFADE_TIME;
                    iconState.iconAlphaFactorTarget = 1.0f;
                }
            }
        }

#region Math

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

        private static float RandomNegativeOneToOne() => Random.Shared.NextSingle() * (Random.Shared.NextSingle() > 0.5f ? 1.0f : -1.0f);
        private static Vector3 RandomUnitVector() => Vector3.Normalize(new Vector3(RandomNegativeOneToOne(), RandomNegativeOneToOne(), RandomNegativeOneToOne()));

        private static float Lerp(float a, float b, float t) => b * t + a * (1f - t);
        private static float Saturate(float v) => Math.Clamp(v, 0f, 1f);

#endregion

    }
}