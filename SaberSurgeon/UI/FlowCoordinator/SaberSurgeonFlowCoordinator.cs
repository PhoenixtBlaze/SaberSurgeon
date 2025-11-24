using HMUI;
using BeatSaberMarkupLanguage;
using SaberSurgeon.UI.Controllers;

namespace SaberSurgeon.UI.FlowCoordinators
{
    public class SaberSurgeonFlowCoordinator : FlowCoordinator
    {
        private SaberSurgeonViewController _viewController;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                // Set the title
                SetTitle("Saber Surgeon");

                // Enable the back button
                showBackButton = true;

                // Create the view controller
                _viewController = BeatSaberUI.CreateViewController<SaberSurgeonViewController>();
            }

            if (addedToHierarchy)
            {
                // Show the view controller
                ProvideInitialViewControllers(_viewController);
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            // Dismiss this flow coordinator
            BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this);
        }
    }
}
