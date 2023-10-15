using System;
using System.Runtime.InteropServices;

namespace Raz.VRCMicOverlay
{
    internal static class WindowsUtilities
    {
        public static bool SetWindowState(IntPtr hWnd, CMDSHOW command)
        {
            return ShowWindow(hWnd, (int)command);
        }

        [DllImport("User32.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow([In] IntPtr hWnd, [In] int nCmdShow);
        
        [DllImport("kernel32.dll")]
        public static extern IntPtr GetConsoleWindow();

        // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-showwindow?redirectedfrom=MSDN
        public enum CMDSHOW
        {
            SW_HIDE = 0,
            SW_SHOWNORMAL = 1,
            SW_SHOWMINIMIZED = 2,
            SW_SHOWMAXIMIZED = 3,
            SW_SHOWNOACTIVATE = 4,
            SW_MINIMIZE = 6,
            SW_SHOWMINNOACTIVE = 7,
            SW_SHOWNA = 8,
            SW_RESTORE = 9,
            SW_SHOWDEFAULT = 10,
            SW_FORCEMINIMIZE = 11
        }

        // thanks to https://stackoverflow.com/questions/34277066/how-do-i-fade-out-the-audio-of-a-wav-file-using-soundplayer-instead-of-stopping
        [DllImport("winmm.dll", EntryPoint = "waveOutGetVolume")]
        private static extern int WaveOutGetVolume(IntPtr hwo, out uint dwVolume);

        [DllImport("winmm.dll", EntryPoint = "waveOutSetVolume")]
        private static extern int WaveOutSetVolume(IntPtr hwo, uint dwVolume);

        public static int SetVolume(float volume)
        {
            float clampedVolume = MathF.Min(MathF.Max(volume, 0f), 1f);
            ushort channelVolume = (ushort)(ushort.MaxValue * clampedVolume);
            uint vol = (uint)channelVolume | ((uint)channelVolume << 16);
            return WaveOutSetVolume(IntPtr.Zero, vol);
        }
    }
}