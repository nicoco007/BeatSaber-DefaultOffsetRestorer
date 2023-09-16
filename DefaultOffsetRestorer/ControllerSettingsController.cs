// <copyright file="ControllerSettingsController.cs" company="nicoco007">
// This file is part of DefaultOffsetRestorer.
//
// DefaultOffsetRestorer is free software: you can redistribute it and/or modify it under the terms
// of the GNU General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
//
// DefaultOffsetRestorer is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License along with DefaultOffsetRestorer.
// If not, see https://www.gnu.org/licenses/.
// </copyright>

using System;
using HMUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using Zenject;
using Object = UnityEngine.Object;

namespace DefaultOffsetRestorer
{
    internal class ControllerSettingsController : IInitializable, IDisposable
    {
        private readonly SettingsNavigationController _settingsNavigationController;
        private readonly MainSettingsModelSO _mainSettingsModel;
        private readonly Settings _settings;
        private readonly UnityXRHelper _unityXRHelper;

        private ControllersTransformSettingsViewController? _controllersTransformSettingsViewController;
        private Toggle? _toggle;
        private Button? _button;
        private TextMeshProUGUI? _buttonText;
        private bool _wasEnabled;

        private ControllerSettingsController(SettingsNavigationController settingsNavigationController, MainSettingsModelSO mainSettingsModel, Settings settings, IVRPlatformHelper vrPlatformHelper)
        {
            if (vrPlatformHelper is not UnityXRHelper unityXRHelper)
            {
                throw new ArgumentException($"Expected {nameof(IVRPlatformHelper)} to be {nameof(UnityXRHelper)}", nameof(vrPlatformHelper));
            }

            _settingsNavigationController = settingsNavigationController;
            _mainSettingsModel = mainSettingsModel;
            _settings = settings;
            _unityXRHelper = unityXRHelper;
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            _controllersTransformSettingsViewController = _settingsNavigationController.transform.Find("ControllersTransformSettings").GetComponent<ControllersTransformSettingsViewController>();

            _toggle = CreateToggleSetting();
            (_button, _buttonText) = CreateConvertButton();

            _settingsNavigationController.didFinishEvent += OnDidFinish;
            _settingsNavigationController.didActivateEvent += OnDidActivate;

            _unityXRHelper.controllersDidChangeReferenceEvent += ControllersDidChangeReference;
            _unityXRHelper.controllersDidDisconnectEvent += ControllersDidChangeReference;

            ControllersDidChangeReference();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Object.Destroy(_toggle!.gameObject);
            Object.Destroy(_button!.gameObject);

            _settingsNavigationController.didFinishEvent -= OnDidFinish;
            _settingsNavigationController.didActivateEvent -= OnDidActivate;
        }

        private void ControllersDidChangeReference()
        {
            bool interactable = _unityXRHelper.ControllerFromNode(XRNode.RightHand) != null && OpenVRUtilities.TryGetGripOffset(XRNode.RightHand, out Pose _);
            _toggle!.interactable = interactable;
            _button!.interactable = interactable;
        }

        private void OnDidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            _wasEnabled = _settings.enabled;
            HandleOnValueChanged(_settings.enabled);
        }

        private void OnDidFinish(SettingsNavigationController.FinishAction finishAction)
        {
            if (finishAction is not SettingsNavigationController.FinishAction.Ok and not SettingsNavigationController.FinishAction.Apply)
            {
                _settings.enabled = _wasEnabled;
                _toggle!.isOn = _wasEnabled;
            }
        }

        private Toggle CreateToggleSetting()
        {
            GameObject toggleTemplate = _settingsNavigationController.transform.Find("GraphicSettings/ViewPort/Content/Fullscreen").gameObject;
            Transform parent = _controllersTransformSettingsViewController!.transform.Find("Content");

            GameObject gameObject = Object.Instantiate(toggleTemplate, parent, false);
            GameObject nameText = gameObject.transform.Find("NameText").gameObject;
            GameObject switchView = gameObject.transform.Find("SwitchView").gameObject;
            Object.Destroy(gameObject.GetComponent<BoolSettingsController>());

            gameObject.name = "UseLegacyOffsetsToggle";
            gameObject.SetActive(false);

            RectTransform rectTransfrom = (RectTransform)gameObject.transform;
            rectTransfrom.anchoredPosition = new Vector2(0, -43.5f);

            AnimatedSwitchView animatedSwitchView = switchView.GetComponent<AnimatedSwitchView>();
            Toggle toggle = switchView.GetComponent<Toggle>();
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(animatedSwitchView.HandleOnValueChanged);
            toggle.isOn = _settings.enabled;
            toggle.interactable = true;
            animatedSwitchView.enabled = true; // force refresh the UI state

            Object.Destroy(nameText.GetComponent("LocalizedTextMeshProUGUI"));

            TextMeshProUGUI text = nameText.GetComponent<TextMeshProUGUI>();
            text.text = "Use Legacy Default Offsets";
            text.richText = true;
            text.overflowMode = TextOverflowModes.Ellipsis;

            gameObject.GetComponent<LayoutElement>().preferredWidth = 90;
            gameObject.SetActive(true);

            toggle.onValueChanged.AddListener(HandleOnValueChanged);

            return toggle;
        }

        private (Button button, TextMeshProUGUI text) CreateConvertButton()
        {
            Transform parent = _controllersTransformSettingsViewController!.transform.Find("Content/ResetButton");
            GameObject gameObject = Object.Instantiate(parent.gameObject, _controllersTransformSettingsViewController!.transform.Find("Content"), false);
            gameObject.name = "ConvertOffsetsButton";

            RectTransform rectTransform = (RectTransform)gameObject.transform;
            rectTransform.offsetMin = new Vector2(0, -58);
            rectTransform.offsetMax = new Vector2(0, -53);
            rectTransform.anchorMin = new Vector2(0.5f, 1);
            rectTransform.anchorMax = new Vector2(0.5f, 1);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // this is really stupid but it's to work around the ImageView's preferred size being slightly too big so the ContentSizeFitter doesn't shrink enough
            rectTransform.localScale = Vector3.one * 0.5f;

            Transform text = gameObject.transform.Find("Text");
            Object.Destroy(text.GetComponent("LocalizedTextMeshProUGUI"));

            TextMeshProUGUI textMesh = text.GetComponent<TextMeshProUGUI>();
            textMesh.fontSize = 7;
            textMesh.fontStyle = FontStyles.Italic;

            ContentSizeFitter contentSizeFitter = gameObject.AddComponent<ContentSizeFitter>();
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Button button = gameObject.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleButtonClicked);

            gameObject.SetActive(true);

            return (button, textMesh);
        }

        private void HandleButtonClicked()
        {
            // controller offsets are based on the right hand
            if (!OpenVRUtilities.TryGetGripOffset(XRNode.RightHand, out Pose poseOffset))
            {
                return;
            }

            bool newValue = !_settings.enabled;
            Vector3 controllerPosition = _mainSettingsModel.controllerPosition;
            Vector3 controllerRotation = _mainSettingsModel.controllerRotation;

            if (newValue)
            {
                (controllerPosition, controllerRotation) = OffsetConverter.ConvertToLegacy(_unityXRHelper, poseOffset, controllerPosition, controllerRotation);
            }
            else
            {
                (controllerPosition, controllerRotation) = OffsetConverter.ConvertFromLegacy(_unityXRHelper, poseOffset, controllerPosition, controllerRotation);
            }

            _controllersTransformSettingsViewController!._posXSlider.value = controllerPosition.x * 100f;
            _controllersTransformSettingsViewController!._posYSlider.value = controllerPosition.y * 100f;
            _controllersTransformSettingsViewController!._posZSlider.value = controllerPosition.z * 100f;
            _controllersTransformSettingsViewController!._rotXSlider.value = Clamp180(controllerRotation.x);
            _controllersTransformSettingsViewController!._rotYSlider.value = Clamp180(controllerRotation.y);
            _controllersTransformSettingsViewController!._rotZSlider.value = Clamp180(controllerRotation.z);

            _mainSettingsModel.controllerPosition.value = controllerPosition;
            _mainSettingsModel.controllerRotation.value = controllerRotation;

            _toggle!.isOn = newValue;
        }

        private void HandleOnValueChanged(bool value)
        {
            _settings.enabled = value;
            _buttonText!.text = value ? "Convert from Legacy Offsets" : "Convert to Legacy Offsets";
            _unityXRHelper.RefreshControllersReference();
        }

        private float Clamp180(float angle)
        {
            angle %= 360;

            return angle switch
            {
                > 180 => angle - 360,
                < -180 => angle + 360,
                _ => angle,
            };
        }
    }
}
