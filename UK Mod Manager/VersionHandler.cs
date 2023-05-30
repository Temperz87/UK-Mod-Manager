﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace UMM.Loader
{
    public static class VersionHandler
    {
        public const string VERSION = "0.5.7"; // Should this be hardcoded? No it should not be
        public static  IEnumerator CheckVersion()
        {
            //Plugin.logger.LogInfo("Trying to get verison.");
            using (UnityWebRequest www = UnityWebRequest.Get("https://api.github.com/repos/Temperz87/ultra-mod-manager/tags"))
            {
                yield return www.SendWebRequest();
                if (www == null)
                {
                    Plugin.logger.LogError("WWW was null when checking version!");
                    yield break;
                }
                if (www.isNetworkError)
                {
                    Plugin.logger.LogError("Couldn't get the version from url: " + www.error);
                    yield break;
                }
                string text = www.downloadHandler.text;
                //Plugin.logger.LogMessage(text);
                JArray jObjects = JArray.Parse(text);
                string latestVersion = jObjects[0].Value<string>("name");
                if (latestVersion != VERSION)
                {
                    // Check current version string against the newer one
                    if (VERSION.CompareTo(latestVersion) < 0)
                    {
                        Plugin.logger.LogWarning("New UMM version found: " + latestVersion + " while the current version is: " + VERSION + ", consider upgrading to the new version.");
                        UltraModManager.outdated = true;
                    }
                    else
                    {
                        Plugin.logger.LogWarning("Latest available UMM build is: " + latestVersion + " while the current version is: " + VERSION + ", be warned that in a dev build mods, or UMM itself, can break.");
                        UltraModManager.devBuild = true;
                    }
                    UltraModManager.newLoaderVersion = latestVersion;
                }
                else
                    Plugin.logger.LogMessage("UMM is up to date!");
                yield break;
            }
        }
    }
}
