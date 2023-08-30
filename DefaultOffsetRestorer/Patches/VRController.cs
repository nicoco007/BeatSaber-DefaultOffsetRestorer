// <copyright file="VRController.cs" company="nicoco007">
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
using System.Runtime.InteropServices;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.XR;
using Valve.VR;

namespace DefaultOffsetRestorer.Patches
{
    /// <summary>
    /// Applies the user's controller offsets the same way as they used to be before Beat Saber 1.29.4.
    /// </summary>
    [HarmonyPatch(typeof(VRController), nameof(VRController.TryGetControllerOffset))]
    internal static class VRController_TryGetControllerOffset
    {
        private static readonly uint kInputOriginInfoStructSize = (uint)Marshal.SizeOf(typeof(InputOriginInfo_t));
        private static readonly string[] kOffsetComponentNames = new[] { "openxr_grip", "grip" };

        private static bool Prefix(VRController __instance, ref bool __result, out Pose poseOffset)
        {
            if (__instance._vrPlatformHelper is not UnityXRHelper unityXRHelper)
            {
                poseOffset = default;
                return true;
            }

            __result = GetGripOffset(__instance._node, out poseOffset);

            if (__instance._node == XRNode.LeftHand)
            {
                poseOffset = VRController.InvertControllerPose(poseOffset);
            }

            UnityXRController controller = unityXRHelper.ControllerFromNode(__instance._node);

            if (__instance._transformOffset != null)
            {
                poseOffset = AdjustControllerPose(controller, poseOffset, __instance._transformOffset.positionOffset, __instance._transformOffset.rotationOffset);
            }
            else
            {
                poseOffset = AdjustControllerPose(controller, poseOffset, Vector3.zero, Vector3.zero);
            }

            if (__instance._node == XRNode.LeftHand)
            {
                poseOffset = VRController.InvertControllerPose(poseOffset);
            }

            return false;
        }

        // based on OpenVRHelper's AdjustControllerTransform
        private static Pose AdjustControllerPose(UnityXRController controller, Pose poseOffset, Vector3 position, Vector3 rotation)
        {
            if (controller.manufacturerName == UnityXRHelper.VRControllerManufacturerName.Valve)
            {
                rotation += new Vector3(-16.3f, 0f, 0f);
                position += new Vector3(0f, 0.022f, -0.01f);
            }
            else
            {
                rotation += new Vector3(-4.3f, 0f, 0f);
                position += new Vector3(0f, -0.008f, 0f);
            }

            // The original code does transform.Rotate(rotation) then transform.Translate(position)
            // so we have to rotate the position by both the pose offset and the rotation.
            return new Pose(poseOffset.position + (poseOffset.rotation * Quaternion.Euler(rotation) * position), poseOffset.rotation * Quaternion.Euler(rotation));
        }

        private static bool GetGripOffset(XRNode node, out Pose poseOffset)
        {
            if (OpenVR.Input == null || !OpenVR.System.IsInputAvailable())
            {
                Plugin.log.Error("OpenVR input is not available");
                poseOffset = Pose.identity;
                return false;
            }

            string devicePath = node switch
            {
                XRNode.LeftHand => OpenVR.k_pchPathUserHandLeft,
                XRNode.RightHand => OpenVR.k_pchPathUserHandRight,
                _ => throw new ArgumentException("Invalid XR node", nameof(node)),
            };

            ulong handle = 0;
            EVRInputError error = OpenVR.Input.GetInputSourceHandle(devicePath, ref handle);

            if (error != EVRInputError.None)
            {
                Plugin.log.Error($"Failed to get input source handle for '{devicePath}': {error}");
                poseOffset = Pose.identity;
                return false;
            }

            InputOriginInfo_t originInfo = default;
            error = OpenVR.Input.GetOriginTrackedDeviceInfo(handle, ref originInfo, kInputOriginInfoStructSize);

            if (error is not EVRInputError.None and not EVRInputError.NoData and not EVRInputError.InvalidHandle)
            {
                Plugin.log.Error($"Failed to get origin tracked device info for '{devicePath}' ({handle}): {error}");
                poseOffset = Pose.identity;
                return false;
            }

            string? renderModelName = GetStringTrackedDeviceProperty(originInfo.trackedDeviceIndex, ETrackedDeviceProperty.Prop_RenderModelName_String);

            if (renderModelName == null)
            {
                poseOffset = Pose.identity;
                return false;
            }

            VRControllerState_t controllerState = default;
            RenderModel_ControllerMode_State_t controllerModeState = default;
            RenderModel_ComponentState_t componentState = default;
            bool success = false;

            foreach (string name in kOffsetComponentNames)
            {
                if (success = OpenVR.RenderModels.GetComponentState(renderModelName, name, ref controllerState, ref controllerModeState, ref componentState))
                {
                    break;
                }
            }

            if (!success)
            {
                Plugin.log.Warn($"Controller at '{devicePath}' does not have a grip offset");
                poseOffset = Pose.identity;
                return false;
            }

            HmdMatrix34_t matrix = componentState.mTrackingToComponentLocal;
            Vector3 position = -matrix.GetPosition();
            Quaternion rotation = Quaternion.Inverse(matrix.GetRotation());
            poseOffset = new Pose(rotation * position, rotation);
            return true;
        }

        private static string? GetStringTrackedDeviceProperty(uint deviceIndex, ETrackedDeviceProperty property)
        {
            ETrackedPropertyError error = ETrackedPropertyError.TrackedProp_Success;
            uint length = OpenVR.System.GetStringTrackedDeviceProperty(deviceIndex, property, null, 0, ref error);

            if (error is not ETrackedPropertyError.TrackedProp_Success and not ETrackedPropertyError.TrackedProp_BufferTooSmall)
            {
                Plugin.log.Error($"Failed to get string property '{property}' length for device at index {deviceIndex}: {error}");
                return null;
            }

            if (length <= 0)
            {
                return null;
            }

            StringBuilder stringBuilder = new((int)length);
            OpenVR.System.GetStringTrackedDeviceProperty(deviceIndex, property, stringBuilder, length, ref error);

            if (error != ETrackedPropertyError.TrackedProp_Success)
            {
                Plugin.log.Error($"Failed to get property '{property}' for device at index {deviceIndex}: {error}");
                return null;
            }

            return stringBuilder.ToString();
        }
    }
}
