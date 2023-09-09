// <copyright file="OpenVRUtilities.cs" company="nicoco007">
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
using UnityEngine;
using UnityEngine.XR;
using Valve.VR;

namespace DefaultOffsetRestorer
{
    internal class OpenVRUtilities
    {
        private static readonly uint kInputOriginInfoStructSize = (uint)Marshal.SizeOf(typeof(InputOriginInfo_t));
        private static readonly string[] kOffsetComponentNames = new[] { "openxr_grip", "grip" };

        internal static bool TryGetGripOffset(XRNode node, out Pose poseOffset)
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

            if (error is not EVRInputError.None)
            {
                if (error is not EVRInputError.NoData and not EVRInputError.InvalidHandle)
                {
                    Plugin.log.Error($"Failed to get origin tracked device info for '{devicePath}' ({handle}): {error}");
                }

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

        internal static string? GetStringTrackedDeviceProperty(uint deviceIndex, ETrackedDeviceProperty property)
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
