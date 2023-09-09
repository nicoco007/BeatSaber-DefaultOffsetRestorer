// <copyright file="VRControllerOffsetManager.cs" company="nicoco007">
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

using System.Diagnostics.CodeAnalysis;
using SiraUtil.Affinity;
using UnityEngine;
using UnityEngine.XR;

namespace DefaultOffsetRestorer
{
    internal class VRControllerOffsetManager : IAffinity
    {
        private readonly Settings _settings;

        private VRControllerOffsetManager(Settings settings)
        {
            _settings = settings;
        }

        // based on OpenVRHelper.AdjustControllerTransform from 1.29.1
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

        [AffinityPatch(typeof(VRController), nameof(VRController.TryGetControllerOffset))]
        [AffinityPrefix]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313", Justification = "Special patch naming")]
        private bool VRController_GetControllerOffset_Prefix(VRController __instance, ref bool __result, ref Pose poseOffset)
        {
            if (!_settings.enabled || __instance._vrPlatformHelper is not UnityXRHelper unityXRHelper)
            {
                return true;
            }

            UnityXRController controller = unityXRHelper.ControllerFromNode(__instance._node);

            if (controller == null)
            {
                return true;
            }

            if (!OpenVRUtilities.TryGetGripOffset(__instance._node, out poseOffset))
            {
                return true;
            }

            if (__instance._node == XRNode.LeftHand)
            {
                poseOffset = VRController.InvertControllerPose(poseOffset);
            }

            poseOffset = AdjustControllerPose(controller, poseOffset, __instance._transformOffset.positionOffset, __instance._transformOffset.rotationOffset);

            if (__instance._node == XRNode.LeftHand)
            {
                poseOffset = VRController.InvertControllerPose(poseOffset);
            }

            __result = true;
            return false;
        }
    }
}
