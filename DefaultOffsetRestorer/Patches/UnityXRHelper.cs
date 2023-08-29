// <copyright file="UnityXRHelper.cs" company="nicoco007">
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

using HarmonyLib;
using UnityEngine;

namespace DefaultOffsetRestorer.Patches
{
    /// <summary>
    /// Sets <see cref="UnityXRHelper._htcVivePoseOffset"/>, <see cref="UnityXRHelper._oculusPoseOffset"/>, and <see cref="UnityXRHelper._valveIndexPoseOffset"/>
    /// so the offsets result in the "raw" SteamVR position rather than the "grip" position.
    /// </summary>
    [HarmonyPatch(typeof(UnityXRHelper), nameof(UnityXRHelper.Start))]
    internal static class UnityXRHelper_Start
    {
        // SteamVR\resources\rendermodels\vr_controller_vive_1_5\vr_controller_vive_1_5.json (grip)
        private static readonly Pose kHtcViveGripOffset = SteamVRPose(new Vector3(0.0f, -0.015f, 0.097f), new Vector3(5.037f, 0.0f, 0.0f));

        // SteamVR\resources\rendermodels\oculus_quest2_controller_right\oculus_quest2_controller_right.json (openxr_grip)
        private static readonly Pose kOculusGripOffset = SteamVRPose(new Vector3(-0.007f, -0.00182941f, 0.1019482f), new Vector3(20.6f, 0.0f, 0.0f));

        // SteamVR\drivers\indexcontroller\resources\rendermodels\valve_controller_knu_1_0_right\valve_controller_knu_1_0_right.json (grip)
        private static readonly Pose kKnucklesGripOffset = SteamVRPose(new Vector3(0.0f, -0.015f, 0.13f), new Vector3(15.392f, 2.071f, -0.303f));

        private static void Prefix(UnityXRHelper __instance)
        {
            __instance._htcVivePoseOffset = InvertOffset(kHtcViveGripOffset);
            __instance._oculusPoseOffset = InvertOffset(kOculusGripOffset);
            __instance._valveIndexPoseOffset = InvertOffset(kKnucklesGripOffset);
        }

        private static EulerPose InvertOffset(Pose gripOffset)
        {
            Vector3 localPosition = Quaternion.Inverse(gripOffset.rotation) * -gripOffset.position;
            Quaternion localRotation = Quaternion.Inverse(gripOffset.rotation);
            return new EulerPose(localPosition, localRotation.eulerAngles);
        }

        private static Pose SteamVRPose(Vector3 origin, Vector3 rotateXYZ)
        {
            // The pose needs to be mirrored across the XY plane.
            // SteamVR applies euler angles in a different order so we need to do it manually.
            Quaternion qX = Quaternion.AngleAxis(-rotateXYZ.x, Vector3.right);
            Quaternion qY = Quaternion.AngleAxis(-rotateXYZ.y, Vector3.up);
            Quaternion qZ = Quaternion.AngleAxis(rotateXYZ.z, Vector3.forward);

            return new Pose(new Vector3(origin.x, origin.y, -origin.z), qZ * qY * qX);
        }
    }
}
