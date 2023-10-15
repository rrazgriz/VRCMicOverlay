using System;

using NAudio.Wave;

namespace Raz.VRCMicOverlay
{
    internal class MicListener
    {
        public float MicLevel { get; private set; }

        internal WaveInEvent waveIn;

        public void SetupMicListener(Configuration configuration)
        {
            Console.WriteLine("\nAudio Device Selection:");
            int deviceID = -1;
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var deviceCapabilities = WaveInEvent.GetCapabilities(i);
                string deviceName = deviceCapabilities.ProductName;
                if (deviceName.StartsWith(configuration.AUDIO_DEVICE_STARTS_WITH) && configuration.AUDIO_DEVICE_STARTS_WITH != "")
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
            if(deviceID < 0) Console.WriteLine($"Audio Device \"{configuration.AUDIO_DEVICE_STARTS_WITH}\" not matched, using default recording device.\n");

            waveIn = new WaveInEvent
            {
                DeviceNumber = deviceID,
                // WaveFormat = new WaveFormat(rate: 44100, bits: 16, channels: 2),
                BufferMilliseconds = 50
            };

            waveIn.DataAvailable += CalculatePeakMicLevel;
            waveIn.StartRecording();
        }

        private void CalculatePeakMicLevel(object sender, WaveInEventArgs args)
        {
            const float maxValue = 32767;
            const int bytesPerSample = 2;
            
            int peakValue = 0;
            for (int index = 0; index < args.BytesRecorded; index += bytesPerSample)
            {
                int value = BitConverter.ToInt16(args.Buffer, index);
                peakValue = Math.Max(peakValue, value);
            }

            MicLevel = peakValue / maxValue;
        }
    }
}