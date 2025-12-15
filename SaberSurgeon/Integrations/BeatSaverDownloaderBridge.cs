using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SaberSurgeon.Integrations
{
    /// <summary>
    /// Safe bridge to BeatSaverDownloader with version compatibility checks.
    /// </summary>
    internal static class BeatSaverDownloaderBridge
    {
        private static Assembly _bsdAsm;
        private static Type _songDownloaderType;
        private static object _songDownloaderInstance;
        private static MethodInfo _downloadMethod;

        // Version tracking for diagnostics
        private static string _detectedVersion = "unknown";

        public static bool IsAvailable => Resolve();

        public static bool TryDownloadByKey(string bsrKey, out string reason)
        {
            reason = null;

            if (string.IsNullOrWhiteSpace(bsrKey))
            {
                reason = "BSR key is empty.";
                return false;
            }

            if (!Resolve())
            {
                reason = $"BeatSaverDownloader not found/loaded (detected version: {_detectedVersion}).";
                Plugin.Log.Warn($"BeatSaverDownloaderBridge: {reason}");
                return false;
            }

            // Resolve download method once with validation
            if (_downloadMethod == null)
            {
                if (!TryFindDownloadMethod(out reason))
                {
                    Plugin.Log.Warn($"BeatSaverDownloaderBridge: {reason}");
                    return false;
                }
            }

            try
            {
                var target = _downloadMethod.IsStatic ? null : _songDownloaderInstance;

                // Invoke with error handling
                var result = _downloadMethod.Invoke(target, new object[] { bsrKey });

                Plugin.Log.Info($"BeatSaverDownloaderBridge: Successfully queued download for '{bsrKey}'");
                return true;
            }
            catch (TargetInvocationException tie)
            {
                reason = $"BeatSaverDownloader threw exception: {tie.InnerException?.Message ?? tie.Message}";
                Plugin.Log.Error($"BeatSaverDownloaderBridge: {reason}");
                return false;
            }
            catch (Exception ex)
            {
                reason = $"BeatSaverDownloader invoke failed: {ex.GetType().Name} - {ex.Message}";
                Plugin.Log.Error($"BeatSaverDownloaderBridge: {reason}");
                return false;
            }
        }

        /// <summary>
        /// Find the correct Download method with signature checking.
        /// </summary>
        private static bool TryFindDownloadMethod(out string reason)
        {
            reason = null;

            if (_songDownloaderType == null)
            {
                reason = "SongDownloader type not resolved.";
                return false;
            }

            // Try multiple common method names and signatures
            var candidates = _songDownloaderType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Where(m =>
                {
                    if (m == null) return false;
                    if (m.Name == null) return false;

                    // Check name contains "download" (case-insensitive)
                    if (!m.Name.Contains("Download", StringComparison.OrdinalIgnoreCase))
                        return false;

                    // Check signature: single string parameter
                    var ps = m.GetParameters();
                    if (ps.Length != 1 || ps[0].ParameterType != typeof(string))
                        return false;

                    // Valid candidate
                    return true;
                })
                .ToArray();

            if (candidates.Length == 0)
            {
                reason = $"No Download(string) method found on BeatSaverDownloader type. " +
                         $"Available methods: {string.Join(", ", _songDownloaderType.GetMethods().Select(m => m.Name).Distinct())}";
                return false;
            }

            if (candidates.Length > 1)
            {
                Plugin.Log.Warn($"BeatSaverDownloaderBridge: Multiple Download candidates found, using first: {candidates[0].Name}");
            }

            _downloadMethod = candidates.First();
            Plugin.Log.Info($"BeatSaverDownloaderBridge: Found download method: {_downloadMethod.Name}");
            return true;
        }

        private static bool Resolve()
        {
            if (_songDownloaderType != null)
                return true;

            if (_bsdAsm == null)
            {
                _bsdAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a =>
                        a != null &&
                        a.GetName() != null &&
                        a.GetName().Name != null &&
                        a.GetName().Name.IndexOf("BeatSaverDownloader", StringComparison.OrdinalIgnoreCase) >= 0);

                if (_bsdAsm == null)
                {
                    _detectedVersion = "not installed";
                    return false;
                }

                // Extract version if available
                try
                {
                    _detectedVersion = _bsdAsm.GetName().Version?.ToString() ?? "unknown version";
                }
                catch
                {
                    _detectedVersion = "unknown version";
                }

                Plugin.Log.Info($"BeatSaverDownloaderBridge: Detected BeatSaverDownloader v{_detectedVersion}");
            }

            if (_songDownloaderType == null)
            {
                _songDownloaderType = _bsdAsm.GetTypes()
                    .FirstOrDefault(t =>
                        t != null &&
                        t.Name != null &&
                        t.Name.IndexOf("SongDownloader", StringComparison.OrdinalIgnoreCase) >= 0);

                if (_songDownloaderType == null)
                {
                    Plugin.Log.Warn("BeatSaverDownloaderBridge: SongDownloader type not found in assembly");
                    return false;
                }

                // Try to get singleton Instance property if present
                var instProp = _songDownloaderType.GetProperty(
                    "Instance",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                _songDownloaderInstance = instProp?.GetValue(null, null);

                // Log resolution success
                Plugin.Log.Info("BeatSaverDownloaderBridge: Successfully resolved BeatSaverDownloader");
                return true;
            }

            return true;
        }
    }
}
