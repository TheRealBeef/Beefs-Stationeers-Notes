using System;
using System.Collections.Generic;
using System.IO;
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
        private static string _pendingSaveDirectory = null;
        private static string _pendingSaveFileName = null;
        private static FileInfo _fileToDelete = null;

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

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SaveHelper), "Save",
            new Type[] { typeof(DirectoryInfo), typeof(string), typeof(bool), typeof(System.Threading.CancellationToken) })]
        static void Save_Prefix(DirectoryInfo saveDirectory, string saveFileName)
        {
            _pendingSaveDirectory = saveDirectory?.FullName;
            _pendingSaveFileName = saveFileName;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SaveHelper), "Save",
            new Type[] { typeof(DirectoryInfo), typeof(string), typeof(bool), typeof(System.Threading.CancellationToken) })]
        static void Save_Postfix(ref UniTask<SaveResult> __result)
        {
            string savedFileName = _pendingSaveFileName;

            _pendingSaveDirectory = null;
            _pendingSaveFileName = null;

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
        static void RollSaveFiles_Prefix(DirectoryInfo directoryInfo, int maxCount)
        {
            _fileToDelete = null;

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
                    _fileToDelete = files[0].Item1;
                }
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Error in RollSaveFiles_Prefix: {ex.Message}");
                _fileToDelete = null;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SaveHelper), "RollSaveFiles")]
        static void RollSaveFiles_Postfix()
        {
            if (BeefsRecipesPlugin.Instance == null) return;
            if (_fileToDelete == null) return;

            try
            {
                string deletedFileName = Path.GetFileNameWithoutExtension(_fileToDelete.Name);
                BeefsRecipesPlugin.Instance.OnSaveDeleted(deletedFileName);
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Error in RollSaveFiles_Postfix: {ex.Message}");
            }
            finally
            {
                _fileToDelete = null;
            }
        }
    }
}