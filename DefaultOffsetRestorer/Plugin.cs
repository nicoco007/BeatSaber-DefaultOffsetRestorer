﻿// <copyright file="Plugin.cs" company="nicoco007">
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
using HarmonyLib;
using IPA;
using IPA.Logging;
using Valve.VR;

namespace DefaultOffsetRestorer;

[Plugin(RuntimeOptions.DynamicInit)]
public class Plugin
{
    private readonly Harmony _harmony = new("com.nicoco007.beat-saber.default-offset-restorer");

    [Init]
    public Plugin(Logger logger)
    {
        log = logger;
    }

    internal static Logger log { get; private set; } = null!;

    [OnEnable]
    public void OnEnable()
    {
        try
        {
            if (!OpenVR.IsRuntimeInstalled())
            {
                log.Error("OpenVR runtime (SteamVR) is not installed");
                return;
            }
        }
        catch (DllNotFoundException)
        {
            log.Error("openvr_api.dll not found");
            return;
        }

        EVRInitError error = EVRInitError.None;
        OpenVR.Init(ref error, EVRApplicationType.VRApplication_Overlay);

        if (error != EVRInitError.None)
        {
            log.Error("Failed to start OpenVR in Overlay mode");
            return;
        }

        _harmony.PatchAll();
    }

    [OnDisable]
    public void OnDisable()
    {
        _harmony.UnpatchSelf();
    }
}
