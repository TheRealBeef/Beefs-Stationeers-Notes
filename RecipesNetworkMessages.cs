using System;
using System.Collections.Generic;
using System.Text;
using Assets.Scripts;
using Assets.Scripts.Networking;
using LaunchPadBooster;
using LaunchPadBooster.Networking;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace BeefsRecipes
{
    public static class RecipesNetworkMessages
    {
        private static Mod _mod;

        internal const int ChunkSize = 750;

        internal const byte TagSectionPush = 0;
        internal const byte TagSyncResponse = 1;
        internal const byte TagBroadcastUpdate = 2;

        private static ushort _nextTransferId = 0;

        private static readonly Dictionary<(ulong, ushort), (string[] chunks, float timestamp)> _reassembly
            = new Dictionary<(ulong, ushort), (string[] chunks, float timestamp)>();

        private const float StaleTransferTimeout = 30f;

        public static void ClearTransfersForClient(ulong clientId)
        {
            var toRemove = new List<(ulong, ushort)>();
            foreach (var key in _reassembly.Keys)
            {
                if (key.Item1 == clientId)
                    toRemove.Add(key);
            }
            foreach (var key in toRemove)
                _reassembly.Remove(key);

            if (toRemove.Count > 0)
                BeefsRecipesPlugin.Log.LogInfo($"Cleared {toRemove.Count} incomplete transfer(s) for client {clientId}");
        }

        public static void CleanupStaleTransfers()
        {
            if (_reassembly.Count == 0) return;

            float now = UnityEngine.Time.unscaledTime;
            var stale = new List<(ulong, ushort)>();
            foreach (var kvp in _reassembly)
            {
                if (now - kvp.Value.timestamp > StaleTransferTimeout)
                    stale.Add(kvp.Key);
            }
            foreach (var key in stale)
                _reassembly.Remove(key);

            if (stale.Count > 0)
                BeefsRecipesPlugin.Log.LogInfo($"Cleaned up {stale.Count} stale transfer(s)");
        }

        public static void Initialize()
        {
            _mod = new Mod(PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION);

            _mod.RegisterNetworkMessage<NotesSyncRequest>();
            _mod.RegisterNetworkMessage<NotesSectionDelete>();
            _mod.RegisterNetworkMessage<NotesVoteRemove>();
            _mod.RegisterNetworkMessage<NotesVoteRedact>();
            _mod.RegisterNetworkMessage<NotesVoteUpdate>();
            _mod.RegisterNetworkMessage<NotesPlayerColor>();
            _mod.RegisterNetworkMessage<NotesPlayerColorBroadcast>();
            _mod.RegisterNetworkMessage<NotesPlayerPresence>();
            _mod.RegisterNetworkMessage<NotesChunkedPayload>();

            BeefsRecipesPlugin.Log.LogInfo("Network messages registered with LaunchPadBooster");
        }

        public static Client FindClientBySteamId(ulong steamId)
        {
            foreach (var client in NetworkBase.Clients)
            {
                if (client.ClientId == steamId)
                    return client;
            }
            return null;
        }

        public static void SendToServer<T>(T message) where T : ModNetworkMessage<T>, new()
        {
            try
            {
                BeefsRecipesPlugin.Log.LogInfo($"SendToServer<{typeof(T).Name}>");
                NetworkClient.SendToServer<T>((MessageBase<T>)message, NetworkChannel.GeneralTraffic);
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Failed to send {typeof(T).Name}: {ex.Message}");
            }
        }

        public static void SendToClient<T>(ulong clientSteamId, T message) where T : ModNetworkMessage<T>, new()
        {
            try
            {
                Client client = FindClientBySteamId(clientSteamId);
                if (client == null)
                {
                    BeefsRecipesPlugin.Log.LogWarning($"SendToClient<{typeof(T).Name}>: no Client with SteamID {clientSteamId}");
                    return;
                }
                BeefsRecipesPlugin.Log.LogInfo($"SendToClient<{typeof(T).Name}> to SteamID={clientSteamId}");
                NetworkServer.SendToClient<T>((MessageBase<T>)message, NetworkChannel.GeneralTraffic, client);
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Failed to send {typeof(T).Name} to {clientSteamId}: {ex.Message}");
            }
        }

        public static void BroadcastToAll<T>(T message) where T : ModNetworkMessage<T>, new()
        {
            try
            {
                BeefsRecipesPlugin.Log.LogInfo($"BroadcastToAll<{typeof(T).Name}>");
                NetworkServer.SendToClients<T>((MessageBase<T>)message, NetworkChannel.GeneralTraffic, -1L);
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Failed to broadcast {typeof(T).Name}: {ex.Message}");
            }
        }

        public static void SendSectionPush(ulong senderSteamId, string sectionJson)
        {
            SendChunkedToServer(TagSectionPush, senderSteamId, sectionJson);
        }

        public static void SendSyncResponse(
            ulong clientSteamId,
            string sessionKey,
            string sectionsJson,
            string votedSectionsJson,
            string playerColorsJson,
            string onlinePlayerIdsJson)
        {
            string fullPayload = JsonConvert.SerializeObject(new SyncResponsePayload
            {
                SessionKey = sessionKey,
                SectionsJson = sectionsJson,
                VotedSectionsJson = votedSectionsJson,
                PlayerColorsJson = playerColorsJson,
                OnlinePlayerIdsJson = onlinePlayerIdsJson
            });

            SendChunkedToClient(TagSyncResponse, clientSteamId, fullPayload);
        }

        public static void BroadcastSectionUpdate(string sectionJson, bool isDelete)
        {
            string fullPayload = JsonConvert.SerializeObject(new BroadcastUpdatePayload
            {
                SectionJson = sectionJson,
                IsDelete = isDelete
            });

            BroadcastChunked(TagBroadcastUpdate, fullPayload);
        }

        private static void SendChunkedToServer(byte tag, ulong senderSteamId, string payload)
        {
            ushort transferId = _nextTransferId++;
            int totalChunks = (payload.Length + ChunkSize - 1) / ChunkSize;

            BeefsRecipesPlugin.Log.LogInfo(
                $"Chunking to server: tag={tag}, transferId={transferId}, " +
                $"{payload.Length} chars → {totalChunks} chunks");

            for (int i = 0; i < totalChunks; i++)
            {
                int start = i * ChunkSize;
                int length = Math.Min(ChunkSize, payload.Length - start);

                SendToServer(new NotesChunkedPayload
                {
                    TransferId = transferId,
                    ChunkIndex = (ushort)i,
                    TotalChunks = (ushort)totalChunks,
                    Tag = tag,
                    SenderSteamId = senderSteamId,
                    Payload = payload.Substring(start, length)
                });
            }
        }

        private static void SendChunkedToClient(byte tag, ulong clientSteamId, string payload)
        {
            ushort transferId = _nextTransferId++;
            int totalChunks = (payload.Length + ChunkSize - 1) / ChunkSize;

            BeefsRecipesPlugin.Log.LogInfo(
                $"Chunking to client {clientSteamId}: tag={tag}, transferId={transferId}, " +
                $"{payload.Length} chars → {totalChunks} chunks");

            for (int i = 0; i < totalChunks; i++)
            {
                int start = i * ChunkSize;
                int length = Math.Min(ChunkSize, payload.Length - start);

                SendToClient(clientSteamId, new NotesChunkedPayload
                {
                    TransferId = transferId,
                    ChunkIndex = (ushort)i,
                    TotalChunks = (ushort)totalChunks,
                    Tag = tag,
                    SenderSteamId = 0,
                    Payload = payload.Substring(start, length)
                });
            }
        }

        private static void BroadcastChunked(byte tag, string payload)
        {
            ushort transferId = _nextTransferId++;
            int totalChunks = (payload.Length + ChunkSize - 1) / ChunkSize;

            BeefsRecipesPlugin.Log.LogInfo(
                $"Chunking broadcast: tag={tag}, transferId={transferId}, " +
                $"{payload.Length} chars → {totalChunks} chunks");

            for (int i = 0; i < totalChunks; i++)
            {
                int start = i * ChunkSize;
                int length = Math.Min(ChunkSize, payload.Length - start);

                BroadcastToAll(new NotesChunkedPayload
                {
                    TransferId = transferId,
                    ChunkIndex = (ushort)i,
                    TotalChunks = (ushort)totalChunks,
                    Tag = tag,
                    SenderSteamId = 0,
                    Payload = payload.Substring(start, length)
                });
            }
        }

        internal static void HandleChunk(NotesChunkedPayload chunk)
        {
            var key = (chunk.SenderSteamId, chunk.TransferId);

            string[] chunks;
            if (_reassembly.TryGetValue(key, out var entry))
            {
                chunks = entry.chunks;
            }
            else
            {
                chunks = new string[chunk.TotalChunks];
                _reassembly[key] = (chunks, UnityEngine.Time.unscaledTime);
            }

            if (chunk.ChunkIndex >= chunks.Length)
            {
                BeefsRecipesPlugin.Log.LogWarning(
                    $"Chunk index {chunk.ChunkIndex} out of range for transfer {chunk.TransferId}");
                return;
            }

            chunks[chunk.ChunkIndex] = chunk.Payload;

            for (int i = 0; i < chunks.Length; i++)
            {
                if (chunks[i] == null) return;
            }

            _reassembly.Remove(key);

            var sb = new StringBuilder();
            for (int i = 0; i < chunks.Length; i++)
                sb.Append(chunks[i]);

            string fullPayload = sb.ToString();

            BeefsRecipesPlugin.Log.LogInfo(
                $"Reassembled transfer {chunk.TransferId}: tag={chunk.Tag}, " +
                $"{fullPayload.Length} chars from {chunks.Length} chunks");

            DispatchReassembled(chunk.Tag, chunk.SenderSteamId, fullPayload);
        }

        private static void DispatchReassembled(byte tag, ulong senderSteamId, string payload)
        {
            try
            {
                switch (tag)
                {
                    case TagSectionPush:
                    {
                        var section = JsonConvert.DeserializeObject<BeefsRecipesPlugin.RecipeSection>(payload);
                        if (section == null) return;
                        BeefsRecipesPlugin.Instance?.ServerNoteManager?
                            .HandleSectionUpdate(senderSteamId, section);
                        break;
                    }
                    case TagSyncResponse:
                    {
                        var envelope = JsonConvert.DeserializeObject<SyncResponsePayload>(payload);
                        if (envelope == null) return;

                        var sections = JsonConvert.DeserializeObject<List<BeefsRecipesPlugin.RecipeSection>>(
                            envelope.SectionsJson) ?? new List<BeefsRecipesPlugin.RecipeSection>();
                        var votedSections = JsonConvert.DeserializeObject<List<string>>(
                            envelope.VotedSectionsJson) ?? new List<string>();
                        var playerColors = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                            envelope.PlayerColorsJson) ?? new Dictionary<string, string>();
                        var onlinePlayerIds = JsonConvert.DeserializeObject<List<ulong>>(
                            envelope.OnlinePlayerIdsJson) ?? new List<ulong>();

                        BeefsRecipesPlugin.Instance?.ClientSyncManager?
                            .HandleFullSyncResponse(envelope.SessionKey, sections, votedSections,
                                playerColors, onlinePlayerIds);
                        break;
                    }
                    case TagBroadcastUpdate:
                    {
                        var envelope = JsonConvert.DeserializeObject<BroadcastUpdatePayload>(payload);
                        if (envelope == null) return;

                        var section = JsonConvert.DeserializeObject<BeefsRecipesPlugin.RecipeSection>(
                            envelope.SectionJson);
                        if (section == null) return;

                        BeefsRecipesPlugin.Instance?.ClientSyncManager?
                            .HandleBroadcastUpdate(section, envelope.IsDelete);
                        break;
                    }
                    default:
                        BeefsRecipesPlugin.Log.LogWarning($"Unknown chunked message tag: {tag}");
                        break;
                }
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Error dispatching reassembled tag={tag}: {ex.Message}");
            }
        }

        [Serializable]
        internal class SyncResponsePayload
        {
            public string SessionKey;
            public string SectionsJson;
            public string VotedSectionsJson;
            public string PlayerColorsJson;
            public string OnlinePlayerIdsJson;
        }

        [Serializable]
        internal class BroadcastUpdatePayload
        {
            public string SectionJson;
            public bool IsDelete;
        }
    }

    public class NotesSyncRequest : ModNetworkMessage<NotesSyncRequest>
    {
        public ulong SenderSteamId;

        public NotesSyncRequest() { }

        public override void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteUInt64(SenderSteamId);
        }

        public override void Deserialize(RocketBinaryReader reader)
        {
            SenderSteamId = reader.ReadUInt64();
        }

        public override void Process(long hostId)
        {
            BeefsRecipesPlugin.Log.LogInfo($"NotesSyncRequest.Process - SenderSteamId={SenderSteamId}");

            BeefsRecipesPlugin.Instance?.EnsureServerNoteManager();

            BeefsRecipesPlugin.Instance?.ServerNoteManager?
                .HandleFullSyncRequest(SenderSteamId);
        }
    }

    public class NotesSectionDelete : ModNetworkMessage<NotesSectionDelete>
    {
        public ulong SenderSteamId;
        public string SectionId;

        public NotesSectionDelete() { }

        public override void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteUInt64(SenderSteamId);
            writer.WriteString(SectionId ?? "");
        }

        public override void Deserialize(RocketBinaryReader reader)
        {
            SenderSteamId = reader.ReadUInt64();
            SectionId = reader.ReadString();
        }

        public override void Process(long hostId)
        {
            BeefsRecipesPlugin.Log.LogInfo($"NotesSectionDelete.Process - SenderSteamId={SenderSteamId}, sectionId={SectionId}");

            BeefsRecipesPlugin.Instance?.ServerNoteManager?
                .HandleDeleteSection(SenderSteamId, SectionId);
        }
    }

    public class NotesVoteRemove : ModNetworkMessage<NotesVoteRemove>
    {
        public ulong SenderSteamId;
        public string SectionId;

        public NotesVoteRemove() { }

        public override void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteUInt64(SenderSteamId);
            writer.WriteString(SectionId ?? "");
        }

        public override void Deserialize(RocketBinaryReader reader)
        {
            SenderSteamId = reader.ReadUInt64();
            SectionId = reader.ReadString();
        }

        public override void Process(long hostId)
        {
            BeefsRecipesPlugin.Log.LogInfo($"NotesVoteRemove.Process - SenderSteamId={SenderSteamId}, sectionId={SectionId}");

            BeefsRecipesPlugin.Instance?.ServerNoteManager?
                .HandleVoteRemove(SenderSteamId, SectionId);
        }
    }

    public class NotesVoteRedact : ModNetworkMessage<NotesVoteRedact>
    {
        public ulong SenderSteamId;
        public string SectionId;

        public NotesVoteRedact() { }

        public override void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteUInt64(SenderSteamId);
            writer.WriteString(SectionId ?? "");
        }

        public override void Deserialize(RocketBinaryReader reader)
        {
            SenderSteamId = reader.ReadUInt64();
            SectionId = reader.ReadString();
        }

        public override void Process(long hostId)
        {
            BeefsRecipesPlugin.Log.LogInfo($"NotesVoteRedact.Process - SenderSteamId={SenderSteamId}, sectionId={SectionId}");

            BeefsRecipesPlugin.Instance?.ServerNoteManager?
                .HandleRedactVote(SenderSteamId, SectionId);
        }
    }

    public class NotesPlayerColor : ModNetworkMessage<NotesPlayerColor>
    {
        public ulong SenderSteamId;
        public string ColorHex;

        public NotesPlayerColor() { }

        public override void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteUInt64(SenderSteamId);
            writer.WriteString(ColorHex ?? "");
        }

        public override void Deserialize(RocketBinaryReader reader)
        {
            SenderSteamId = reader.ReadUInt64();
            ColorHex = reader.ReadString();
        }

        public override void Process(long hostId)
        {
            BeefsRecipesPlugin.Log.LogInfo($"NotesPlayerColor.Process - SenderSteamId={SenderSteamId}, color={ColorHex}");

            BeefsRecipesPlugin.Instance?.ServerNoteManager?
                .HandlePlayerColor(SenderSteamId, ColorHex);
        }
    }

    public class NotesVoteUpdate : ModNetworkMessage<NotesVoteUpdate>
    {
        public string SectionId;
        public int VoteCount;

        public NotesVoteUpdate() { }

        public override void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteString(SectionId ?? "");
            writer.WriteInt32(VoteCount);
        }

        public override void Deserialize(RocketBinaryReader reader)
        {
            SectionId = reader.ReadString();
            VoteCount = reader.ReadInt32();
        }

        public override void Process(long hostId)
        {
            BeefsRecipesPlugin.Log.LogInfo($"NotesVoteUpdate.Process - sectionId={SectionId}, voteCount={VoteCount}");

            BeefsRecipesPlugin.Instance?.ClientSyncManager?
                .HandleVoteUpdate(SectionId, VoteCount);
        }
    }

    public class NotesPlayerColorBroadcast : ModNetworkMessage<NotesPlayerColorBroadcast>
    {
        public ulong ClientId;
        public string ColorHex;

        public NotesPlayerColorBroadcast() { }

        public override void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteUInt64(ClientId);
            writer.WriteString(ColorHex ?? "");
        }

        public override void Deserialize(RocketBinaryReader reader)
        {
            ClientId = reader.ReadUInt64();
            ColorHex = reader.ReadString();
        }

        public override void Process(long hostId)
        {
            BeefsRecipesPlugin.Log.LogInfo($"NotesPlayerColorBroadcast.Process - clientId={ClientId}, color={ColorHex}");

            BeefsRecipesPlugin.Instance?.ClientSyncManager?
                .HandlePlayerColorUpdate(ClientId, ColorHex);
        }
    }

    public class NotesPlayerPresence : ModNetworkMessage<NotesPlayerPresence>
    {
        public ulong ClientId;
        public bool IsOnline;

        public NotesPlayerPresence() { }

        public override void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteUInt64(ClientId);
            writer.WriteBoolean(IsOnline);
        }

        public override void Deserialize(RocketBinaryReader reader)
        {
            ClientId = reader.ReadUInt64();
            IsOnline = reader.ReadBoolean();
        }

        public override void Process(long hostId)
        {
            BeefsRecipesPlugin.Log.LogInfo($"NotesPlayerPresence.Process - clientId={ClientId}, isOnline={IsOnline}");

            BeefsRecipesPlugin.Instance?.ClientSyncManager?
                .HandlePlayerPresence(ClientId, IsOnline);
        }
    }

    public class NotesChunkedPayload : ModNetworkMessage<NotesChunkedPayload>
    {
        public ushort TransferId;
        public ushort ChunkIndex;
        public ushort TotalChunks;
        public byte Tag;
        public ulong SenderSteamId;
        public string Payload;

        public NotesChunkedPayload() { }

        public override void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteUInt16(TransferId);
            writer.WriteUInt16(ChunkIndex);
            writer.WriteUInt16(TotalChunks);
            writer.WriteByte(Tag);
            writer.WriteUInt64(SenderSteamId);
            writer.WriteString(Payload ?? "");
        }

        public override void Deserialize(RocketBinaryReader reader)
        {
            TransferId = reader.ReadUInt16();
            ChunkIndex = reader.ReadUInt16();
            TotalChunks = reader.ReadUInt16();
            Tag = reader.ReadByte();
            SenderSteamId = reader.ReadUInt64();
            Payload = reader.ReadString();
        }

        public override void Process(long hostId)
        {
            BeefsRecipesPlugin.Log.LogInfo(
                $"NotesChunkedPayload.Process - transfer={TransferId}, " +
                $"chunk {ChunkIndex + 1}/{TotalChunks}, tag={Tag}, " +
                $"sender={SenderSteamId}, len={Payload?.Length ?? 0}");

            RecipesNetworkMessages.HandleChunk(this);
        }
    }
}