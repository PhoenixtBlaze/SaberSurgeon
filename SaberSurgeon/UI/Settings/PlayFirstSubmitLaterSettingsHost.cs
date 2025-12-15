using BeatSaberMarkupLanguage.Attributes;
using SaberSurgeon.Gameplay;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SaberSurgeon.UI.Settings
{
    // Plain POCO + INotifyPropertyChanged. Works for BSML GameplaySetup tabs.
    internal sealed class PlayFirstSubmitLaterSettingsHost : INotifyPropertyChanged
    {
        private static PlayFirstSubmitLaterSettingsHost _instance;

        public static PlayFirstSubmitLaterSettingsHost Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new PlayFirstSubmitLaterSettingsHost();
                return _instance;
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        [UIValue("enabled")]
        public bool Enabled
        {
            get => Plugin.Settings?.PlayFirstSubmitLaterEnabled ?? true;
            set
            {
                if (Plugin.Settings != null) Plugin.Settings.PlayFirstSubmitLaterEnabled = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(StatusText));
            }
        }

        [UIValue("scoreSubmissionEnabled")]
        public bool ScoreSubmissionEnabled
        {
            get => Plugin.Settings?.ScoreSubmissionEnabled ?? true;
            set
            {
                if (Plugin.Settings != null) Plugin.Settings.ScoreSubmissionEnabled = value;

                // Apply immediately, independent of other SaberSurgeon features:
                if (value) PlayFirstSubmitLaterManager.EnableSubmission();
                else PlayFirstSubmitLaterManager.DisableSubmission();

                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(StatusText));
            }
        }

        [UIValue("autoPauseOnMapEnd")]
        public bool AutoPauseOnMapEnd
        {
            get => Plugin.Settings?.AutoPauseOnMapEnd ?? true;
            set
            {
                if (Plugin.Settings != null) Plugin.Settings.AutoPauseOnMapEnd = value;
                NotifyPropertyChanged();
            }
        }
         
        

        [UIValue("statusText")]
        public string StatusText
        {
            get
            {
                if (!(Plugin.Settings?.PlayFirstSubmitLaterEnabled ?? true))
                    return "<color=grey>Module disabled</color>";

                return (Plugin.Settings?.ScoreSubmissionEnabled ?? true)
                    ? "<color=green>Scores will submit</color>"
                    : "<color=orange>Scores will NOT submit</color>";
            }
        }
    }
}
