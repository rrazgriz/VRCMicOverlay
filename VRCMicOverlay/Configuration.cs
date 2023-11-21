namespace Raz.VRCMicOverlay
{
    internal class Configuration
    {
        // User-Configurable Settings
        public float ICON_MUTED_MAX_ALPHA = 0.50f;      // Max icon transparency (0-1) while muted
        public float ICON_MUTED_MIN_ALPHA = 0.00f;      // Min icon transparency (0-1) while muted
        public float ICON_UNMUTED_MAX_ALPHA = 0.75f;    // Max icon transparency (0-1) while unmuted
        public float ICON_UNMUTED_MIN_ALPHA = 0.05f;    // Min icon transparency (0-1) while unmuted

        public float MIC_MUTED_FADE_START    = 1.0f;    // Time before icon starts fading while muted (times are in seconds)
        public float MIC_MUTED_FADE_PERIOD   = 2.0f;    // Time to fade from max to min alpha while muted
        public float MIC_UNMUTED_FADE_START  = 0.3f;    // Time before icon starts fading while unmuted
        public float MIC_UNMUTED_FADE_PERIOD = 1.0f;    // Time to fade from max to min alpha while unmuted
        public bool RESTART_FADE_TIMER_ON_STATE_CHANGE = true; // Whether to restart the fade timer when changing from mute/unmute. Play with this to see if you like it

        public string ICON_TINT_MUTED = "#FF5F5F";      // Icon tint color while muted
        public string ICON_TINT_UNMUTED = "#FFFFFF";    // Icon tint color while unmuted

        public bool ICON_SHIFTING = true;               // Move the icon in a circle slowly over time to avoid or lessen burn-in/burn-out on (u)OLED HMDs. 
        public float ICON_SHIFTING_PERIOD = 1800.0f;    // Time, in sec, to completely cycle the icon through the shift (defaults to 1800s, 30 minutes)
        public float ICON_SHIFTING_AMOUNT = 1.3f;       // In degrees - how much to move the icon by (in a circle). Test w/ custom icons/pos by reducing ICON_SHIFTING_TIME to low values (1-2 seconds)

        public bool ICON_ALWAYS_ON_TOP = true;          // Whether the icon should show over all other overlays or not
        public bool ICON_RANDOMIZED_OFFSET = false;     // Randomize icon position about the offset point on startup. May help further reduce burn-in/burn-out on (u)OLED HMDs.

        // VRChat doesn't output the Voice parameter while muted, so we have to read from a device ourselves
        public string AUDIO_DEVICE_STARTS_WITH = "";    // Enter the first unique characters of your audio device's name (off the list printed out when the app starts. If this is blank, it'll use the default audio device
        public float MUTED_MIC_THRESHOLD = 0.1f;        // Threshold (0-1) for the mic icon lighting up while muted. Recommend leaving at 0.1 for most mics, 0.2 if it's too sensitive

        public float ICON_SIZE = 0.05f;                 // Size, square, of icon overlay (in meters)
        public float ICON_CHANGE_SCALE_FACTOR = 1.25f;  // Scale icon by this factor when changing between mute/unmute (how big it "Bounces"). Set to 1.0 to disable

        public float ICON_OFFSET_X = -0.37f;            // Distance left/right of head center axis (negative is left)
        public float ICON_OFFSET_Y = -0.26f;            // Distance above/below head axis (negative is in front)
        public float ICON_OFFSET_Z = -0.92f;            // Distance in front of the head (negative is in front)

        public float ICON_UNFADE_TIME = 0.05f;          // Time to go from faded to unfaded
        public float ICON_ALPHA_EXPONENT = 2.0f;        // Scaling exponent for alpha. Recommend keeping at 2.0, reducing if long fade times are used.

        public bool USE_CUSTOM_MIC_SFX = false;     // If true, will use custom sfx wav files when muting/unmuting
        public float CUSTOM_MIC_SFX_VOLUME = 0.65f; // Volume (0-1) of custom sfx

        // All paths are relative to filepath of exe
        public string FILENAME_SFX_MIC_UNMUTED = "Assets/sfx-unmute.wav";          // Custom sound for unmute (must be wav)
        public string FILENAME_SFX_MIC_MUTED = "Assets/sfx-mute.wav";              // Custom sound for mute (must be wav)
        public string FILENAME_IMG_MIC_UNMUTED = "Assets/microphone-unmuted.png";  // Custom icon while unmuted (should probably only be png)
        public string FILENAME_IMG_MIC_MUTED = "Assets/microphone-muted.png";      // Custom icon while muted (should probably only be png)

        public bool USE_LEGACY_OSC = false;         // Will use OSCQuery if false, recommend keeping false as OSCQuery is integrated in VRChat now
        public int LEGACY_OSC_LISTEN_PORT = 9001;   // Port to listen on, configurable for OSC routers. Only used if USE_LEGACY_OSC is true

        // Non user-modifiable (Won't be serialized)
        internal readonly string OSC_MUTE_SELF_PARAMETER_PATH = "/avatar/parameters/MuteSelf";
        internal readonly string OSC_VOICE_PARAMETER_PATH = "/avatar/parameters/Voice";

        internal readonly string SETTINGS_FILENAME = "settings.json"; // User-modifiable settings, generated
        internal readonly string MANIFEST_FILENAME = "vrcmicoverlay.vrmanifest"; // Used to set up SteamVR autostart

        internal readonly string APPLICATION_KEY = "one.raz.vrcmicoverlay";
        internal readonly string OVERLAY_KEY = "one.raz.vrcmicoverlay.mic";
        internal readonly string OVERLAY_NAME = "VRCMicOverlay";
        internal readonly string BINARY_PATH_WINDOWS = "VRCMicOverlay.exe";
        internal readonly string OVERLAY_DESCRIPTION = "OpenVR Overlay to replace the built in VRChat HUD mic icon";

        public void Validate()
        {
            float ClampFloat(float input, float min, float max) => MathF.Max(min, MathF.Min(input, max));

            Configuration defaultConfig = new Configuration();

            const float alphaLimitMin = 0.0f;
            const float alphaLimitMax = 1.0f;
            ICON_MUTED_MAX_ALPHA    = ClampFloat(ICON_MUTED_MAX_ALPHA,   alphaLimitMin, alphaLimitMax);
            ICON_MUTED_MIN_ALPHA    = ClampFloat(ICON_MUTED_MIN_ALPHA,   alphaLimitMin, alphaLimitMax);
            ICON_UNMUTED_MAX_ALPHA  = ClampFloat(ICON_UNMUTED_MAX_ALPHA, alphaLimitMin, alphaLimitMax);
            ICON_UNMUTED_MIN_ALPHA  = ClampFloat(ICON_UNMUTED_MIN_ALPHA, alphaLimitMin, alphaLimitMax);

            const float fadeTimeLimitMin = 0.0f;
            const float fadeTimeLimitMax = 20.0f;
            MIC_MUTED_FADE_START    = ClampFloat(MIC_MUTED_FADE_START,    fadeTimeLimitMin, fadeTimeLimitMax);
            MIC_MUTED_FADE_PERIOD   = ClampFloat(MIC_MUTED_FADE_PERIOD,   fadeTimeLimitMin, fadeTimeLimitMax);
            MIC_UNMUTED_FADE_START  = ClampFloat(MIC_UNMUTED_FADE_START,  fadeTimeLimitMin, fadeTimeLimitMax);
            MIC_UNMUTED_FADE_PERIOD = ClampFloat(MIC_UNMUTED_FADE_PERIOD, fadeTimeLimitMin, fadeTimeLimitMax);

            // Enforce consistent color code formatting
            if (!ICON_TINT_MUTED.StartsWith('#'))   ICON_TINT_MUTED   = '#' + ICON_TINT_MUTED;
            if (!ICON_TINT_UNMUTED.StartsWith('#')) ICON_TINT_UNMUTED = '#' + ICON_TINT_UNMUTED;
            ICON_TINT_MUTED = ICON_TINT_MUTED.ToUpperInvariant();
            ICON_TINT_UNMUTED = ICON_TINT_UNMUTED.ToUpperInvariant();

            /*
            |   ^#(?:[0-9A-F]{2}){3}$   Matches a color hex string of the form #FF33F3
            |
            |   ^                       Match the beginning of the string
            |    #                      Match the literal character '#'
            |     (?:                   Open noncapturing group
            |        [0-9A-F]             Character set: any digits 0-9 or characters A-F (case-sensitive)
            |                {2}          Match exactly 2 of the preceding token (digit/character match)
            |                   )       Close noncapturing group
            |                    {3}    Exactly 3 of the preceding group (hex octets)
            |                       $   End of the string
            */
            const string hexCodeRegex = @"^#(?:[0-9A-F]{2}){3}$";
            bool IsValidHexColor(string color) => System.Text.RegularExpressions.Regex.IsMatch(color, hexCodeRegex);
            if (!IsValidHexColor(ICON_TINT_MUTED))   ICON_TINT_MUTED   = defaultConfig.ICON_TINT_MUTED;
            if (!IsValidHexColor(ICON_TINT_UNMUTED)) ICON_TINT_UNMUTED = defaultConfig.ICON_TINT_UNMUTED;

            const float iconShiftingPeriodLimitMin = 0.1f;
            const float iconShiftingPeriodLimitMax = 86400f; // About a day, in seconds
            ICON_SHIFTING_PERIOD = ClampFloat(ICON_SHIFTING_PERIOD, iconShiftingPeriodLimitMin, iconShiftingPeriodLimitMax);

            const float iconShiftingAmountLimitMin = 0.0f;
            const float iconShiftingAmountLimitMax = 10.0f; // 10 degrees is a LOT to move around the viewsphere
            ICON_SHIFTING_AMOUNT = ClampFloat(ICON_SHIFTING_AMOUNT, iconShiftingAmountLimitMin, iconShiftingAmountLimitMax);

            const float mutedMicThresholdLimitMin = 0.0f;
            const float mutedMicThresholdLimitMax = 1.0f;
            MUTED_MIC_THRESHOLD = ClampFloat(MUTED_MIC_THRESHOLD, mutedMicThresholdLimitMin, mutedMicThresholdLimitMax);

            const float iconSizeLimitMin = 0.01f;
            const float iconSizeLimitMax = 1.0f;
            ICON_SIZE = ClampFloat(ICON_SIZE, iconSizeLimitMin, iconSizeLimitMax);

            const float iconScaleFactorLimitMin = 0.0f;
            const float iconScaleFactorLimitMax = 3.0f;
            ICON_CHANGE_SCALE_FACTOR = ClampFloat(ICON_CHANGE_SCALE_FACTOR, iconScaleFactorLimitMin, iconScaleFactorLimitMax);

            const float iconPositionLimitMin = -2.0f;
            const float iconPositionLimitMax =  2.0f;
            ICON_OFFSET_X = ClampFloat(ICON_OFFSET_X, iconPositionLimitMin, iconPositionLimitMax);
            ICON_OFFSET_Y = ClampFloat(ICON_OFFSET_Y, iconPositionLimitMin, iconPositionLimitMax);
            ICON_OFFSET_Z = ClampFloat(ICON_OFFSET_Z, iconPositionLimitMin, iconPositionLimitMax);

            const float iconUnfadeTimeLimitMin = 0.0f;
            const float iconUnfadeTimeLimitMax = 1.0f;
            ICON_UNFADE_TIME = ClampFloat(ICON_UNFADE_TIME, iconUnfadeTimeLimitMin, iconUnfadeTimeLimitMax);

            const float iconAlphaExponentLimitMin = 0.1f;
            const float iconAlphaExponentLimitMax = 4.0f;
            ICON_ALPHA_EXPONENT = ClampFloat(ICON_ALPHA_EXPONENT, iconAlphaExponentLimitMin, iconAlphaExponentLimitMax);

            const float customMicSfxVolumeLimitMin = 0.0f;
            const float customMicSfxVolumeLimitMax = 1.0f;
            CUSTOM_MIC_SFX_VOLUME = ClampFloat(CUSTOM_MIC_SFX_VOLUME, customMicSfxVolumeLimitMin, customMicSfxVolumeLimitMax);

            bool IsValidFilePathRelative(string filePath) => File.Exists(Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory), filePath));
            if (!IsValidFilePathRelative(FILENAME_IMG_MIC_UNMUTED)) FILENAME_IMG_MIC_UNMUTED = defaultConfig.FILENAME_IMG_MIC_UNMUTED;
            if (!IsValidFilePathRelative(FILENAME_IMG_MIC_MUTED  )) FILENAME_IMG_MIC_MUTED   = defaultConfig.FILENAME_IMG_MIC_MUTED;
            if (!IsValidFilePathRelative(FILENAME_SFX_MIC_UNMUTED)) FILENAME_SFX_MIC_UNMUTED = defaultConfig.FILENAME_SFX_MIC_UNMUTED;
            if (!IsValidFilePathRelative(FILENAME_SFX_MIC_MUTED  )) FILENAME_SFX_MIC_MUTED   = defaultConfig.FILENAME_SFX_MIC_MUTED;
        }
    }
}