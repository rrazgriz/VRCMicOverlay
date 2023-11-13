using System;

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
        public float R;
        public float G;
        public float B;
        public float A;

        public ColorFloat(float r, float g, float b, float a)
        {
            R = MathF.Min(MathF.Max(0.0f, r), 1.0f);
            G = MathF.Min(MathF.Max(0.0f, g), 1.0f);
            B = MathF.Min(MathF.Max(0.0f, b), 1.0f);
            A = MathF.Min(MathF.Max(0.0f, a), 1.0f);
        }
    }
}