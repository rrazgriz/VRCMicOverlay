using System;
using System.Drawing;

namespace Raz.VRCMicOverlay
{
    internal enum MuteState { MUTED, UNMUTED }

    internal struct IconState
    {
        public IconState()
        {
            iconScaleFactorCurrent = 1.0f;
            iconScaleFactorTarget = 1.0f;
            iconScaleFactorRate = 1.0f;

            iconAlphaFactorCurrent = 0.0f;
            iconAlphaFactorTarget = 1.0f;
            iconAlphaFactorRate = 10.0f;
        }

        public void Update(float deltaTime)
        {
            // Calculate icon Alpha
            float iconAlphaDelta = iconAlphaFactorRate * deltaTime;
            if (Math.Abs(iconAlphaFactorTarget - iconAlphaFactorCurrent) > iconAlphaDelta)
            {
                iconAlphaFactorCurrent += iconAlphaDelta * Math.Sign(iconAlphaFactorTarget - iconAlphaFactorCurrent);
            }
            else
            {
                // Snap to the target value (so we don't float above zero)
                iconAlphaFactorCurrent = iconAlphaFactorTarget;
            }

            // Calculate icon Scaling
            float iconScaleDelta = iconScaleFactorRate * deltaTime;
            if (Math.Abs(iconScaleFactorTarget - iconScaleFactorCurrent) > iconScaleDelta)
            {
                iconScaleFactorCurrent += iconScaleDelta * Math.Sign(iconScaleFactorTarget - iconScaleFactorCurrent);
            }
            else
            {
                iconScaleFactorCurrent = iconScaleFactorTarget;
            }
        }

        public float iconScaleFactorCurrent;
        public float iconScaleFactorTarget;
        public float iconScaleFactorRate;

        public float iconAlphaFactorCurrent;
        public float iconAlphaFactorTarget;
        public float iconAlphaFactorRate;
    }

    internal struct MicState
    {
        public MicState()
        {
            deviceMicLevel = 0f;
            vrcMicLevel = 0f;
            mutedMicLevelTimer = 0f;
            unmutedMicLevelTimer = 0f;
            vrcMuteState = MuteState.MUTED;
        }

        public float deviceMicLevel;
        public float vrcMicLevel;
        public float mutedMicLevelTimer;
        public float unmutedMicLevelTimer;
        public MuteState vrcMuteState;
    }

    internal struct ColorFloat
    {
        // Gamma representation (?)
        public float R;
        public float G;
        public float B;
        public float A;

        public ColorFloat(float r = 1.0f, float g = 1.0f, float b = 1.0f, float a = 1.0f)
        {
            R = MathF.Min(MathF.Max(0.0f, r), 1.0f);
            G = MathF.Min(MathF.Max(0.0f, g), 1.0f);
            B = MathF.Min(MathF.Max(0.0f, b), 1.0f);
            A = MathF.Min(MathF.Max(0.0f, a), 1.0f);
        }

        public ColorFloat(Color color, bool convertToGamma = true)
        {
            float gamma = convertToGamma ? 2.2f : 1.0f;
            // Have to gamma-fy these
            R = MathF.Pow(color.R/255f, gamma);
            G = MathF.Pow(color.G/255f, gamma);
            B = MathF.Pow(color.B/255f, gamma);
            A = color.A;
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    internal class MuteUnmuteSoundPlayer
    {
        System.Media.SoundPlayer sfxMute;
        System.Media.SoundPlayer sfxUnmute;

        public void TryPlayMuteSound()
        {
            if (sfxMute == null)
            {
                return;
            }

            sfxMute.Play();
        }

        public void TryPlayUnmuteSound()
        {
            if (sfxUnmute == null)
            {
                return;
            }

            sfxUnmute.Play();
        }

        public MuteUnmuteSoundPlayer(string muteSoundPath, string unmuteSoundPath)
        {
            try
            {
                sfxMute = new(muteSoundPath);
                sfxUnmute = new(unmuteSoundPath);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error setting up {nameof(MuteUnmuteSoundPlayer)}: ");
                Console.WriteLine(e);
            }
        }
    }
}