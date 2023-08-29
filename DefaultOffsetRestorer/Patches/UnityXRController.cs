// <copyright file="UnityXRController.cs" company="nicoco007">
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
using UnityEngine.XR;

namespace DefaultOffsetRestorer.Patches
{
    /// <summary>
    /// Simply logs for debugging purposes when trying to find the manufacturer of a given controller.
    /// </summary>
    [HarmonyPatch(typeof(UnityXRController), nameof(UnityXRController.TryToUpdateManufacturerName))]
    internal class UnityXRController_TryToUpdateManufacturerName
    {
        private static void Prefix(InputDevice device)
        {
            Plugin.log.Notice($"Got controller '{device.name}' from manufacturer '{device.manufacturer}'");
        }
    }
}
