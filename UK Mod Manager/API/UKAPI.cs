﻿using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UMM.Loader;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Linq;
using UnityEngine.Events;
using System.Diagnostics;

namespace UMM
{
    public static class UKAPI
    {
        public static bool IsDevBuild { get { return UltraModManager.outdated; } }
        private static List<string> disableCybergrindReasons = new List<string>();

        /// <summary>
        /// Returns whether or not leaderboard submissions are allowed.
        /// </summary>
        public static bool CanSubmitCybergrindScore
        {
            get
            {
                return disableCybergrindReasons.Count == 0;
            }
        }

        /// <summary>
        /// Returns a clone of all found <see cref="ModInformation"/> instances.
        /// </summary>
        [Obsolete(Plugin.UKMOD_DEPRECATION_MESSAGE)]
        public static Dictionary<string, ModInformation> AllModInfoClone => UltraModManager.foundMods.ToDictionary(entry => entry.Key, entry => entry.Value);

        /// <summary>
        /// Returns a clone of all loaded <see cref="ModInformation"/> instances.
        /// </summary>
        [Obsolete(Plugin.UKMOD_DEPRECATION_MESSAGE)]
        public static Dictionary<string, ModInformation> AllLoadedModInfoClone => UltraModManager.allLoadedMods.ToDictionary(entry => entry.Key, entry => entry.Value);

        /// <summary>
        /// Initializes the API by loading the save file and common asset bundle
        /// </summary>
        internal static void Initialize()
        {
            Stopwatch watch = new Stopwatch();
            Plugin.logger.LogMessage("Beginning UKAPI");
            string[] launchArgs = Environment.GetCommandLineArgs();
            if (launchArgs != null)
            {
                foreach (string str in launchArgs)
                {
                    if (str.Contains("disable_mods"))
                    {
                        Plugin.logger.LogMessage("Not starting UMM due to launch arg disabling it.");
                        goto EndInitialization;
                    }
                }
            }
            SaveFileHandler.LoadData();
            UltraModManager.InitializeManager();
            if (launchArgs != null)
            {
                foreach (string str in launchArgs)
                {
                    if (str != null && (str.Contains("sandbox") || str.Contains("uk_construct")))
                    {
                        Plugin.logger.LogMessage("Launch argument detected: " + str + ", loading into the sandbox!");
                        SceneManager.LoadScene("uk_construct");
                    }
                    else
                    {
                        Plugin.logger.LogMessage("Launch argument detected: " + str + ", but is has no use with UKAPI!");
                    }
                }
            }
            EndInitialization:
            watch.Stop();
            Plugin.logger.LogMessage("UMM initialization completed in " + watch.ElapsedMilliseconds + "ms");
        }


        internal static void Update()
        {
            foreach (UKKeyBind bind in KeyBindHandler.moddedKeyBinds.Values.ToList())
                bind?.CheckEvents(); // Always null check :P
        }

        /// <summary>
        /// Disables CyberGrind score submission, CyberGrind submissions can only be re-enabled if nothing else disables it
        /// </summary>
        /// <param name="reason">Why CyberGrind is disabled, if you want to reenable it later you can do so by removing the reason</param>
        public static void DisableCyberGrindSubmission(string reason)
        {
            if (!disableCybergrindReasons.Contains(reason))
                disableCybergrindReasons.Add(reason);
        }

        /// <summary>
        /// Removes a Cybergrind disable reason if found, Cybergrind score submissions will only be enabled if there aren't any reasons to disable it
        /// </summary>
        /// <param name="reason">The reason to remove</param>
        public static void RemoveDisableCyberGrindReason(string reason)
        {
            if (disableCybergrindReasons.Contains(reason))
                disableCybergrindReasons.Remove(reason);
            else
                Plugin.logger.LogError("Tried to remove cg reason " + reason + " but could not find it!");
        }

        /// <summary>
        /// Enumerated version of the Ultrakill scene types
        /// </summary>
        public enum UKLevelType { Intro, MainMenu, Level, Endless, Sandbox, Credits, Custom, Intermission, Secret, PrimeSanctum, Unknown }

        /// <summary>
        /// Returns the current level type
        /// </summary>
        public static UKLevelType CurrentLevelType = UKLevelType.Intro;

        /// <summary>
        /// Returns the currently active ultrakill scene name.
        /// </summary>
        public static string CurrentSceneName { get; private set; } = "";


        /// <summary>
        /// Invoked whenever the current level type is changed.
        /// </summary>
        /// <param name="uKLevelType">The type of level that was loaded.</param>
        public delegate void OnLevelChangedHandler(UKLevelType uKLevelType);

        /// <summary>
        /// Invoked whenever the current level type is changed.
        /// </summary>
        public static OnLevelChangedHandler OnLevelTypeChanged;

        /// <summary>
        /// Invoked whenever the scene is changed.
        /// </summary>
        public static OnLevelChangedHandler OnLevelChanged;

        //Perhaps there is a better way to do this.
        private static void OnSceneLoad(Scene scene, LoadSceneMode loadSceneMode)
        {
            string sceneName = scene.name;

            if (scene != SceneManager.GetActiveScene())
                return;

            UKLevelType newScene = GetUKLevelType(sceneName);

            if (newScene != CurrentLevelType)
            {
                CurrentLevelType = newScene;
                CurrentSceneName = scene.name;
                OnLevelTypeChanged?.Invoke(newScene);
            }

            OnLevelChanged?.Invoke(CurrentLevelType);
        }

        //Perhaps there is a better way to do this. Also this will most definitely cause problems in the future if PITR or Hakita rename any scenes.

        /// <summary>
        /// Gets enumerated level type from the name of a scene.
        /// </summary>
        /// <param name="sceneName">Name of the scene</param>
        /// <returns></returns>
        public static UKLevelType GetUKLevelType(string sceneName)
        {
            sceneName = (sceneName.Contains("P-")) ? "Sanctum" : sceneName;
            sceneName = (sceneName.Contains("-S")) ? "Secret" : sceneName;
            sceneName = (sceneName.Contains("Level")) ? "Level" : sceneName;
            sceneName = (sceneName.Contains("Intermission")) ? "Intermission" : sceneName;

            switch (sceneName)
            {
                case "Main Menu":
                    return UKLevelType.MainMenu;
                case "Custom Content":
                    return UKLevelType.Custom;
                case "Intro":
                    return UKLevelType.Intro;
                case "Endless":
                    return UKLevelType.Endless;
                case "uk_construct":
                    return UKLevelType.Sandbox;
                case "Intermission":
                    return UKLevelType.Intermission;
                case "Level":
                    return UKLevelType.Level;
                case "Secret":
                    return UKLevelType.Secret;
                case "Sanctum":
                    return UKLevelType.PrimeSanctum;
                case "Credits":
                    return UKLevelType.Credits;
                default:
                    return UKLevelType.Unknown;
            }
        }

        /// <summary>
        /// Returns true if the current scene is playable.
        /// This will return false for all secret levels.
        /// </summary>
        /// <returns></returns>
        public static bool InLevel()
        {
            bool inNonPlayable = (CurrentLevelType == UKLevelType.MainMenu || CurrentLevelType == UKLevelType.Intro || CurrentLevelType == UKLevelType.Intermission || CurrentLevelType == UKLevelType.Secret || CurrentLevelType == UKLevelType.Unknown);
            return !inNonPlayable;
        }

        /// <summary>
        /// Gets a <see cref="UKKeyBind"/> given its name, if the keybind doesn't exist it will be created
        /// </summary>
        /// <param name="key">The name of the keybind</param>
        /// <param name="fallback">The default key of the keybind</param>
        /// <returns>An instance of a <see cref="UKKeyBind"/></returns>
        public static UKKeyBind GetKeyBind(string key, KeyCode fallback = KeyCode.None)
        {
            UKKeyBind bind = KeyBindHandler.GetKeyBind(key, fallback);
            if (!bind.enabled)
            {
                bind.enabled = true;
                KeyBindHandler.OnKeyBindEnabled.Invoke(bind);
            }
            return bind;
        }

        /// <summary>
        /// Ensures that a <see cref="UKKeyBind"/> exists given a key, if it doesn't exist it won't be created
        /// </summary>
        /// <param name="key">The name of the keybind</param>
        /// <returns>If the keybind exists</returns>
        public static bool EnsureKeyBindExists(string key)
        {
            return KeyBindHandler.moddedKeyBinds.ContainsKey(key);
        }

        /// <summary>
        /// Checks if a mod is loaded provided its GUID
        /// </summary>
        /// <param name="GUID">The GUID of the mod</param>
        /// <returns></returns>
        public static bool IsModLoaded(string GUID)
        {
            return UltraModManager.allLoadedMods.ContainsKey(GUID);
        }

        [Obsolete("Use AllModInfoClone instead.")]
        public static Dictionary<string, ModInformation> GetAllModInformation() => AllModInfoClone;

        [Obsolete("Use AllLoadedModInfoClone instead.")]
        public static Dictionary<string, ModInformation> GetAllLoadedModInformation() => AllLoadedModInfoClone;

        /// <summary>
        /// Restarts Ultrakill
        /// </summary>
        public static void Restart() // thanks https://gitlab.com/vtolvr-mods/ModLoader/-/blob/release/Launcher/Program.cs
        {
            Application.Quit();
            Plugin.logger.LogMessage("Restarting Ultrakill!");

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = @"steam://run/1229490",
                UseShellExecute = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Minimized
            };
            System.Diagnostics.Process.Start(psi);

            //Plugin.logger.LogMessage("Path is \"" + Environment.CurrentDirectory + "\\BepInEx\\plugins\\UMM\\UltrakillRestarter.exe\"");
            //string strCmdText;
            //strCmdText = "/K \"" + Environment.CurrentDirectory + "\\BepInEx\\plugins\\UMM\\Ultrakill Restarter.exe\""/* + System.Diagnostics.Process.GetCurrentProcess().Id.ToString() + "\""*/;
            ////strCmdText = "/K \"" + Environment.CurrentDirectory + "ULTRAKILL.exe\"";
            //System.Diagnostics.Process.Start("CMD.exe", strCmdText);

            //var psi = new System.Diagnostics.ProcessStartInfo
            //{
            //    FileName = Environment.CurrentDirectory + "\\BepInEx\\plugins\\UMM\\Ultrakill Restarter.exe",
            //    UseShellExecute = true,
            //    WindowStyle = System.Diagnostics.ProcessWindowStyle.Minimized,
            //    Arguments = System.Diagnostics.Process.GetCurrentProcess().Id.ToString()
            //};
            //System.Diagnostics.Process.Start(psi);
        }

        internal static class SaveFileHandler
        {
            private static Dictionary<string, Dictionary<string, string>> savedData = new Dictionary<string, Dictionary<string, string>>();
            private static FileInfo SaveFile = null;

            internal static void LoadData()
            {
            	OperatingSystem os = Environment.OSVersion;
            	PlatformID     pid = os.Platform;
                string path = Assembly.GetExecutingAssembly().Location;
                path = path.Substring(0, path.LastIndexOf(Path.DirectorySeparatorChar)) + Path.DirectorySeparatorChar + "persistent mod data.json";
                Plugin.logger.LogInfo("Trying to load persistent mod data.json from " + path);
                SaveFile = new FileInfo(path);
                if (SaveFile.Exists)
                {
                    using (StreamReader jFile = SaveFile.OpenText())
                    {
                        savedData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(jFile.ReadToEnd());
                        if (savedData == null)
                            savedData = new Dictionary<string, Dictionary<string, string>>();
                        jFile.Close();
                    }
                }
                else
                {
                    Plugin.logger.LogInfo("Couldn't find a save file, making one now");
                    SaveFile.Create();
                }
                KeyBindHandler.LoadKeyBinds();
                if (EnsureModData("UMM", "ModProfiles"))
                    UltraModManager.LoadModProfiles();
                else
                {
                    SetModData("UMM", "ModProfiles", "");
                    SetModData("UMM", "CurrentModProfile", "Default");
                }
            }

            internal static void DumpFile()
            {
                if (SaveFile == null)
                    return;
                Plugin.logger.LogInfo("Dumping keybinds");
                KeyBindHandler.DumpKeyBinds();
                Plugin.logger.LogInfo("Dumping mod profiles");
                UltraModManager.DumpModProfiles();
                Plugin.logger.LogInfo("Dumping mod persistent data file to " + SaveFile.FullName);
                File.WriteAllText(SaveFile.FullName, JsonConvert.SerializeObject(savedData));
            }

            /// <summary>
            /// Gets presistent mod data from a key and modname
            /// </summary>
            /// <param name="modName">The name of the mod to retrieve data from</param>
            /// <param name="key">The value you want</param>
            /// <returns>The mod data if found, otherwise null</returns>
            public static string RetrieveModData(string key, string modName)
            {
                if (savedData.ContainsKey(modName))
                {
                    if (savedData[modName].ContainsKey(key))
                        return savedData[modName][key];
                }
                return null;
            }

            /// <summary>
            /// Adds persistent mod data from a key and mod name
            /// </summary>
            /// <param name="modName">The name of the mod to add data to</param>
            /// <param name="key">The key for the data</param>
            /// <param name="value">The data you want as a string, note you can only add strings</param>
            public static void SetModData(string modName, string key, string value)
            {
                if (!savedData.ContainsKey(modName))
                {
                    Dictionary<string, string> newDict = new Dictionary<string, string>();
                    newDict.Add(key, value);
                    savedData.Add(modName, newDict);
                }
                else if (!savedData[modName].ContainsKey(key))
                    savedData[modName].Add(key, value);
                else
                    savedData[modName][key] = value;
            }

            /// <summary>
            /// Removes persistent mod data from a key and a mod name
            /// </summary>
            /// <param name="modName">The name of the mod to remove data from</param>
            /// <param name="key">The key for the data</param>
            public static void RemoveModData(string modName, string key)
            {
                if (savedData.ContainsKey(modName))
                {
                    if (savedData[modName].ContainsKey(key))
                        savedData[modName].Remove(key);
                }
            }

            /// <summary>
            /// Checks if persistent mod data exists from a key and a mod name
            /// </summary>
            /// <param name="modName">The name of the mod to remove data from</param>
            /// <param name="key">The key for the data</param>
            public static bool EnsureModData(string modName, string key)
            {
                return savedData.ContainsKey(modName) && savedData[modName].ContainsKey(key);
            }
        }

        internal static class KeyBindHandler
        {
            internal static Dictionary<string, UKKeyBind> moddedKeyBinds = new Dictionary<string, UKKeyBind>();
            internal static KeyBindEnabledEvent OnKeyBindEnabled = new KeyBindEnabledEvent();

            internal static void LoadKeyBinds() // Copilot basically wrote this code
            {
                if (SaveFileHandler.EnsureModData("UMM", "KeyBinds"))
                {
                    string[] keyBinds = SaveFileHandler.RetrieveModData("KeyBinds", "UMM").Split(';');
                    foreach (string keyBind in keyBinds)
                    {
                        string[] keyBindData = keyBind.Split(':');
                        if (keyBindData.Length == 2)
                        {
                            Plugin.logger.LogMessage("Loading keybind" + keyBindData[0] + " : " + keyBindData[1]);
                            UKKeyBind bind = new UKKeyBind(new InputAction(keyBindData[0], InputActionType.Value, null, null, null, null), keyBindData[0], (KeyCode)Enum.Parse(typeof(KeyCode), keyBindData[1]));
                            moddedKeyBinds.Add(keyBindData[0], bind);
                        }
                    }
                }
                else
                {
                    SaveFileHandler.SetModData("UMM", "KeyBinds", "");
                }
            }

            internal static UKKeyBind GetKeyBind(string key, KeyCode defaultBind)
            {
                if (moddedKeyBinds.ContainsKey(key))
                    return moddedKeyBinds[key];
                UKKeyBind bind = new UKKeyBind(new InputAction(key, InputActionType.Value, null, null, null, null), key, defaultBind);
                moddedKeyBinds.Add(key, bind);
                return bind;
            }

            internal static void DumpKeyBinds()
            {
                string keyBinds = "";
                foreach (KeyValuePair<string, UKKeyBind> keyBind in moddedKeyBinds)
                {
                    Plugin.logger.LogInfo("Adding keybind " + keyBind.Key + ":" + keyBind.Value.keyBind + ";");
                    keyBinds += keyBind.Key + ":" + keyBind.Value.keyBind + ";"; // This is fine because all keyBinds should only have one action
                }
                SaveFileHandler.SetModData("UMM", "KeyBinds", keyBinds);
            }

            internal static IEnumerator SetKeyBindRoutine(GameObject currentKey, string keyName) // I copy and pasted this function completely, credit to whoever wrote ControlsOptions.OnGUI
            {
                Color32 normalColor = new Color32(20, 20, 20, 255);
                yield return null;
                while (true) // This is bad practice, I don't care :P
                {
                    Event current = Event.current;
                    if (current == null)
                    {
                        yield return null;
                        continue;
                    }
                    KeyCode keyCode = KeyCode.None;
                    if (current.keyCode == KeyCode.Escape)
                    {
                        currentKey.GetComponent<Image>().color = normalColor;
                        currentKey = null;
                        OptionsManager.Instance.dontUnpause = false;
                    }
                    else if (current.isKey || current.isMouse || current.button > 2 || current.shift)
                    {
                        if (current.isKey)
                        {
                            keyCode = current.keyCode;
                        }
                        else if (Input.GetKey(KeyCode.LeftShift))
                        {
                            keyCode = KeyCode.LeftShift;
                        }
                        else if (Input.GetKey(KeyCode.RightShift))
                        {
                            keyCode = KeyCode.RightShift;
                        }
                        else
                        {
                            if (current.button > 6)
                            {
                                currentKey.GetComponent<Image>().color = normalColor;
                                OptionsManager.Instance.dontUnpause = false;
                                yield break;
                            }
                            keyCode = KeyCode.Mouse0 + current.button;
                        }
                    }
                    else if (Input.GetKey(KeyCode.Mouse3) || Input.GetKey(KeyCode.Mouse4) || Input.GetKey(KeyCode.Mouse5) || Input.GetKey(KeyCode.Mouse6))
                    {
                        if (Input.GetKey(KeyCode.Mouse4))
                        {
                            keyCode = KeyCode.Mouse4;
                        }
                        else if (Input.GetKey(KeyCode.Mouse5))
                        {
                            keyCode = KeyCode.Mouse5;
                        }
                        else if (Input.GetKey(KeyCode.Mouse6))
                        {
                            keyCode = KeyCode.Mouse6;
                        }
                    }   
                    else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    {
                        keyCode = KeyCode.LeftShift;
                        if (Input.GetKey(KeyCode.RightShift))
                        {
                            keyCode = KeyCode.RightShift;
                        }
                    }
                    else
                    {
                        yield return null;
                        continue;
                    }
                    if (keyCode == KeyCode.None)
                    {
                        yield return null;
                        continue;
                    }
                    //InputManager.Instance.Inputs[this.currentKey.name] = keyCode;
                    currentKey.GetComponentInChildren<Text>().text = ControlsOptions.GetKeyName(keyCode);
                    //MonoSingleton<PrefsManager>.Instance.SetInt("keyBinding." + this.currentKey.name, (int)keyCode);
                    //InputManager.Instance.UpdateBindings();
                    moddedKeyBinds[keyName].ChangeKeyBind(keyCode);
                    currentKey.GetComponent<Image>().color = normalColor;
                    OptionsManager.Instance.dontUnpause = false;
                    yield break;
                }
            }

            internal class KeyBindEnabledEvent : UnityEvent<UKKeyBind> { }
        }
    }
}
