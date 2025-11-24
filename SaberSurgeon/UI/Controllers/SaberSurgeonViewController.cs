using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using UnityEngine;

namespace SaberSurgeon.UI.Controllers
{
    [ViewDefinition("SaberSurgeon.UI.Views.SaberSurgeonSettings.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\SaberSurgeonSettings.bsml")]
    public class SaberSurgeonViewController : BSMLAutomaticViewController
    {

        [UIValue("playTime")]
        public float PlayTime
        {
            get => _playTime;
            set
            {
                _playTime = value;
                NotifyPropertyChanged(nameof(PlayTime));
                Plugin.Log.Info($"Slider changed → PlayTime = {_playTime} minutes");
            }
        }

        private float _playTime = 60f; // default mid-range


        [UIAction("OnStartPlayPressed")]
        private void OnStartPlayPressed()
        {
            Plugin.Log.Info("SaberSurgeon: Start/Play button pressed!");
            Plugin.Log.Info($"Timer set to: {PlayTime} minutes");
            // gameplay logic to start/stop the mod will go here later.
        }
    }
}
