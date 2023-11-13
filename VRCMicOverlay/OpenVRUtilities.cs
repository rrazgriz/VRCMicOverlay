using System;
using System.Text;
using System.Text.Json;

using Valve.VR;

namespace Raz.VRCMicOverlay
{
    internal class OVRUtilities
    {
        internal static EVRApplicationError EVRApplicationErrorHandler(EVRApplicationError error)
        {
            if(error != EVRApplicationError.None)
            {
                Console.WriteLine($"STEAMVR APPLICATION ERROR: {error.ToString()}");
            }

            return error;
        }

        internal static EVROverlayError EVROverlayErrorHandler(EVROverlayError error)
        {
            if(error != EVROverlayError.None)
            {
                Console.WriteLine($"STEAMVR OVERLAY ERROR: {error.ToString()}");
            }

            return error;
        }

        internal static void SetupOpenVRAutostart(Configuration config)
        {
            string executablePath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) ?? $".{Path.DirectorySeparatorChar}";

            string manifestPath = Path.Combine(executablePath, config.MANIFEST_FILENAME);

            var manifest = new SteamVR_ManifestFile();
            var manifestApplication = new SteamVR_ManifestFile_Application
            {
                app_key = config.APPLICATION_KEY,
                launch_type = "binary",
                binary_path_windows = config.BINARY_PATH_WINDOWS,
                is_dashboard_overlay = true
            };
            var strings = new SteamVR_ManifestFile_ApplicationString()
            {
                name = config.OVERLAY_NAME,
                description = config.OVERLAY_DESCRIPTION,
            };
            manifestApplication.strings.Add("en_us", strings);
            manifest.applications = new List<SteamVR_ManifestFile_Application> {manifestApplication};
            
            var serializerOptions = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };

            string manifestJsonString = JsonSerializer.Serialize(manifest, serializerOptions);
            File.WriteAllText(manifestPath, manifestJsonString);

            // Set up autolaunch
            if (!OpenVR.Applications.IsApplicationInstalled(config.APPLICATION_KEY))
            {
                // Add our manifest first
                EVRApplicationErrorHandler(OpenVR.Applications.AddApplicationManifest(manifestPath, false));
                EVRApplicationErrorHandler(OpenVR.Applications.SetApplicationAutoLaunch(config.APPLICATION_KEY, true));
            }
            else
            {
                // Check if the autolaunch is set up with the current program location
                bool isAutostartEnabled = OpenVR.Applications.GetApplicationAutoLaunch(config.APPLICATION_KEY);
                StringBuilder binaryPath = new();
                EVRApplicationError dummyApplicationError = EVRApplicationError.None;
                OpenVR.Applications.GetApplicationPropertyString(config.APPLICATION_KEY, EVRApplicationProperty.BinaryPath_String, binaryPath, 255, ref dummyApplicationError);
                string binaryPathTrimmed = Path.GetDirectoryName(binaryPath.ToString());
                
                if (!String.Equals(binaryPathTrimmed, executablePath, StringComparison.Ordinal))
                {
                    EVRApplicationErrorHandler(OpenVR.Applications.RemoveApplicationManifest(Path.Combine($"{binaryPathTrimmed}", config.MANIFEST_FILENAME)));
                    EVRApplicationErrorHandler(OpenVR.Applications.AddApplicationManifest(manifestPath, false));
                    EVRApplicationErrorHandler(OpenVR.Applications.SetApplicationAutoLaunch(config.APPLICATION_KEY, isAutostartEnabled));
                }
            }
        }
    }

    // These classes are modified from https://github.com/ValveSoftware/steamvr_unity_plugin/
    // Used with permission under the BSD 3-Clause license
    internal class SteamVR_ManifestFile
    {
        public List<SteamVR_ManifestFile_Application> applications;
    }

    internal class SteamVR_ManifestFile_Application
    {
        public string app_key;
        public string launch_type;
        public string binary_path_windows;
        public bool is_dashboard_overlay;
        public Dictionary<string, SteamVR_ManifestFile_ApplicationString> strings = new Dictionary<string, SteamVR_ManifestFile_ApplicationString>();
    }

    internal class SteamVR_ManifestFile_ApplicationString
    {
        public string name;
        public string description;
    }
}