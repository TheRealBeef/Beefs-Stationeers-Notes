using System;
using System.Collections.Generic;
using Assets.Scripts.Objects;
using Assets.Scripts.Serialization;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

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
            public bool isPublic;
            public ulong ownerId;
            public string ownerDisplayName;
            public string sharedTimestamp;
            public bool isDrawing;
            public string drawingPngBase64;
            public int drawingHeight;
            public bool drawingShowBg;
        }

        [Serializable]
        public class Stroke
        {
            public List<float> points;
            public string colorHex;
            public float brushSize;
            public bool isEraser;
        }

        public static class RuntimeContext
        {
            public enum Mode
            {
                Unknown,
                Singleplayer,
                NonDedicatedHost,
                DedicatedServer,
                Client
            }

            private static Mode _current = Mode.Unknown;
            public static Mode Current => _current;

            private static bool? _isHeadless;
            public static bool IsHeadlessMode()
            {
                _isHeadless ??= SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
                return _isHeadless.Value;
            }

            public static void Detect()
            {
                Mode previous = _current;

                if (IsHeadlessMode())
                {
                    _current = Mode.DedicatedServer;
                }
                else if (Assets.Scripts.Networking.NetworkManager.IsServer
                         || Assets.Scripts.NetworkServer.IsHosting)
                {
                    _current = Mode.NonDedicatedHost;
                }
                else if (Assets.Scripts.Networking.NetworkManager.IsClient)
                {
                    _current = Mode.Client;
                }
                else
                {
                    _current = Mode.Singleplayer;
                }

                if (_current != previous)
                {
                    Log.LogInfo($"Runtime context: {previous} → {_current}");
                }
            }

            public static bool MayNeedRedetection()
            {
                if (_current != Mode.Singleplayer) return false;
                if (IsHeadlessMode()) return false;
                try
                {
                    return Assets.Scripts.Serialization.Settings.CurrentData.StartLocalHost;
                }
                catch
                {
                    return false;
                }
            }

            public static void Reset()
            {
                _current = Mode.Unknown;
            }

            public static bool HasUI => _current != Mode.DedicatedServer && _current != Mode.Unknown;
            public static bool HasServer => _current == Mode.DedicatedServer || _current == Mode.NonDedicatedHost;
            public static bool HasClient => _current == Mode.Client || _current == Mode.NonDedicatedHost;
            public static bool IsMultiplayer => _current != Mode.Singleplayer && _current != Mode.Unknown;
            public static bool IsHostAdmin => _current == Mode.NonDedicatedHost;
        }

        private RecipesUIManager _uiManager;
        private RecipesPanelManager _panelManager;
        private RecipesContentManager _contentManager;
        public RecipesContentManager ContentManager => _contentManager;
        public Canvas UICanvas => _uiManager?.Canvas;
        public ServerNoteManager ServerNoteManager { get; private set; }
        public ClientSyncManager ClientSyncManager { get; private set; }

        private string _currentWorldName = "";
        private string _currentWorldId = "";
        private string _currentlyLoadedSaveFile = "";
        private bool _wasInWorld = false;
        public bool isWorldLoaded = false;
        private float _lastPersonalSaveTime = 0f;
        private const float PersonalSaveInterval = 300f;
        public static ConfigEntry<float> UIScaleMultiplier;
        public static ConfigEntry<float> EdgeBarWidthMultiplier;
        public static ConfigEntry<float> EdgeBarHeightMultiplier;
        public static ConfigEntry<float> HoverZoneWidth;
        public static ConfigEntry<float> DragBarWidth;
        public static ConfigEntry<int> VotesRequiredToRemove;
        public static ConfigEntry<int> StrikesBeforeKick;
        public static ConfigEntry<int> StrikesBeforeBan;
        public static ConfigEntry<bool> BanEnforcement;

        public bool IsEditing => _panelManager?.IsEditing ?? false;

        public bool IsPanelInteractive
        {
            get
            {
                if (_panelManager == null) return false;
                var state = _panelManager.CurrentState;
                return state == RecipesPanelManager.PanelState.PeekLocked
                    || state == RecipesPanelManager.PanelState.Expanded
                    || state == RecipesPanelManager.PanelState.Fullscreen;
            }
        }

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

            DragBarWidth = Config.Bind("UI", "DragBarWidth", 5f,
                new ConfigDescription("Width in pixels of the section drag/reorder bars",
                    new AcceptableValueRange<float>(2f, 20f)));

            VotesRequiredToRemove = Config.Bind("Moderation", "VotesRequiredToRemove", 2,
                new ConfigDescription("Number of unique votes required to remove a public section",
                    new AcceptableValueRange<int>(1, 10)));

            StrikesBeforeKick = Config.Bind("Moderation", "StrikesBeforeKick", 3,
                new ConfigDescription("Auto-kick player after this many vote-removed sections (0 = disabled)",
                    new AcceptableValueRange<int>(0, 20)));

            StrikesBeforeBan = Config.Bind("Moderation", "StrikesBeforeBan", 5,
                new ConfigDescription("Auto-ban player after this many vote-removed sections (0 = disabled)",
                    new AcceptableValueRange<int>(0, 50)));

            BanEnforcement = Config.Bind("Moderation", "BanEnforcement", true,
                "Check blacklist on player connect and disconnect banned players");

            ApplyPatches();
            RecipesNetworkMessages.Initialize();
        }

        private void Start()
        {
            if (!RuntimeContext.IsHeadlessMode())
            {
                InitializeManagers();
            }
            else
            {
                Log.LogInfo("Headless mode detected - skipping UI initialization");
            }

            Assets.Scripts.NetworkClient.ClientFinishedJoining += OnClientFinishedJoining;
        }

        private void Update()
        {
            WorldTransition();

            if (!isWorldLoaded || !IsInGameWorld())
            {
                if (_uiManager?.Canvas != null)
                    _uiManager.Canvas.enabled = false;
                return;
            }

            if (RuntimeContext.MayNeedRedetection())
            {
                var previousContext = RuntimeContext.Current;
                RuntimeContext.Detect();

                if (RuntimeContext.Current != previousContext)
                {
                    OnContextChanged();
                }
            }

            if (_uiManager == null) return;

            if (_uiManager.Canvas != null)
                _uiManager.Canvas.enabled = true;

            _uiManager.UpdateSizes();
            _panelManager.Update();
            _contentManager.Update();

            if (_contentManager?.HasSessionKey == true)
            {
                if (Time.unscaledTime - _lastPersonalSaveTime > PersonalSaveInterval)
                {
                    _lastPersonalSaveTime = Time.unscaledTime;
                    _contentManager.SavePersonalNotes();
                }
            }

            if (Time.frameCount % 900 == 0)
            {
                RecipesNetworkMessages.CleanupStaleTransfers();
            }
        }

        private void OnDestroy()
        {
            Assets.Scripts.NetworkClient.ClientFinishedJoining -= OnClientFinishedJoining;
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

            var settingsPanel = new RecipesSettingsPanel(_uiManager, _panelManager);
            _panelManager.SetSettingsPanel(settingsPanel);
            _panelManager.SetContentManager(_contentManager);

            var userGuide = new RecipesUserGuide(_uiManager, _panelManager);
            _panelManager.SetUserGuide(userGuide);
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

        private void OnClientFinishedJoining()
        {
            if (ServerNoteManager != null)
            {
                ServerNoteManager.Shutdown();
                ServerNoteManager = null;
            }
            if (ClientSyncManager != null)
            {
                ClientSyncManager.OnNotesUpdated -= OnNetworkNotesUpdated;
                ClientSyncManager.OnSectionContentUpdated -= OnNetworkSectionUpdated;
                ClientSyncManager.OnPlayerColorUpdated -= OnPlayerColorChanged;
                ClientSyncManager.OnPlayerPresenceChanged -= OnPlayerPresenceChanged;
                ClientSyncManager.Shutdown();
                ClientSyncManager = null;
            }
            RuntimeContext.Reset();
            isWorldLoaded = false;

            RuntimeContext.Detect();

            if (RuntimeContext.Current != RuntimeContext.Mode.Client)
                return;

            Log.LogInfo("ClientFinishedJoining - initializing as client");

            try
            {
                _contentManager?.ClearNotes();
                _panelManager?.ResetToHidden();

                _currentWorldName = Assets.Scripts.Serialization.XmlSaveLoad.Instance?.CurrentStationName ?? "";
                _currentWorldId = World.CurrentId;
                _currentlyLoadedSaveFile = "";
                isWorldLoaded = true;
                _lastPersonalSaveTime = Time.unscaledTime;

                ClientSyncManager = new ClientSyncManager();
                ClientSyncManager.OnNotesUpdated += OnNetworkNotesUpdated;
                ClientSyncManager.OnSectionContentUpdated += OnNetworkSectionUpdated;
                ClientSyncManager.OnPlayerColorUpdated += OnPlayerColorChanged;
                ClientSyncManager.OnPlayerPresenceChanged += OnPlayerPresenceChanged;
                ClientSyncManager.Initialize();
            }
            catch (Exception ex)
            {
                Log.LogError($"Error in OnClientFinishedJoining: {ex.Message}");
            }
        }

        private void OnContextChanged()
        {
            Log.LogInfo($"Context changed - initializing for {RuntimeContext.Current}");

            if (RuntimeContext.HasServer && ServerNoteManager == null)
            {
                ServerNoteManager = new ServerNoteManager();
                ServerNoteManager.Initialize(_currentWorldName, _currentlyLoadedSaveFile);
            }

            if (RuntimeContext.HasClient && ClientSyncManager == null)
            {
                ClientSyncManager = new ClientSyncManager();
                ClientSyncManager.OnNotesUpdated += OnNetworkNotesUpdated;
                ClientSyncManager.OnSectionContentUpdated += OnNetworkSectionUpdated;
                ClientSyncManager.OnPlayerColorUpdated += OnPlayerColorChanged;
                ClientSyncManager.OnPlayerPresenceChanged += OnPlayerPresenceChanged;
                ClientSyncManager.Initialize();
                if (ServerNoteManager != null && _contentManager != null)
                {
                    _contentManager.MergePublicNotes(ServerNoteManager.PublicSections);
                }
            }
        }

        private void OnWorldUnloaded()
        {
            if (_contentManager?.HasSessionKey == true)
            {
                try
                {
                    _contentManager.SavePersonalNotes();
                }
                catch (Exception ex)
                {
                    Log.LogError($"Error saving personal notes on unload: {ex.Message}");
                }
            }

            ServerNoteManager?.Shutdown();
            ServerNoteManager = null;

            if (ClientSyncManager != null)
            {
                ClientSyncManager.OnNotesUpdated -= OnNetworkNotesUpdated;
                ClientSyncManager.OnSectionContentUpdated -= OnNetworkSectionUpdated;
                ClientSyncManager.OnPlayerColorUpdated -= OnPlayerColorChanged;
                ClientSyncManager.OnPlayerPresenceChanged -= OnPlayerPresenceChanged;
                ClientSyncManager.Shutdown();
                ClientSyncManager = null;
            }

            RuntimeContext.Reset();

            _currentWorldName = "";
            _currentWorldId = "";
            _currentlyLoadedSaveFile = "";
            isWorldLoaded = false;

            _contentManager?.ClearNotes();
            _panelManager?.ResetToHidden();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void OnSaveLoaded(string worldName, string saveFileName)
        {
            if (ServerNoteManager != null)
            {
                ServerNoteManager.Shutdown();
                ServerNoteManager = null;
            }
            if (ClientSyncManager != null)
            {
                ClientSyncManager.OnNotesUpdated -= OnNetworkNotesUpdated;
                ClientSyncManager.OnSectionContentUpdated -= OnNetworkSectionUpdated;
                ClientSyncManager.OnPlayerColorUpdated -= OnPlayerColorChanged;
                ClientSyncManager.OnPlayerPresenceChanged -= OnPlayerPresenceChanged;
                ClientSyncManager.Shutdown();
                ClientSyncManager = null;
            }
            RuntimeContext.Reset();

            _contentManager?.ClearNotes();
            _panelManager?.ResetToHidden();

            _currentWorldName = worldName;
            _currentWorldId = World.CurrentId;
            _currentlyLoadedSaveFile = saveFileName;
            isWorldLoaded = true;

            RuntimeContext.Detect();

            if (RuntimeContext.HasServer)
            {
                ServerNoteManager = new ServerNoteManager();
                ServerNoteManager.Initialize(worldName, saveFileName);
            }

            if (RuntimeContext.HasClient)
            {
                ClientSyncManager = new ClientSyncManager();
                ClientSyncManager.OnNotesUpdated += OnNetworkNotesUpdated;
                ClientSyncManager.OnSectionContentUpdated += OnNetworkSectionUpdated;
                ClientSyncManager.OnPlayerColorUpdated += OnPlayerColorChanged;
                ClientSyncManager.OnPlayerPresenceChanged += OnPlayerPresenceChanged;
                ClientSyncManager.Initialize();
            }

            if (RuntimeContext.HasUI)
            {
                switch (RuntimeContext.Current)
                {
                    case RuntimeContext.Mode.Singleplayer:
                        _contentManager?.LoadNotes(worldName, saveFileName);
                        break;

                    case RuntimeContext.Mode.NonDedicatedHost:
                        _contentManager?.LoadNotes(worldName, saveFileName);

                        if (ServerNoteManager != null)
                        {
                            _contentManager?.MergePublicNotes(ServerNoteManager.PublicSections);
                        }
                        break;

                    case RuntimeContext.Mode.Client:
                        if (ClientSyncManager?.HasSynced == true)
                        {
                            _contentManager?.LoadPersonalNotes(ClientSyncManager.SessionKey);
                            _contentManager?.MergePublicNotes(
                                ClientSyncManager.GetVisiblePublicSections());
                        }
                        break;
                }
            }
        }

        private void OnNetworkNotesUpdated()
        {
            if (_contentManager == null || ClientSyncManager == null) return;

            if (!(_contentManager.HasSessionKey) &&
                !string.IsNullOrEmpty(ClientSyncManager.SessionKey) &&
                RuntimeContext.Current == RuntimeContext.Mode.Client)
            {
                _contentManager.LoadPersonalNotes(ClientSyncManager.SessionKey);
            }

            _contentManager.MergePublicNotes(ClientSyncManager.GetVisiblePublicSections());
        }

        private void OnNetworkSectionUpdated(RecipeSection section)
        {
            _contentManager?.UpdateSectionInPlace(section);
        }

        private void OnPlayerColorChanged(ulong clientId, string colorHex)
        {
            _contentManager?.RefreshAccentColors();
        }

        private void OnPlayerPresenceChanged(ulong clientId, bool isOnline)
        {
            _contentManager?.RefreshPresenceIndicators();
        }

        public void OnSave(string saveFileName)
        {
            if (!isWorldLoaded) return;

            if (!string.IsNullOrEmpty(_currentWorldName))
            {
                _contentManager?.SaveNotes(_currentWorldName, saveFileName);
            }

            if (_contentManager?.HasSessionKey == true)
            {
                _contentManager.SavePersonalNotes();
            }

            ServerNoteManager?.SnapshotPublicNotes(saveFileName);
        }

        public void OnSaveAs(string worldName)
        {
            if (!isWorldLoaded) return;
            if (string.IsNullOrEmpty(worldName)) return;

            string saveFileName = worldName;
            _contentManager?.SaveNotes(worldName, saveFileName);

            ServerNoteManager?.SnapshotPublicNotes(saveFileName);
        }

        public void OnSaveDeleted(string deletedFileName)
        {
            if (!string.IsNullOrEmpty(_currentWorldName))
            {
                BeefsRecipesSaveManager.DeleteNotes(_currentWorldName, deletedFileName);
            }

            ServerNoteManager?.DeleteSnapshot(deletedFileName);
        }

        public void EnsureServerNoteManager()
        {
            if (ServerNoteManager != null) return;

            RuntimeContext.Detect();
            if (!RuntimeContext.HasServer) return;

            string worldName = _currentWorldName;
            if (string.IsNullOrEmpty(worldName))
            {
                worldName = XmlSaveLoad.Instance?.CurrentStationName ?? "";
            }
            if (string.IsNullOrEmpty(worldName))
            {
                worldName = WorldManager.CurrentWorldName;
            }
            if (string.IsNullOrEmpty(worldName))
            {
                Log.LogWarning("EnsureServerNoteManager: no world name available yet");
                return;
            }

            _currentWorldName = worldName;
            _currentWorldId = World.CurrentId;
            isWorldLoaded = true;

            ServerNoteManager = new ServerNoteManager();
            ServerNoteManager.Initialize(worldName, _currentlyLoadedSaveFile ?? "");
            Log.LogInfo($"ServerNoteManager lazy-initialized - world: {worldName}");
        }
    }
}