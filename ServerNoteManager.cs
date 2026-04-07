using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Assets.Scripts;
using Assets.Scripts.Networking;
using BepInEx;
using Newtonsoft.Json;

namespace BeefsRecipes
{
    public class ServerNoteManager
    {

        [Serializable]
        private class ServerIdentity
        {
            public string guid;
            public string createdAt;
        }

        [Serializable]
        public class PublicNotesData
        {
            public string worldName;
            public string serverGuid;
            public List<BeefsRecipesPlugin.RecipeSection> sections;
            public string lastModified;
            public int notesVersion;
        }

        [Serializable]
        public class ModerationData
        {
            public List<BannedPlayer> bannedPlayers = new List<BannedPlayer>();
            public Dictionary<string, PlayerRecord> playerRecords = new Dictionary<string, PlayerRecord>();
        }

        [Serializable]
        public class BannedPlayer
        {
            public ulong clientId;
            public string playerName;
            public string reason;
            public string bannedAt;
            public ulong bannedBy;
        }

        [Serializable]
        public class PlayerRecord
        {
            public ulong clientId;
            public string playerName;
            public int strikeCount;
            public string lastStrikeSectionId;
            public string lastStrikeTime;
        }

        private ModerationData _moderation = new ModerationData();
        private const string ModerationFileName = "moderation.json";

        private List<BeefsRecipesPlugin.RecipeSection> _publicSections = new List<BeefsRecipesPlugin.RecipeSection>();

        private readonly Dictionary<string, HashSet<ulong>> _removalVotes = new Dictionary<string, HashSet<ulong>>();

        private readonly HashSet<ulong> _connectedClients = new HashSet<ulong>();

        private readonly Dictionary<ulong, string> _playerColors = new Dictionary<ulong, string>();

        private string _serverGuid;
        private string _worldName;
        private string _sessionKey;

        private const string ConfigSubFolder = "BeefsRecipes";
        private const string SnapshotsSubFolder = "snapshots";
        private const string IdentityFileName = "server_identity.json";

        public string SessionKey => _sessionKey;
        public List<BeefsRecipesPlugin.RecipeSection> PublicSections => _publicSections;

        public void Initialize(string worldName, string saveFileName)
        {
            _worldName = worldName;
            _serverGuid = LoadOrCreateServerGuid();
            _sessionKey = ComputeSessionKey(_serverGuid, worldName);

            LoadPublicNotes(saveFileName);
            LoadModeration();

            BeefsRecipesPlugin.Log.LogInfo(
                $"ServerNoteManager initialized - world: {worldName}, " +
                $"session: {_sessionKey.Substring(0, 8)}..., " +
                $"public sections: {_publicSections.Count}");
        }

        public void Shutdown()
        {
            SavePublicNotes();
            SaveModeration();
            _publicSections.Clear();
            _moderation = new ModerationData();
            _removalVotes.Clear();
            _connectedClients.Clear();
            _playerColors.Clear();
            _sessionKey = null;
        }

        private string LoadOrCreateServerGuid()
        {
            string configDir = GetConfigDir();
            string guidPath = Path.Combine(configDir, IdentityFileName);

            if (File.Exists(guidPath))
            {
                try
                {
                    var identity = JsonConvert.DeserializeObject<ServerIdentity>(
                        File.ReadAllText(guidPath));

                    if (!string.IsNullOrEmpty(identity?.guid))
                    {
                        BeefsRecipesPlugin.Log.LogInfo(
                            $"Server identity loaded: {identity.guid.Substring(0, 8)}...");
                        return identity.guid;
                    }
                }
                catch (Exception ex)
                {
                    BeefsRecipesPlugin.Log.LogWarning(
                        $"Failed to load server identity, regenerating: {ex.Message}");
                }
            }

            string newGuid = Guid.NewGuid().ToString();
            try
            {
                var identity = new ServerIdentity
                {
                    guid = newGuid,
                    createdAt = DateTime.UtcNow.ToString("o")
                };
                File.WriteAllText(guidPath,
                    JsonConvert.SerializeObject(identity, Formatting.Indented));

                BeefsRecipesPlugin.Log.LogInfo(
                    $"New server identity created: {newGuid.Substring(0, 8)}...");
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError(
                    $"Failed to save server identity: {ex.Message}");
            }

            return newGuid;
        }

        public static string ComputeSessionKey(string serverGuid, string world)
        {
            string raw = $"{serverGuid}_{world}";
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                return BitConverter.ToString(hash).Replace("-", "").Substring(0, 32).ToLowerInvariant();
            }
        }

        public void OnClientConnected(ulong clientId)
        {
            _connectedClients.Add(clientId);
            BeefsRecipesPlugin.Log.LogInfo($"Client connected for notes: {clientId}");

            BroadcastPlayerPresence(clientId, true);
        }

        public void OnClientDisconnected(ulong clientId)
        {
            _connectedClients.Remove(clientId);
            _playerColors.Remove(clientId);

            foreach (var kvp in _removalVotes)
            {
                kvp.Value.Remove(clientId);
            }

            RecipesNetworkMessages.ClearTransfersForClient(clientId);

            BeefsRecipesPlugin.Log.LogInfo($"Client disconnected from notes: {clientId}");

            BroadcastPlayerPresence(clientId, false);
        }

        public void HandleFullSyncRequest(ulong clientId)
        {
            if (BeefsRecipesPlugin.BanEnforcement.Value)
            {
                string playerName = GetClientName(clientId);
                if (IsPlayerBanned(clientId, playerName))
                {
                    BeefsRecipesPlugin.Log.LogInfo(
                        $"Banned player {clientId} ({playerName}) attempted sync - disconnecting");
                    KickPlayer(clientId, "Banned from notes");
                    return;
                }
            }

            OnClientConnected(clientId);

            var votedSections = GetClientVotes(clientId);

            BeefsRecipesPlugin.Log.LogInfo(
                $"Full sync requested by {clientId} - sending {_publicSections.Count} sections");

            SendSyncToClient(clientId, votedSections);
        }

        public void HandleSectionUpdate(ulong clientId, BeefsRecipesPlugin.RecipeSection section)
        {
            if (section == null) return;

            int existingIndex = _publicSections.FindIndex(s => s.id == section.id);

            if (existingIndex >= 0)
            {
                if (_publicSections[existingIndex].ownerId != clientId)
                {
                    BeefsRecipesPlugin.Log.LogWarning(
                        $"Client {clientId} tried to edit section owned by {_publicSections[existingIndex].ownerId}");
                    return;
                }
                _publicSections[existingIndex] = section;
            }
            else
            {
                section.isPublic = true;
                section.ownerId = clientId;
                section.sharedTimestamp = DateTime.UtcNow.ToString("o");
                _publicSections.Add(section);
            }

            SavePublicNotes();

            BeefsRecipesPlugin.Log.LogInfo(
                $"Section {section.id.Substring(0, 8)}... updated by {clientId}");

            BroadcastSectionUpdate(section, false);
        }

        public void HandleDeleteSection(ulong clientId, string sectionId)
        {
            var section = _publicSections.Find(s => s.id == sectionId);
            if (section == null) return;

            bool isOwner = section.ownerId == clientId;

            bool isHostAdmin = BeefsRecipesPlugin.RuntimeContext.IsHostAdmin &&
                               clientId == GetHostClientId();

            if (isOwner || isHostAdmin)
            {
                RemoveSection(sectionId);
                BeefsRecipesPlugin.Log.LogInfo(
                    $"Section {sectionId.Substring(0, 8)}... deleted by {clientId} (owner={isOwner}, admin={isHostAdmin})");
                return;
            }

            BeefsRecipesPlugin.Log.LogWarning(
                $"Client {clientId} tried to delete section owned by {section.ownerId} - use vote system");
        }

        public void HandleVoteRemove(ulong clientId, string sectionId)
        {
            if (!BeefsRecipesPlugin.RuntimeContext.IsHeadlessMode()) return;

            var section = _publicSections.Find(s => s.id == sectionId);
            if (section == null) return;

            if (section.ownerId == clientId) return;

            if (!_removalVotes.ContainsKey(sectionId))
                _removalVotes[sectionId] = new HashSet<ulong>();

            _removalVotes[sectionId].Add(clientId);

            int voteCount = _removalVotes[sectionId].Count;
            int required = BeefsRecipesPlugin.VotesRequiredToRemove.Value;
            BeefsRecipesPlugin.Log.LogInfo(
                $"Vote to remove {sectionId.Substring(0, 8)}... by {clientId} - {voteCount}/{required}");

            if (voteCount >= required)
            {
                ulong ownerId = section.ownerId;
                string ownerName = section.ownerDisplayName ?? "";
                BeefsRecipesPlugin.Log.LogInfo(
                    $"Section {sectionId.Substring(0, 8)}... removed by vote ({voteCount} votes)");
                RemoveSection(sectionId);
                RecordStrike(ownerId, ownerName, sectionId);
            }
            else
            {
                BroadcastVoteCountUpdate(sectionId, voteCount);
            }
        }

        public void HandleRedactVote(ulong clientId, string sectionId)
        {
            if (_removalVotes.TryGetValue(sectionId, out var voters))
            {
                voters.Remove(clientId);
                int voteCount = voters.Count;

                BeefsRecipesPlugin.Log.LogInfo(
                    $"Vote redacted on {sectionId.Substring(0, 8)}... by {clientId} - {voteCount}/2");

                BroadcastVoteCountUpdate(sectionId, voteCount);
            }
        }

        public void HandlePlayerColor(ulong clientId, string colorHex)
        {
            if (string.IsNullOrEmpty(colorHex)) return;

            _playerColors[clientId] = colorHex;

            BeefsRecipesPlugin.Log.LogInfo(
                $"Player {clientId} accent color: {colorHex}");

            BroadcastPlayerColor(clientId, colorHex);
        }

        public Dictionary<ulong, string> GetPlayerColors() => new Dictionary<ulong, string>(_playerColors);

        public HashSet<ulong> GetConnectedClients() => new HashSet<ulong>(_connectedClients);

        public List<string> GetClientVotes(ulong clientId)
        {
            var voted = new List<string>();
            foreach (var kvp in _removalVotes)
            {
                if (kvp.Value.Contains(clientId))
                    voted.Add(kvp.Key);
            }
            return voted;
        }

        public int GetVoteCount(string sectionId)
        {
            if (_removalVotes.TryGetValue(sectionId, out var voters))
                return voters.Count;
            return 0;
        }

        private void RemoveSection(string sectionId)
        {
            _publicSections.RemoveAll(s => s.id == sectionId);
            _removalVotes.Remove(sectionId);
            SavePublicNotes();

            BroadcastSectionUpdate(new BeefsRecipesPlugin.RecipeSection { id = sectionId }, true);
        }

        private static ulong GetHostClientId()
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

        private static string GetClientName(ulong clientId)
        {
            try
            {
                var client = RecipesNetworkMessages.FindClientBySteamId(clientId);
                return client?.name ?? "";
            }
            catch
            {
                return "";
            }
        }

        public void RecordStrike(ulong clientId, string playerName, string sectionId)
        {
            string key = clientId.ToString();
            if (!_moderation.playerRecords.TryGetValue(key, out var record))
            {
                record = new PlayerRecord
                {
                    clientId = clientId,
                    playerName = playerName
                };
                _moderation.playerRecords[key] = record;
            }

            record.strikeCount++;
            record.lastStrikeSectionId = sectionId;
            record.lastStrikeTime = DateTime.UtcNow.ToString("o");
            if (!string.IsNullOrEmpty(playerName))
                record.playerName = playerName;

            SaveModeration();

            BeefsRecipesPlugin.Log.LogInfo(
                $"Strike recorded for {clientId} ({record.playerName}): " +
                $"{record.strikeCount} total strikes");

            int banThreshold = BeefsRecipesPlugin.StrikesBeforeBan.Value;
            int kickThreshold = BeefsRecipesPlugin.StrikesBeforeKick.Value;

            if (banThreshold > 0 && record.strikeCount >= banThreshold)
            {
                BanPlayer(clientId, record.playerName,
                    $"Auto-ban: {record.strikeCount} strikes", 0);
            }
            else if (kickThreshold > 0 && record.strikeCount >= kickThreshold)
            {
                KickPlayer(clientId, $"Auto-kick: {record.strikeCount} strikes");
            }
        }

        public void KickPlayer(ulong clientId, string reason)
        {
            BeefsRecipesPlugin.Log.LogInfo($"Kicking player {clientId}: {reason}");

            Client client = RecipesNetworkMessages.FindClientBySteamId(clientId);
            if (client != null)
            {
                client.Disconnect();
            }
            else
            {
                foreach (var c in NetworkBase.Clients)
                {
                    if (c.ClientId == clientId)
                    {
                        c.Disconnect();
                        return;
                    }
                }

                BeefsRecipesPlugin.Log.LogWarning(
                    $"Could not find client {clientId} to kick - may have already disconnected");
            }
        }

        public void BanPlayer(ulong clientId, string playerName, string reason, ulong bannedBy)
        {
            if (IsPlayerBanned(clientId, playerName))
            {
                BeefsRecipesPlugin.Log.LogInfo(
                    $"Player {clientId} ({playerName}) is already banned");
                KickPlayer(clientId, reason);
                return;
            }

            _moderation.bannedPlayers.Add(new BannedPlayer
            {
                clientId = clientId,
                playerName = playerName ?? "",
                reason = reason ?? "",
                bannedAt = DateTime.UtcNow.ToString("o"),
                bannedBy = bannedBy
            });

            SaveModeration();

            BeefsRecipesPlugin.Log.LogInfo(
                $"Player banned: {clientId} ({playerName}) - {reason}");

            KickPlayer(clientId, reason);
        }

        public bool IsPlayerBanned(ulong clientId, string playerName)
        {
            foreach (var ban in _moderation.bannedPlayers)
            {
                if (clientId != 0 && ban.clientId == clientId)
                    return true;

                if (!string.IsNullOrEmpty(playerName) &&
                    !string.IsNullOrEmpty(ban.playerName) &&
                    string.Equals(ban.playerName, playerName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public bool UnbanPlayer(ulong clientId)
        {
            int removed = _moderation.bannedPlayers.RemoveAll(b => b.clientId == clientId);
            if (removed > 0)
            {
                SaveModeration();
                BeefsRecipesPlugin.Log.LogInfo($"Player unbanned by ID: {clientId}");
                return true;
            }
            return false;
        }

        public bool UnbanPlayer(string playerName)
        {
            int removed = _moderation.bannedPlayers.RemoveAll(b =>
                string.Equals(b.playerName, playerName, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                SaveModeration();
                BeefsRecipesPlugin.Log.LogInfo($"Player unbanned by name: {playerName}");
                return true;
            }
            return false;
        }

        public int GetStrikeCount(ulong clientId)
        {
            string key = clientId.ToString();
            if (_moderation.playerRecords.TryGetValue(key, out var record))
                return record.strikeCount;
            return 0;
        }

        private void LoadModeration()
        {
            string path = GetModerationPath();
            if (!File.Exists(path))
            {
                _moderation = new ModerationData();
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                _moderation = JsonConvert.DeserializeObject<ModerationData>(json)
                              ?? new ModerationData();

                BeefsRecipesPlugin.Log.LogInfo(
                    $"Moderation data loaded: {_moderation.bannedPlayers.Count} bans, " +
                    $"{_moderation.playerRecords.Count} player records");
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Failed to load moderation data: {ex.Message}");
                _moderation = new ModerationData();
            }
        }

        private void SaveModeration()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_moderation, Formatting.Indented);
                File.WriteAllText(GetModerationPath(), json);
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Failed to save moderation data: {ex.Message}");
            }
        }

        private string GetModerationPath()
        {
            return Path.Combine(GetWorldDir(), ModerationFileName);
        }

        private string GetConfigDir()
        {
            string dir = Path.Combine(Paths.ConfigPath, ConfigSubFolder);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        private string GetWorldDir()
        {
            string dir = Path.Combine(GetConfigDir(), _worldName);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        private string GetHeadPath()
        {
            return Path.Combine(GetWorldDir(), $"{_worldName}_public.json");
        }

        private string GetSnapshotDir()
        {
            string dir = Path.Combine(GetWorldDir(), SnapshotsSubFolder);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        private string GetSnapshotPath(string saveFileName)
        {
            return Path.Combine(GetSnapshotDir(), $"{_worldName}_{saveFileName}_public.json");
        }

        private void LoadPublicNotes(string saveFileName)
        {
            string snapshotPath = !string.IsNullOrEmpty(saveFileName) ? GetSnapshotPath(saveFileName) : null;
            string headPath = GetHeadPath();

            string loadPath = null;
            string loadSource = null;

            if (snapshotPath != null && File.Exists(snapshotPath))
            {
                loadPath = snapshotPath;
                loadSource = "snapshot";
            }
            else if (File.Exists(headPath))
            {
                loadPath = headPath;
                loadSource = "head";
            }

            if (loadPath == null)
            {
                _publicSections = new List<BeefsRecipesPlugin.RecipeSection>();
                return;
            }

            try
            {
                string json = File.ReadAllText(loadPath);
                var data = JsonConvert.DeserializeObject<PublicNotesData>(json);
                _publicSections = data?.sections ?? new List<BeefsRecipesPlugin.RecipeSection>();

                foreach (var section in _publicSections)
                {
                    if (string.IsNullOrEmpty(section.id))
                        section.id = Guid.NewGuid().ToString();
                }

                BeefsRecipesPlugin.Log.LogInfo(
                    $"Public notes loaded ({loadSource}): {_publicSections.Count} sections from {Path.GetFileName(loadPath)}");
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Failed to load public notes: {ex.Message}");
                _publicSections = new List<BeefsRecipesPlugin.RecipeSection>();
            }
        }

        private void SavePublicNotes()
        {
            try
            {
                var data = new PublicNotesData
                {
                    worldName = _worldName,
                    serverGuid = _serverGuid,
                    sections = _publicSections,
                    lastModified = DateTime.UtcNow.ToString("o"),
                    notesVersion = 1
                };

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(GetHeadPath(), json);
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Failed to save public notes: {ex.Message}");
            }
        }

        public void SnapshotPublicNotes(string saveFileName)
        {
            if (string.IsNullOrEmpty(saveFileName)) return;

            try
            {
                var data = new PublicNotesData
                {
                    worldName = _worldName,
                    serverGuid = _serverGuid,
                    sections = _publicSections,
                    lastModified = DateTime.UtcNow.ToString("o"),
                    notesVersion = 1
                };

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                string snapshotPath = GetSnapshotPath(saveFileName);
                File.WriteAllText(snapshotPath, json);

                BeefsRecipesPlugin.Log.LogInfo(
                    $"Public notes snapshot: {Path.GetFileName(snapshotPath)}");
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Failed to snapshot public notes: {ex.Message}");
            }
        }

        public void DeleteSnapshot(string saveFileName)
        {
            if (string.IsNullOrEmpty(saveFileName)) return;

            try
            {
                string snapshotPath = GetSnapshotPath(saveFileName);
                if (File.Exists(snapshotPath))
                {
                    File.Delete(snapshotPath);
                    BeefsRecipesPlugin.Log.LogInfo(
                        $"Public notes snapshot deleted: {Path.GetFileName(snapshotPath)}");
                }
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Failed to delete public notes snapshot: {ex.Message}");
            }
        }

        private void SendSyncToClient(ulong clientSteamId, List<string> votedSections)
        {
            var colorDict = new Dictionary<string, string>();
            foreach (var kvp in _playerColors)
                colorDict[kvp.Key.ToString()] = kvp.Value;

            RecipesNetworkMessages.SendSyncResponse(
                clientSteamId,
                _sessionKey,
                JsonConvert.SerializeObject(_publicSections),
                JsonConvert.SerializeObject(votedSections),
                JsonConvert.SerializeObject(colorDict),
                JsonConvert.SerializeObject(new List<ulong>(_connectedClients)));
        }

        private void BroadcastSectionUpdate(BeefsRecipesPlugin.RecipeSection section, bool isDelete)
        {
            if (Assets.Scripts.NetworkServer.HasClients())
            {
                RecipesNetworkMessages.BroadcastSectionUpdate(
                    JsonConvert.SerializeObject(section), isDelete);
            }

            if (BeefsRecipesPlugin.RuntimeContext.IsHostAdmin)
            {
                BeefsRecipesPlugin.Instance.ClientSyncManager?
                    .HandleBroadcastUpdate(section, isDelete);
            }
        }

        private void BroadcastVoteCountUpdate(string sectionId, int voteCount)
        {
            if (Assets.Scripts.NetworkServer.HasClients())
            {
                RecipesNetworkMessages.BroadcastToAll(new NotesVoteUpdate
                {
                    SectionId = sectionId,
                    VoteCount = voteCount
                });
            }

            if (BeefsRecipesPlugin.RuntimeContext.IsHostAdmin)
            {
                BeefsRecipesPlugin.Instance.ClientSyncManager?
                    .HandleVoteUpdate(sectionId, voteCount);
            }
        }

        private void BroadcastPlayerColor(ulong clientId, string colorHex)
        {
            if (Assets.Scripts.NetworkServer.HasClients())
            {
                RecipesNetworkMessages.BroadcastToAll(new NotesPlayerColorBroadcast
                {
                    ClientId = clientId,
                    ColorHex = colorHex
                });
            }

            if (BeefsRecipesPlugin.RuntimeContext.IsHostAdmin)
            {
                BeefsRecipesPlugin.Instance.ClientSyncManager?
                    .HandlePlayerColorUpdate(clientId, colorHex);
            }
        }

        private void BroadcastPlayerPresence(ulong clientId, bool isOnline)
        {
            if (Assets.Scripts.NetworkServer.HasClients())
            {
                RecipesNetworkMessages.BroadcastToAll(new NotesPlayerPresence
                {
                    ClientId = clientId,
                    IsOnline = isOnline
                });
            }

            if (BeefsRecipesPlugin.RuntimeContext.IsHostAdmin)
            {
                BeefsRecipesPlugin.Instance.ClientSyncManager?
                    .HandlePlayerPresence(clientId, isOnline);
            }
        }
    }
}