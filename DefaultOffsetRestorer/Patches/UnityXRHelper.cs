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
using Valve.VR;

namespace DefaultOffsetRestorer.Patches
{
    /// <summary>
    /// Initializes OpenVR when <see cref="UnityXRHelper"/> starts.
    /// </summary>
    [HarmonyPatch(typeof(UnityXRHelper), nameof(UnityXRHelper.Start))]
    internal static class UnityXRHelper_Start
    {
        private static void Postfix()
        {
            EVRInitError error = EVRInitError.None;
            OpenVR.Init(ref error, EVRApplicationType.VRApplication_Overlay);

            if (error != EVRInitError.None)
            {
                Plugin.log.Error("Failed to start OpenVR in Overlay mode: " + error);
            }
        }
    }
}
