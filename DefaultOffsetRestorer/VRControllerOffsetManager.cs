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

using System;
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
                rotation += OffsetConverter.kLegacyIndexControllerOffset.rotation;
                position += OffsetConverter.kLegacyIndexControllerOffset.position;
            }
            else
            {
                rotation += OffsetConverter.kLegacyOtherControllerOffset.rotation;
                position += OffsetConverter.kLegacyOtherControllerOffset.position;
            }

            // The original code does transform.Rotate(rotation) then transform.Translate(position)
            // so we have to rotate the position by both the pose offset and the rotation.
            return new Pose(poseOffset.position + (poseOffset.rotation * Quaternion.Euler(rotation) * position), poseOffset.rotation * Quaternion.Euler(rotation));
        }

        [AffinityPatch(
            typeof(VRController),
            nameof(VRController.TryGetControllerOffset),
            AffinityMethodType.Normal,
            new AffinityArgumentType[] { AffinityArgumentType.Normal, AffinityArgumentType.Normal, AffinityArgumentType.Normal, AffinityArgumentType.Out },
            new Type[] { typeof(IVRPlatformHelper), typeof(VRControllerTransformOffset), typeof(XRNode), typeof(Pose) })]
        [AffinityPrefix]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313", Justification = "Special patch naming")]
        private bool VRController_GetControllerOffset_Prefix(ref bool __result, IVRPlatformHelper vrPlatformHelper, VRControllerTransformOffset transformOffset, XRNode node, ref Pose poseOffset)
        {
            if (!_settings.enabled || vrPlatformHelper is not UnityXRHelper unityXRHelper)
            {
                return true;
            }

            UnityXRController controller = unityXRHelper.ControllerFromNode(node);

            if (controller == null)
            {
                return true;
            }

            if (!OpenVRUtilities.TryGetGripOffset(node, out poseOffset))
            {
                return true;
            }

            if (node == XRNode.LeftHand)
            {
                poseOffset = VRController.InvertControllerPose(poseOffset);
            }

            poseOffset = AdjustControllerPose(controller, poseOffset, transformOffset.positionOffset, transformOffset.rotationOffset);

            if (node == XRNode.LeftHand)
            {
                poseOffset = VRController.InvertControllerPose(poseOffset);
            }

            __result = true;
            return false;
        }
    }
}
