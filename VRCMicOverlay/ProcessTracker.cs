using System;
using System.Diagnostics;

namespace Raz.VRCMicOverlay
{
    internal class ProcessTracker
    {
        private bool isProcessRunning;
        public bool IsProcessRunning 
        {
            get 
            {
                PeriodicCheck();
                return isProcessRunning; 
            } 
            private set { isProcessRunning = value; } 
        }
        public float ProcessCheckInterval { get; set; }

        internal readonly string processName;
        internal Stopwatch stopwatch = new Stopwatch();

        public ProcessTracker(string _processName, float _processCheckInterval)
        {
            processName = _processName;
            ProcessCheckInterval = _processCheckInterval;
            IsProcessRunning = CheckIfProcessIsRunning(processName);
            PrintProcessSatus(isProcessRunning);
            stopwatch.Start();
        }

        private bool CheckIfProcessIsRunning(string processName)
        {
            Process[] pname = Process.GetProcessesByName(processName);
            return pname.Length > 0;
        }

        public void PeriodicCheck()
        {
            if (stopwatch.Elapsed.TotalSeconds > ProcessCheckInterval)
            {
                stopwatch.Restart();

                bool isProcessRunningNow = CheckIfProcessIsRunning(processName);

                if (isProcessRunningNow != isProcessRunning)
                {
                    PrintProcessSatus(isProcessRunningNow);
                    IsProcessRunning = isProcessRunningNow;
                }
            }
        }

        private void PrintProcessSatus(bool isRunning)
        {
            if (isRunning)
                Console.WriteLine($"{processName} Process Detected!");
            else
                Console.WriteLine($"{processName} Process NOT Detected!");
        }
    }
}