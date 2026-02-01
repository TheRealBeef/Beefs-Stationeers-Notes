using System;
using Assets.Scripts.Objects;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace BeefsRecipes
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class BeefsRecipesPlugin : BaseUnityPlugin
    {
        public static BeefsRecipesPlugin Instance;
        public static ManualLogSource Log;

        [Serializable]
        public class RecipeSection
        {
            public string id;
            public string title;
            public string content;
            public string titleColorHex;
            public string contentColorHex;
            public bool isCollapsed;
        }

        private RecipesUIManager _uiManager;
        private RecipesPanelManager _panelManager;
        private RecipesContentManager _contentManager;

        private string _currentWorldName = "";
        private string _currentWorldId = "";
        private string _currentlyLoadedSaveFile = "";
        private bool _wasInWorld = false;
        public bool isWorldLoaded = false;

        public static ConfigEntry<float> UIScaleMultiplier;
        public static ConfigEntry<float> EdgeBarWidthMultiplier;
        public static ConfigEntry<float> EdgeBarHeightMultiplier;
        public static ConfigEntry<float> HoverZoneWidth;

        public bool IsEditing => _panelManager?.IsEditing ?? false;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            UIScaleMultiplier = Config.Bind("UI", "UIScaleMultiplier", 1.0f,
                new ConfigDescription("Global multiplier for all UI elements (fonts, buttons, panel widths)",
                    new AcceptableValueRange<float>(0.5f, 3.0f)));

            EdgeBarWidthMultiplier = Config.Bind("UI", "EdgeBarWidthMultiplier", 1.0f,
                new ConfigDescription("Additional multiplier for edge bar width only (makes it easier to click)",
                    new AcceptableValueRange<float>(1.0f, 5.0f)));

            EdgeBarHeightMultiplier = Config.Bind("UI", "EdgeBarHeightMultiplier", 1.0f,
                new ConfigDescription("Multiplier for edge bar height (makes it easier to click)",
                    new AcceptableValueRange<float>(0.5f, 5.0f)));

            HoverZoneWidth = Config.Bind("UI", "HoverZoneWidth", 20f,
                new ConfigDescription("Width in pixels of the hover detection zone on the right edge of the screen",
                    new AcceptableValueRange<float>(10f, 100f)));
            ApplyPatches();
        }

        private void Start()
        {
            InitializeManagers();
        }

        private void Update()
        {
            WorldTransition();

            if (!isWorldLoaded || !IsInGameWorld())
            {
                if (_uiManager.Canvas != null)
                    _uiManager.Canvas.enabled = false;
                return;
            }

            if (_uiManager.Canvas != null)
                _uiManager.Canvas.enabled = true;

            _uiManager.UpdateSizes();
            _panelManager.Update();
            _contentManager.Update();
        }

        private void OnDestroy()
        {
            _panelManager?.Cleanup();
        }

        private void ApplyPatches()
        {
            try
            {
                var harmony = new Harmony(PluginInfo.PLUGIN_GUID);
                harmony.PatchAll();
                Log.LogInfo("Harmony patches applied successfully");
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to apply Harmony patches: {ex.Message}");
            }
        }

        private void InitializeManagers()
        {
            _uiManager = new RecipesUIManager();
            _uiManager.CreateUI();

            _panelManager = new RecipesPanelManager(_uiManager);
            _contentManager = new RecipesContentManager(_uiManager, _panelManager);

            _panelManager.SetContentManager(_contentManager);
        }

        private void WorldTransition()
        {
            bool inWorld = IsInGameWorld();

            if (!inWorld && _wasInWorld)
            {
                OnWorldUnloaded();
            }

            _wasInWorld = inWorld;
        }

        private bool IsInGameWorld()
        {
            try
            {
                Light worldSun = WorldManager.Instance?.WorldSun?.TargetLight;
                return worldSun != null;
            }
            catch
            {
                return false;
            }
        }

        private void OnWorldUnloaded()
        {
            _currentWorldName = "";
            _currentWorldId = "";
            _currentlyLoadedSaveFile = "";
            isWorldLoaded = false;

            _contentManager.ClearNotes();
            _panelManager.ResetToHidden();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void OnSaveLoaded(string worldName, string saveFileName)
        {
            _currentWorldName = worldName;
            _currentWorldId = World.CurrentId;
            _currentlyLoadedSaveFile = saveFileName;
            isWorldLoaded = true;

            _contentManager.LoadNotes(worldName, saveFileName);
        }

        public void OnSave(string saveFileName)
        {
            if (!isWorldLoaded) return;
            _contentManager.SaveNotes(_currentWorldName, saveFileName);
        }

        public void OnSaveAs(string worldName)
        {
            if (!isWorldLoaded) return;

            string saveFileName = worldName;
            _contentManager.SaveNotes(worldName, saveFileName);
        }

        public void OnSaveDeleted(string deletedFileName)
        {
            BeefsRecipesSaveManager.DeleteNotes(_currentWorldName, deletedFileName);
        }
    }
}