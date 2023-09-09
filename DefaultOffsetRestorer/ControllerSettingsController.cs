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
        private readonly ControllersTransformSettingsViewController _controllersTransformSettingsViewController;
        private readonly MainSettingsModelSO _mainSettingsModel;
        private readonly Settings _settings;
        private readonly UnityXRHelper _unityXRHelper;

        private Toggle? _toggle;
        private GameObject? _rootObject;
        private bool _wasEnabled;

        private ControllerSettingsController(SettingsNavigationController settingsNavigationController, MainSettingsModelSO mainSettingsModel, Settings settings, IVRPlatformHelper vrPlatformHelper)
        {
            if (vrPlatformHelper is not UnityXRHelper unityXRHelper)
            {
                throw new ArgumentException($"Expected {nameof(IVRPlatformHelper)} to be {nameof(UnityXRHelper)}", nameof(vrPlatformHelper));
            }

            _settingsNavigationController = settingsNavigationController;
            _controllersTransformSettingsViewController = settingsNavigationController.transform.Find("ControllersTransformSettings").GetComponent<ControllersTransformSettingsViewController>();
            _mainSettingsModel = mainSettingsModel;
            _settings = settings;
            _unityXRHelper = unityXRHelper;
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            Transform parent = _settingsNavigationController.transform.Find("ControllersTransformSettings/Content");
            _rootObject = CreateToggleSetting(parent);

            _settingsNavigationController.didFinishEvent += OnDidFinish;
            _settingsNavigationController.didActivateEvent += OnDidActivate;

            _unityXRHelper.controllersDidChangeReferenceEvent += ControllersDidChangeReference;
            _unityXRHelper.controllersDidDisconnectEvent += ControllersDidChangeReference;

            ControllersDidChangeReference();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Object.Destroy(_rootObject);

            _settingsNavigationController.didFinishEvent -= OnDidFinish;
            _settingsNavigationController.didActivateEvent -= OnDidActivate;
        }

        private void ControllersDidChangeReference()
        {
            _toggle!.interactable = _unityXRHelper.ControllerFromNode(XRNode.RightHand) != null && OpenVRUtilities.TryGetGripOffset(XRNode.RightHand, out Pose _);
        }

        private void OnDidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            _wasEnabled = _settings.enabled;
        }

        private CustomFormatRangeValuesSlider GetSlider(Transform parent, string name)
        {
            return parent.Find($"{name}/Slider").GetComponent<CustomFormatRangeValuesSlider>();
        }

        private void OnDidFinish(SettingsNavigationController.FinishAction finishAction)
        {
            if (finishAction is not SettingsNavigationController.FinishAction.Ok or SettingsNavigationController.FinishAction.Apply)
            {
                _settings.enabled = _wasEnabled;
                _toggle!.isOn = _wasEnabled;
            }
        }

        private GameObject CreateToggleSetting(Transform parent)
        {
            GameObject toggleTemplate = _settingsNavigationController.transform.Find("GraphicSettings/ViewPort/Content/Fullscreen").gameObject;

            GameObject gameObject = Object.Instantiate(toggleTemplate, parent, false);
            GameObject nameText = gameObject.transform.Find("NameText").gameObject;
            GameObject switchView = gameObject.transform.Find("SwitchView").gameObject;
            Object.Destroy(gameObject.GetComponent<BoolSettingsController>());

            gameObject.name = "UseLegacyOffsetsToggle";
            gameObject.SetActive(false);

            RectTransform rectTransfrom = (RectTransform)gameObject.transform;
            rectTransfrom.anchoredPosition = new Vector2(0, -45);

            AnimatedSwitchView animatedSwitchView = switchView.GetComponent<AnimatedSwitchView>();
            _toggle = switchView.GetComponent<Toggle>();
            _toggle.onValueChanged.RemoveAllListeners();
            _toggle.onValueChanged.AddListener(animatedSwitchView.HandleOnValueChanged);
            _toggle.isOn = _settings.enabled;
            _toggle.interactable = true;
            animatedSwitchView.enabled = true; // force refresh the UI state

            Object.Destroy(nameText.GetComponent("LocalizedTextMeshProUGUI"));

            TextMeshProUGUI text = nameText.GetComponent<TextMeshProUGUI>();
            text.text = "Use Legacy Default Offsets";
            text.richText = true;
            text.overflowMode = TextOverflowModes.Ellipsis;

            gameObject.GetComponent<LayoutElement>().preferredWidth = 90;
            gameObject.SetActive(true);

            _toggle.onValueChanged.AddListener(HandleOnValueChanged);

            return gameObject;
        }

        private void HandleOnValueChanged(bool value)
        {
            if (value == _settings.enabled)
            {
                return;
            }

            Vector3 controllerPosition = _mainSettingsModel.controllerPosition;
            Vector3 controllerRotation = _mainSettingsModel.controllerRotation;

            if (!OpenVRUtilities.TryGetGripOffset(XRNode.RightHand, out Pose poseOffset))
            {
                return;
            }

            if (value)
            {
                (controllerPosition, controllerRotation) = OffsetConverter.ConvertToLegacy(_unityXRHelper, poseOffset, controllerPosition, controllerRotation);
            }
            else
            {
                (controllerPosition, controllerRotation) = OffsetConverter.ConvertFromLegacy(_unityXRHelper, poseOffset, controllerPosition, controllerRotation);
            }

            _settings.enabled = value;

            _controllersTransformSettingsViewController._posXSlider.value = controllerPosition.x * 100f;
            _controllersTransformSettingsViewController._posYSlider.value = controllerPosition.y * 100f;
            _controllersTransformSettingsViewController._posZSlider.value = controllerPosition.z * 100f;
            _controllersTransformSettingsViewController._rotXSlider.value = Clamp180(controllerRotation.x);
            _controllersTransformSettingsViewController._rotYSlider.value = Clamp180(controllerRotation.y);
            _controllersTransformSettingsViewController._rotZSlider.value = Clamp180(controllerRotation.z);

            _mainSettingsModel.controllerPosition.value = controllerPosition;
            _mainSettingsModel.controllerRotation.value = controllerRotation;

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
