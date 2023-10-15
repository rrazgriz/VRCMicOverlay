namespace Raz.VRCMicOverlay
{
    internal class Configuration
    {
        // User-Configurable Settings
        public float ICON_MUTED_MAX_ALPHA = 0.50f;      // Max icon transparency (0-1) while muted
        public float ICON_MUTED_MIN_ALPHA = 0.00f;      // Min icon transparency (0-1) while muted
        public float ICON_UNMUTED_MAX_ALPHA = 0.75f;    // Max icon transparency (0-1) while unmuted
        public float ICON_UNMUTED_MIN_ALPHA = 0.05f;    // Min icon transparency (0-1) while unmuted

        public bool USE_CUSTOM_MIC_SFX = false;     // If true, will use custom sfx wav files when muting/unmuting
        public float CUSTOM_MIC_SFX_VOLUME = 0.65f; // Volume (0-1) of custom sfx

        // VRChat doesn't output the Voice parameter while muted, so we have to read from a device ourselves
        public string AUDIO_DEVICE_STARTS_WITH = "";    // If this is blank, it'll use the default audio device
        public float MUTED_MIC_THRESHOLD = 0.1f;        // Threshold (0-1) for the mic icon lighting up while muted

        public float ICON_CHANGE_SCALE_FACTOR = 1.25f; // Scale icon by this factor when changing between mute/unmute. Set to 1.0 to disable

        public float ICON_SIZE = 0.05f;         // Size, square, of icon overlay (in meters)
        public float ICON_OFFSET_X = -0.37f;    // Distance left/right of head center axis (negative is left)
        public float ICON_OFFSET_Y = -0.26f;    // Distance above/below head axis (negative is in front)
        public float ICON_OFFSET_Z = -0.92f;    // Distance in front of the head (negative is in front)

        public bool RESTART_FADE_TIMER_ON_STATE_CHANGE = true; // Whether to restart the fade timer when changing from mute/unmute. Play with this to see if you like it
        public float MIC_MUTED_FADE_START    = 1.0f;    // Time before icon starts fading while muted (times are in seconds)
        public float MIC_MUTED_FADE_PERIOD   = 2.0f;    // Time to fade from max to min alpha while muted
        public float MIC_UNMUTED_FADE_START  = 0.3f;    // Time before icon starts fading while unmuted
        public float MIC_UNMUTED_FADE_PERIOD = 1.0f;    // Time to fade from max to min alpha while unmuted

        public float ICON_UNFADE_TIME = 0.05f; // Time to go from faded to unfaded

        public string FILENAME_SFX_MIC_UNMUTED = "sfx-unmute.wav";          // Custom sound for unmute (must be wav)
        public string FILENAME_SFX_MIC_MUTED = "sfx-mute.wav";              // Custom sound for mute (must be wav)
        public string FILENAME_IMG_MIC_UNMUTED = "microphone-unmuted.png";  // Custom icon while unmuted (should probably only be png)
        public string FILENAME_IMG_MIC_MUTED = "microphone-muted.png";      // Custom icon while muted (should probably only be png)

        public bool USE_LEGACY_OSC = false;         // Will use OSCQuery if false, recommend keeping false as OSCQuery is integrated in VRChat now
        public int LEGACY_OSC_LISTEN_PORT = 9001;   // Port to listen on, configurable for OSC routers. Only used if USE_LEGACY_OSC is true

        // Non user-modifiable
        internal readonly string OSC_MUTE_SELF_PARAMETER_PATH = "/avatar/parameters/MuteSelf";
        internal readonly string OSC_VOICE_PARAMETER_PATH = "/avatar/parameters/Voice";

        internal readonly string SETTINGS_FILENAME = "settings.json"; // User-modifiable settings, generated
        internal readonly string MANIFEST_FILENAME = "vrcmicoverlay.vrmanifest"; // Used to set up SteamVR autostart

        internal readonly string APPLICATION_KEY = "one.raz.vrcmicoverlay";
        internal readonly string OVERLAY_KEY = "one.raz.vrcmicoverlay.mic";
        internal readonly string OVERLAY_NAME = "VRCMicOverlay";
    }
}