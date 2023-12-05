// <copyright file="OffsetConverter.cs" company="nicoco007">
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
using UnityEngine;
using UnityEngine.XR;

namespace DefaultOffsetRestorer
{
    internal static class OffsetConverter
    {
        // numbers are from OpenVRHelper.AdjustControllerTransform in 1.29.1
        internal static readonly EulerPose kLegacyIndexControllerOffset = new(new Vector3(0f, 0.022f, -0.01f), new Vector3(-16.3f, 0f, 0f));
        internal static readonly EulerPose kLegacyOtherControllerOffset = new(new Vector3(0f, -0.008f, 0f), new Vector3(-4.3f, 0f, 0f));

        internal static (Vector3 position, Vector3 rotation) ConvertFromLegacy(UnityXRHelper unityXRHelper, Pose gripOffset, Vector3 position, Vector3 rotation)
        {
            // controller offsets are based on the right hand
            UnityXRController controller = unityXRHelper.ControllerFromNode(XRNode.RightHand) ?? throw new InvalidOperationException("Missing controller");
            Pose controllerManufacturerOffset = unityXRHelper.GetPoseOffsetForManufacturer(controller.manufacturerName);

            if (controller.manufacturerName == UnityXRHelper.VRControllerManufacturerName.Valve)
            {
                rotation += kLegacyIndexControllerOffset.rotation;
                position += kLegacyIndexControllerOffset.position;
            }
            else
            {
                rotation += kLegacyOtherControllerOffset.rotation;
                position += kLegacyOtherControllerOffset.position;
            }

            Pose oldLocalOffset = new(gripOffset.position + (gripOffset.rotation * Quaternion.Euler(rotation) * position), gripOffset.rotation * Quaternion.Euler(rotation));
            Pose newLocalOffset = GetInverseTransformedBy(oldLocalOffset, new Pose(controllerManufacturerOffset.position, controllerManufacturerOffset.rotation));

            return (newLocalOffset.position, newLocalOffset.rotation.eulerAngles);
        }

        internal static (Vector3 position, Vector3 rotation) ConvertToLegacy(UnityXRHelper unityXRHelper, Pose gripOffset, Vector3 position, Vector3 rotation)
        {
            // controller offsets are based on the right hand
            UnityXRController controller = unityXRHelper.ControllerFromNode(XRNode.RightHand) ?? throw new InvalidOperationException("Missing controller");
            Pose controllerManufacturerOffset = unityXRHelper.GetPoseOffsetForManufacturer(controller.manufacturerName);

            Pose localOffset = new(position, Quaternion.Euler(rotation));
            localOffset = localOffset.GetTransformedBy(new Pose(controllerManufacturerOffset.position, controllerManufacturerOffset.rotation));

            Vector3 rotationLegacy = (localOffset.rotation * Quaternion.Inverse(gripOffset.rotation)).eulerAngles;
            Vector3 positionLegacy = Quaternion.Inverse(localOffset.rotation) * (localOffset.position - gripOffset.position);

            if (controller.manufacturerName == UnityXRHelper.VRControllerManufacturerName.Valve)
            {
                rotationLegacy -= kLegacyIndexControllerOffset.rotation;
                positionLegacy -= kLegacyIndexControllerOffset.position;
            }
            else
            {
                rotationLegacy -= kLegacyOtherControllerOffset.rotation;
                positionLegacy -= kLegacyOtherControllerOffset.position;
            }

            return (positionLegacy, rotationLegacy);
        }

        // this is the inverse of Pose.GetTransformedBy(Pose)
        private static Pose GetInverseTransformedBy(Pose self, Pose lhs)
        {
            Quaternion inverse = Quaternion.Inverse(lhs.rotation);
            return new Pose(
                inverse * (self.position - lhs.position),
                inverse * self.rotation);
        }

        internal struct EulerPose
        {
            internal EulerPose(Vector3 position, Vector3 rotation)
            {
                this.position = position;
                this.rotation = rotation;
            }

            internal Vector3 position { get; }

            internal Vector3 rotation { get; }
        }
    }
}
