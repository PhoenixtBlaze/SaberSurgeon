using HMUI;
using BeatSaberMarkupLanguage;
using SaberSurgeon.UI.Controllers;
using UnityEngine;
using Zenject;
using SaberSurgeon.Gameplay;

namespace SaberSurgeon.UI.FlowCoordinators
{
    public class SaberSurgeonFlowCoordinator : FlowCoordinator
    {
        private SaberSurgeonViewController _viewController;
        private SaberSurgeonCooldownViewController _cooldownViewController;

        [Inject] private GameplaySetupViewController _gameplaySetupViewController;
        [Inject] private MenuTransitionsHelper _menuTransitionsHelper;
        [Inject] private EnvironmentsListModel _environmentsListModel;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                SetTitle("Saber Surgeon");
                showBackButton = true;

                _viewController = BeatSaberUI.CreateViewController<SaberSurgeonViewController>();
                _cooldownViewController = BeatSaberUI.CreateViewController<SaberSurgeonCooldownViewController>();

                GameplayManager.GetInstance().SetDependencies(_menuTransitionsHelper, _environmentsListModel);
            }

            if (addedToHierarchy)
            {
                _gameplaySetupViewController.Setup(
                    showModifiers: true,
                    showEnvironmentOverrideSettings: true,
                    showColorSchemesSettings: true,
                    showMultiplayer: false,
                    playerSettingsPanelLayout: PlayerSettingsPanelController.PlayerSettingsPanelLayout.Singleplayer
                );

                // center = SaberSurgeon, left = gameplay setup, right = cooldowns
                ProvideInitialViewControllers(
                    _viewController,
                    _gameplaySetupViewController,
                    _cooldownViewController
                );
            }
        }
        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this);
        }
    }
}
