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
}