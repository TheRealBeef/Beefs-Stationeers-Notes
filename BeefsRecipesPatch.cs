using System;
using System.Collections.Generic;
using System.IO;
using Assets.Scripts;
using Assets.Scripts.Serialization;
using HarmonyLib;
using Assets.Scripts.UI;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace BeefsRecipes
{
    [HarmonyPatch]
    public static class BeefsRecipesPatch
    {
        private static string _loadedSaveFilePath = null;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InputWindowBase), "get_IsInputWindow")]
        static void IsInputWindow_Postfix(ref bool __result)
        {
            if (BeefsRecipesPlugin.Instance != null && BeefsRecipesPlugin.Instance.IsEditing)
            {
                __result = true;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(KeyManager), nameof(KeyManager.GetButtonDown))]
        static bool GetButtonDown_Prefix(KeyCode key, ref bool __result)
        {
            if (BeefsRecipesPlugin.Instance == null || !BeefsRecipesPlugin.Instance.IsEditing)
                return true;

            if (key == KeyCode.Return || key == KeyCode.KeypadEnter || key == KeyCode.Tab || key == KeyCode.Escape)
            {
                __result = false;
                return false;
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(KeyManager), nameof(KeyManager.GetButtonUp))]
        static bool GetButtonUp_Prefix(KeyCode key, ref bool __result)
        {
            if (BeefsRecipesPlugin.Instance == null || !BeefsRecipesPlugin.Instance.IsEditing)
                return true;

            if (key == KeyCode.Return || key == KeyCode.KeypadEnter || key == KeyCode.Tab)
            {
                __result = false;
                return false;
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(KeyManager), nameof(KeyManager.GetButton))]
        static bool GetButton_Prefix(KeyCode key, ref bool __result)
        {
            if (BeefsRecipesPlugin.Instance == null || !BeefsRecipesPlugin.Instance.IsEditing)
                return true;

            if (key == KeyCode.Return || key == KeyCode.KeypadEnter || key == KeyCode.Tab)
            {
                __result = false;
                return false;
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LoadHelper), "LoadGameTask")]
        static void LoadGameTask_Prefix(string path, string stationName)
        {
            _loadedSaveFilePath = path;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(XmlSaveLoad), nameof(XmlSaveLoad.LoadWorld))]
        static void LoadWorld_Postfix()
        {
            if (BeefsRecipesPlugin.Instance == null) return;

            try
            {
                var instance = XmlSaveLoad.Instance;
                if (instance?.CurrentWorldSave == null)
                {
                    return;
                }

                string worldName = instance.CurrentStationName;

                string saveFileName = worldName;
                if (!string.IsNullOrEmpty(_loadedSaveFilePath))
                {
                    saveFileName = Path.GetFileNameWithoutExtension(_loadedSaveFilePath);
                }

                _loadedSaveFilePath = null;

                BeefsRecipesPlugin.Instance.OnSaveLoaded(worldName, saveFileName);
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Error in LoadWorld_Postfix: {ex.Message}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
        static void StartGame_Postfix()
        {
            if (BeefsRecipesPlugin.Instance == null) return;
            if (BeefsRecipesPlugin.Instance.isWorldLoaded) return;

            try
            {
                string worldName = XmlSaveLoad.Instance?.CurrentStationName ?? "";
                if (string.IsNullOrEmpty(worldName))
                {
                    worldName = WorldManager.CurrentWorldName;
                }

                BeefsRecipesPlugin.Log.LogInfo(
                    $"StartGame_Postfix - triggering OnSaveLoaded({worldName})");
                BeefsRecipesPlugin.Instance.OnSaveLoaded(worldName, "");
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Error in StartGame_Postfix: {ex.Message}");
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SaveHelper), "Save",
            new Type[] { typeof(DirectoryInfo), typeof(string), typeof(bool), typeof(System.Threading.CancellationToken) })]
        static void Save_Prefix(DirectoryInfo saveDirectory, string saveFileName, out string __state)
        {
            __state = saveFileName;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SaveHelper), "Save",
            new Type[] { typeof(DirectoryInfo), typeof(string), typeof(bool), typeof(System.Threading.CancellationToken) })]
        static void Save_Postfix(ref UniTask<SaveResult> __result, string __state)
        {
            string savedFileName = __state;

            if (string.IsNullOrEmpty(savedFileName))
            {
                return;
            }

            var originalTask = __result;
            __result = SaveNotesAfterGameSave(originalTask, savedFileName);
        }

        private static async UniTask<SaveResult> SaveNotesAfterGameSave(
            UniTask<SaveResult> originalTask,
            string saveFileName)
        {
            SaveResult result = await originalTask;

            if (result.Success && BeefsRecipesPlugin.Instance != null && BeefsRecipesPlugin.Instance.isWorldLoaded)
            {
                try
                {
                    string saveFileNameWithoutExt = Path.GetFileNameWithoutExtension(saveFileName);
                    BeefsRecipesPlugin.Instance.OnSave(saveFileNameWithoutExt);
                }
                catch (Exception ex)
                {
                    BeefsRecipesPlugin.Log.LogError($"Error saving notes after game save: {ex.Message}");
                }
            }

            return result;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SaveHelper), "DoSaveAs")]
        static void DoSaveAs_Postfix(string stationName, ref UniTask<SaveResult> __result)
        {
            if (BeefsRecipesPlugin.Instance == null) return;
            if (!BeefsRecipesPlugin.Instance.isWorldLoaded)
            {
                return;
            }

            var originalTask = __result;
            __result = CopyNotesToHeadAfterSaveAs(originalTask, stationName);
        }

        private static async UniTask<SaveResult> CopyNotesToHeadAfterSaveAs(
            UniTask<SaveResult> originalTask,
            string stationName)
        {
            SaveResult result = await originalTask;

            if (result.Success && BeefsRecipesPlugin.Instance != null && BeefsRecipesPlugin.Instance.isWorldLoaded)
            {
                try
                {
                    BeefsRecipesPlugin.Instance.OnSaveAs(stationName);
                }
                catch (Exception ex)
                {
                    BeefsRecipesPlugin.Log.LogError($"Error copying notes in DoSaveAs_Postfix: {ex.Message}");
                }
            }

            return result;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SaveHelper), "RollSaveFiles")]
        static void RollSaveFiles_Prefix(DirectoryInfo directoryInfo, int maxCount, out FileInfo __state)
        {
            __state = null;

            try
            {
                if (directoryInfo.GetFiles().Length <= maxCount)
                {
                    return;
                }

                var files = new List<ValueTuple<FileInfo, DateTime>>();

                foreach (FileInfo fileInfo in directoryInfo.GetFiles())
                {
                    if (fileInfo.Extension != SaveLoadConstants.SaveFileExtension)
                        continue;

                    if (fileInfo.Name.Length <= SaveLoadConstants.DateTimeFormat.Length)
                        continue;

                    DateTime dateTime;
                    if (DateTime.TryParseExact(
                            fileInfo.Name.Substring(0, SaveLoadConstants.DateTimeFormat.Length),
                            SaveLoadConstants.DateTimeFormat,
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AssumeLocal,
                            out dateTime))
                    {
                        files.Add(new ValueTuple<FileInfo, DateTime>(fileInfo, dateTime));
                    }
                }

                if (files.Count > 0)
                {
                    files.Sort((a, b) => a.Item2.CompareTo(b.Item2));
                    __state = files[0].Item1;
                }
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Error in RollSaveFiles_Prefix: {ex.Message}");
                __state = null;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SaveHelper), "RollSaveFiles")]
        static void RollSaveFiles_Postfix(FileInfo __state)
        {
            if (BeefsRecipesPlugin.Instance == null) return;
            if (__state == null) return;

            try
            {
                string deletedFileName = Path.GetFileNameWithoutExtension(__state.Name);
                BeefsRecipesPlugin.Instance.OnSaveDeleted(deletedFileName);
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Error in RollSaveFiles_Postfix: {ex.Message}");
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Assets.Scripts.NetworkServer), nameof(Assets.Scripts.NetworkServer.ClientDisconnected))]
        static void ClientDisconnected_Prefix(long connectionId, out ulong __state)
        {
            __state = 0;

            try
            {
                Client client = Client.Find(connectionId);
                if (client != null)
                {
                    __state = client.ClientId;
                }
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Error in ClientDisconnected_Prefix: {ex.Message}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Assets.Scripts.NetworkServer), nameof(Assets.Scripts.NetworkServer.ClientDisconnected))]
        static void ClientDisconnected_Postfix(ulong __state)
        {
            if (__state == 0) return;
            if (BeefsRecipesPlugin.Instance?.ServerNoteManager == null) return;

            try
            {
                BeefsRecipesPlugin.Instance.ServerNoteManager
                    .OnClientDisconnected(__state);
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Error in ClientDisconnected_Postfix: {ex.Message}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Assets.Scripts.Objects.Entities.Human), "OnSuitOccupantChanged")]
        static void OnSuitOccupantChanged_Postfix(Assets.Scripts.Objects.Entities.Human __instance)
        {
            if (__instance == null) return;
            if (BeefsRecipesPlugin.Instance?.ContentManager == null) return;

            try
            {
                if (__instance == Assets.Scripts.Objects.Entities.Human.LocalHuman)
                {
                    BeefsRecipesPlugin.Instance.ContentManager.RefreshAccentColors();
                    BeefsRecipesPlugin.Instance.ClientSyncManager?.AnnounceColor();
                }
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Error in OnSuitOccupantChanged_Postfix: {ex.Message}");
            }
        }
    }
}