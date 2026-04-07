using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Assets.Scripts.Objects.Entities;
using BepInEx;
using Newtonsoft.Json;
using UnityEngine;

namespace BeefsRecipes
{
    public class ClientSyncManager
    {
        private string _sessionKey;
        private List<BeefsRecipesPlugin.RecipeSection> _publicSections = new List<BeefsRecipesPlugin.RecipeSection>();
        private HashSet<string> _hiddenSectionIds = new HashSet<string>();
        private HashSet<string> _votedSectionIds = new HashSet<string>();
        private Dictionary<string, int> _voteCounts = new Dictionary<string, int>();
        private bool _hasSynced = false;

        private readonly Dictionary<ulong, string> _playerColors = new Dictionary<ulong, string>();
        private string _localColorOverride = null;
        private static bool _overrideLoaded = false;

        private readonly HashSet<ulong> _onlinePlayers = new HashSet<ulong>();

        private const string AccentOverrideFileName = "accent_override.json";
        private static string AccentOverridePath =>
            Path.Combine(Paths.ConfigPath, "BeefsRecipes", "client", AccentOverrideFileName);

        public event Action OnNotesUpdated;

        public event Action<BeefsRecipesPlugin.RecipeSection> OnSectionContentUpdated;

        public event Action<string, int> OnVoteUpdated;

        public event Action<ulong, string> OnPlayerColorUpdated;

        public event Action<ulong, bool> OnPlayerPresenceChanged;

        public string SessionKey => _sessionKey;
        public bool HasSynced => _hasSynced;
        public List<BeefsRecipesPlugin.RecipeSection> PublicSections => _publicSections;

        public void Initialize()
        {
            var context = BeefsRecipesPlugin.RuntimeContext.Current;
            LoadAccentOverride();

            if (context == BeefsRecipesPlugin.RuntimeContext.Mode.NonDedicatedHost)
            {
                var serverManager = BeefsRecipesPlugin.Instance.ServerNoteManager;
                if (serverManager != null)
                {
                    _sessionKey = serverManager.SessionKey;
                    _publicSections = new List<BeefsRecipesPlugin.RecipeSection>(serverManager.PublicSections);
                    _hasSynced = true;

                    _onlinePlayers.Clear();
                    foreach (var id in serverManager.GetConnectedClients())
                        _onlinePlayers.Add(id);
                    _onlinePlayers.Add(GetLocalClientId());
                    AnnounceColor();

                    BeefsRecipesPlugin.Log.LogInfo(
                        $"ClientSyncManager initialized (local host) - {_publicSections.Count} public sections");
                }
            }
            else if (context == BeefsRecipesPlugin.RuntimeContext.Mode.Client)
            {
                RequestFullSync();
            }
        }

        public void Shutdown()
        {
            _sessionKey = null;
            _publicSections.Clear();
            _hiddenSectionIds.Clear();
            _votedSectionIds.Clear();
            _voteCounts.Clear();
            _playerColors.Clear();
            _onlinePlayers.Clear();
            _hasSynced = false;
            _overrideLoaded = false;
        }

        public void HandleFullSyncResponse(
            string sessionKey,
            List<BeefsRecipesPlugin.RecipeSection> publicSections,
            List<string> clientVotedSections,
            Dictionary<string, string> playerColors,
            List<ulong> onlinePlayerIds)
        {
            _sessionKey = sessionKey;
            _publicSections = publicSections ?? new List<BeefsRecipesPlugin.RecipeSection>();
            _votedSectionIds = new HashSet<string>(clientVotedSections ?? new List<string>());
            _hasSynced = true;

            _playerColors.Clear();
            if (playerColors != null)
            {
                foreach (var kvp in playerColors)
                {
                    if (ulong.TryParse(kvp.Key, out ulong id))
                        _playerColors[id] = kvp.Value;
                }
            }

            _onlinePlayers.Clear();
            if (onlinePlayerIds != null)
            {
                foreach (var id in onlinePlayerIds)
                    _onlinePlayers.Add(id);
            }
            _onlinePlayers.Add(GetLocalClientId());

            AnnounceColor();

            BeefsRecipesPlugin.Log.LogInfo(
                $"Full sync received - session: {sessionKey?.Substring(0, 8)}..., " +
                $"{_publicSections.Count} public sections, {_playerColors.Count} player colors, " +
                $"{_onlinePlayers.Count} online players");

            OnNotesUpdated?.Invoke();
        }

        public void HandleBroadcastUpdate(BeefsRecipesPlugin.RecipeSection section, bool isDelete)
        {
            if (section == null) return;

            if (isDelete)
            {
                _publicSections.RemoveAll(s => s.id == section.id);
                _hiddenSectionIds.Remove(section.id);
                _votedSectionIds.Remove(section.id);
                _voteCounts.Remove(section.id);
                OnNotesUpdated?.Invoke();
                return;
            }

            int idx = _publicSections.FindIndex(s => s.id == section.id);
            bool isNewSection = idx < 0;

            if (idx >= 0)
                _publicSections[idx] = section;
            else
                _publicSections.Add(section);

            if (isNewSection)
            {
                OnNotesUpdated?.Invoke();
            }
            else if (section.ownerId != GetLocalClientId())
            {
                OnSectionContentUpdated?.Invoke(section);
            }
        }

        public void HandleVoteUpdate(string sectionId, int voteCount)
        {
            _voteCounts[sectionId] = voteCount;
            OnVoteUpdated?.Invoke(sectionId, voteCount);
        }

        public void HandlePlayerColorUpdate(ulong clientId, string colorHex)
        {
            if (string.IsNullOrEmpty(colorHex)) return;
            _playerColors[clientId] = colorHex;
            OnPlayerColorUpdated?.Invoke(clientId, colorHex);
        }

        public void HandlePlayerPresence(ulong clientId, bool isOnline)
        {
            if (isOnline)
                _onlinePlayers.Add(clientId);
            else
                _onlinePlayers.Remove(clientId);

            OnPlayerPresenceChanged?.Invoke(clientId, isOnline);
        }

        public bool IsPlayerOnline(ulong clientId) => _onlinePlayers.Contains(clientId);

        private void RequestFullSync()
        {
            RecipesNetworkMessages.SendToServer(new NotesSyncRequest
            {
                SenderSteamId = GetLocalClientId()
            });
            BeefsRecipesPlugin.Log.LogInfo("Requesting full sync from server");
        }

        public void ShareSection(BeefsRecipesPlugin.RecipeSection section)
        {
            if (section == null) return;
            section.isPublic = true;
            section.ownerId = GetLocalClientId();
            section.ownerDisplayName = GetLocalUsername();
            section.sharedTimestamp = DateTime.UtcNow.ToString("o");

            _publicSections.Add(section);

            SendSectionToServer(section);
            OnNotesUpdated?.Invoke();
        }

        public BeefsRecipesPlugin.RecipeSection UnshareSection(string sectionId)
        {
            var section = _publicSections.Find(s => s.id == sectionId);
            if (section == null) return null;
            if (section.ownerId != GetLocalClientId()) return null;

            _publicSections.Remove(section);

            SendDeleteToServer(sectionId);

            section.isPublic = false;
            section.ownerId = 0;
            section.ownerDisplayName = null;
            section.sharedTimestamp = null;

            OnNotesUpdated?.Invoke();

            return section;
        }

        public void PushPublicSectionUpdates(List<BeefsRecipesPlugin.RecipeSection> modifiedSections)
        {
            if (modifiedSections == null) return;

            foreach (var section in modifiedSections)
            {
                if (section.ownerId == GetLocalClientId())
                {
                    SendSectionToServer(section);
                }
            }
        }

        public void DeletePublicSection(string sectionId)
        {
            SendDeleteToServer(sectionId);
        }

        public void VoteToRemove(string sectionId)
        {
            _votedSectionIds.Add(sectionId);
            SendVoteToServer(sectionId);
        }

        public void RedactVote(string sectionId)
        {
            _votedSectionIds.Remove(sectionId);
            SendRedactToServer(sectionId);
        }

        public void HideSection(string sectionId)
        {
            _hiddenSectionIds.Add(sectionId);
        }

        public void UnhideSection(string sectionId)
        {
            _hiddenSectionIds.Remove(sectionId);
        }

        public bool IsSectionHidden(string sectionId) => _hiddenSectionIds.Contains(sectionId);
        public bool HasVotedOn(string sectionId) => _votedSectionIds.Contains(sectionId);

        public int GetVoteCount(string sectionId)
        {
            return _voteCounts.TryGetValue(sectionId, out int count) ? count : 0;
        }

        public List<BeefsRecipesPlugin.RecipeSection> GetVisiblePublicSections()
        {
            return _publicSections.FindAll(s => !_hiddenSectionIds.Contains(s.id));
        }

        public HashSet<string> GetHiddenSectionIds() => _hiddenSectionIds;

        private void SendSectionToServer(BeefsRecipesPlugin.RecipeSection section)
        {
            if (IsLocalHost())
            {
                BeefsRecipesPlugin.Instance.ServerNoteManager?
                    .HandleSectionUpdate(GetLocalClientId(), section);
                return;
            }

            RecipesNetworkMessages.SendSectionPush(
                GetLocalClientId(),
                JsonConvert.SerializeObject(section));
        }

        private void SendDeleteToServer(string sectionId)
        {
            if (IsLocalHost())
            {
                BeefsRecipesPlugin.Instance.ServerNoteManager?
                    .HandleDeleteSection(GetLocalClientId(), sectionId);
                return;
            }

            RecipesNetworkMessages.SendToServer(new NotesSectionDelete
            {
                SenderSteamId = GetLocalClientId(),
                SectionId = sectionId
            });
        }

        private void SendVoteToServer(string sectionId)
        {
            if (IsLocalHost())
            {
                BeefsRecipesPlugin.Instance.ServerNoteManager?
                    .HandleVoteRemove(GetLocalClientId(), sectionId);
                return;
            }

            RecipesNetworkMessages.SendToServer(new NotesVoteRemove
            {
                SenderSteamId = GetLocalClientId(),
                SectionId = sectionId
            });
        }

        private void SendRedactToServer(string sectionId)
        {
            if (IsLocalHost())
            {
                BeefsRecipesPlugin.Instance.ServerNoteManager?
                    .HandleRedactVote(GetLocalClientId(), sectionId);
                return;
            }

            RecipesNetworkMessages.SendToServer(new NotesVoteRedact
            {
                SenderSteamId = GetLocalClientId(),
                SectionId = sectionId
            });
        }

        private static bool IsLocalHost()
        {
            return BeefsRecipesPlugin.RuntimeContext.Current ==
                   BeefsRecipesPlugin.RuntimeContext.Mode.NonDedicatedHost;
        }

        private static ulong GetLocalClientId()
        {
            try
            {
                return Assets.Scripts.Networking.NetworkManager.LocalClientId;
            }
            catch
            {
                return 0;
            }
        }

        private static string GetLocalUsername()
        {
            try
            {
                return Assets.Scripts.Networking.NetworkManager.Username ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        public string GetPlayerColor(ulong clientId)
        {
            if (_playerColors.TryGetValue(clientId, out string hex) && !string.IsNullOrEmpty(hex))
                return hex;
            return HashToHue(clientId);
        }

        public void AnnounceColor()
        {
            string hex = !string.IsNullOrEmpty(_localColorOverride)
                ? _localColorOverride
                : GetSuitColorHex();

            ulong localId = GetLocalClientId();
            _playerColors[localId] = hex;

            if (IsLocalHost())
            {
                BeefsRecipesPlugin.Instance.ServerNoteManager?
                    .HandlePlayerColor(localId, hex);
            }
            else
            {
                RecipesNetworkMessages.SendToServer(new NotesPlayerColor
                {
                    SenderSteamId = GetLocalClientId(),
                    ColorHex = hex
                });
            }

            BeefsRecipesPlugin.Log.LogInfo($"Announced accent color: {hex}");
        }

        public void SetAccentColorOverride(string hex)
        {
            _localColorOverride = hex;
            SaveAccentOverride();
            AnnounceColor();
        }

        public string GetAccentColorOverride() => _localColorOverride;

        private static string GetSuitColorHex()
        {
            try
            {
                var human = Human.LocalHuman;
                if (human != null && human.Suit != null)
                {
                    var suitThing = human.Suit.AsThing;
                    if (suitThing != null && suitThing.CustomColor != null
                        && suitThing.CustomColor.IsSet)
                    {
                        Color color = suitThing.CustomColor.Color;
                        if (color != Color.clear && color != Color.black)
                            return "#" + ColorUtility.ToHtmlStringRGB(color);
                    }
                }
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogWarning(
                    $"Failed to read suit color: {ex.Message}");
            }

            return HashToHue(GetLocalClientId());
        }

        internal static string HashToHue(ulong clientId)
        {
            uint hash;
            using (var md5 = MD5.Create())
            {
                byte[] bytes = BitConverter.GetBytes(clientId);
                byte[] hashed = md5.ComputeHash(bytes);
                hash = BitConverter.ToUInt32(hashed, 0);
            }

            float hue = (hash % 360) / 360f;
            Color color = Color.HSVToRGB(hue, 0.6f, 0.95f);
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        private void LoadAccentOverride()
        {
            if (_overrideLoaded) return;
            _overrideLoaded = true;

            try
            {
                if (File.Exists(AccentOverridePath))
                {
                    string json = File.ReadAllText(AccentOverridePath);
                    var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (data != null && data.TryGetValue("colorHex", out string hex)
                        && !string.IsNullOrEmpty(hex))
                    {
                        _localColorOverride = hex;
                        BeefsRecipesPlugin.Log.LogInfo($"Accent override loaded: {hex}");
                    }
                }
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogWarning($"Failed to load accent override: {ex.Message}");
            }
        }

        private void SaveAccentOverride()
        {
            try
            {
                string dir = Path.GetDirectoryName(AccentOverridePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var data = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(_localColorOverride))
                    data["colorHex"] = _localColorOverride;

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(AccentOverridePath, json);
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogWarning($"Failed to save accent override: {ex.Message}");
            }
        }
    }
}