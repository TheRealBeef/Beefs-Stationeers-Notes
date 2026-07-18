using System;
using System.Collections.Generic;
using Assets.Scripts;
using Assets.Scripts.Networking;
using LaunchPadBooster;
using LaunchPadBooster.Networking;
using Newtonsoft.Json;

namespace BeefsRecipes
{
    public static class RecipesNetworkMessages
    {
        public static readonly Mod MOD = new(PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION);

        public static void Initialize()
        {
            var _mod = MOD;

            _mod.Networking.RegisterMessage<NotesSyncRequest>();
            _mod.Networking.RegisterMessage<NotesSyncResponse>();
            _mod.Networking.RegisterMessage<NotesSectionPush>();
            _mod.Networking.RegisterMessage<NotesSectionDelete>();
            _mod.Networking.RegisterMessage<NotesVoteRemove>();
            _mod.Networking.RegisterMessage<NotesVoteRedact>();
            _mod.Networking.RegisterMessage<NotesVoteUpdate>();
            _mod.Networking.RegisterMessage<NotesPlayerColor>();
            _mod.Networking.RegisterMessage<NotesPlayerColorBroadcast>();
            _mod.Networking.RegisterMessage<NotesPlayerPresence>();
            _mod.Networking.RegisterMessage<NotesBroadcastUpdate>();

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
    }

    public class NotesSyncRequest : INetworkMessage
    {
        public ulong SenderSteamId;

        public void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteUInt64(SenderSteamId);
        }

        public void Deserialize(RocketBinaryReader reader)
        {
            SenderSteamId = reader.ReadUInt64();
        }

        public void Process(long clientId)
        {
            BeefsRecipesPlugin.Log.LogInfo($"NotesSyncRequest.Process - SenderSteamId={SenderSteamId}");

            BeefsRecipesPlugin.Instance?.EnsureServerNoteManager();

            BeefsRecipesPlugin.Instance?.ServerNoteManager?
                .HandleFullSyncRequest(SenderSteamId);
        }
    }

    public class NotesSyncResponse : INetworkMessage
    {
        public string SessionKey;
        public string SectionsJson;
        public string VotedSectionsJson;
        public string PlayerColorsJson;
        public string OnlinePlayerIdsJson;

        public void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteString(SessionKey ?? "");
            writer.WriteString(SectionsJson ?? "");
            writer.WriteString(VotedSectionsJson ?? "");
            writer.WriteString(PlayerColorsJson ?? "");
            writer.WriteString(OnlinePlayerIdsJson ?? "");
        }

        public void Deserialize(RocketBinaryReader reader)
        {
            SessionKey = reader.ReadString();
            SectionsJson = reader.ReadString();
            VotedSectionsJson = reader.ReadString();
            PlayerColorsJson = reader.ReadString();
            OnlinePlayerIdsJson = reader.ReadString();
        }

        public void Process(long clientId)
        {
            BeefsRecipesPlugin.Log.LogInfo(
                $"NotesSyncResponse.Process - session={SessionKey?.Substring(0, 8)}...");

            try
            {
                var sections = JsonConvert.DeserializeObject<List<BeefsRecipesPlugin.RecipeSection>>(
                    SectionsJson) ?? new List<BeefsRecipesPlugin.RecipeSection>();
                var votedSections = JsonConvert.DeserializeObject<List<string>>(
                    VotedSectionsJson) ?? new List<string>();
                var playerColors = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                    PlayerColorsJson) ?? new Dictionary<string, string>();
                var onlinePlayerIds = JsonConvert.DeserializeObject<List<ulong>>(
                    OnlinePlayerIdsJson) ?? new List<ulong>();

                BeefsRecipesPlugin.Instance?.ClientSyncManager?
                    .HandleFullSyncResponse(SessionKey, sections, votedSections,
                        playerColors, onlinePlayerIds);
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Error processing sync response: {ex.Message}");
            }
        }
    }

    public class NotesSectionPush : INetworkMessage
    {
        public ulong SenderSteamId;
        public string SectionJson;

        public void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteUInt64(SenderSteamId);
            writer.WriteString(SectionJson ?? "");
        }

        public void Deserialize(RocketBinaryReader reader)
        {
            SenderSteamId = reader.ReadUInt64();
            SectionJson = reader.ReadString();
        }

        public void Process(long clientId)
        {
            BeefsRecipesPlugin.Log.LogInfo(
                $"NotesSectionPush.Process - SenderSteamId={SenderSteamId}");

            try
            {
                var section = JsonConvert.DeserializeObject<BeefsRecipesPlugin.RecipeSection>(SectionJson);
                if (section == null) return;

                BeefsRecipesPlugin.Instance?.ServerNoteManager?
                    .HandleSectionUpdate(SenderSteamId, section);
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Error processing section push: {ex.Message}");
            }
        }
    }

    public class NotesSectionDelete : INetworkMessage
    {
        public ulong SenderSteamId;
        public string SectionId;

        public void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteUInt64(SenderSteamId);
            writer.WriteString(SectionId ?? "");
        }

        public void Deserialize(RocketBinaryReader reader)
        {
            SenderSteamId = reader.ReadUInt64();
            SectionId = reader.ReadString();
        }

        public void Process(long clientId)
        {
            BeefsRecipesPlugin.Log.LogInfo(
                $"NotesSectionDelete.Process - SenderSteamId={SenderSteamId}, sectionId={SectionId}");

            BeefsRecipesPlugin.Instance?.ServerNoteManager?
                .HandleDeleteSection(SenderSteamId, SectionId);
        }
    }

    public class NotesVoteRemove : INetworkMessage
    {
        public ulong SenderSteamId;
        public string SectionId;

        public void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteUInt64(SenderSteamId);
            writer.WriteString(SectionId ?? "");
        }

        public void Deserialize(RocketBinaryReader reader)
        {
            SenderSteamId = reader.ReadUInt64();
            SectionId = reader.ReadString();
        }

        public void Process(long clientId)
        {
            BeefsRecipesPlugin.Log.LogInfo(
                $"NotesVoteRemove.Process - SenderSteamId={SenderSteamId}, sectionId={SectionId}");

            BeefsRecipesPlugin.Instance?.ServerNoteManager?
                .HandleVoteRemove(SenderSteamId, SectionId);
        }
    }

    public class NotesVoteRedact : INetworkMessage
    {
        public ulong SenderSteamId;
        public string SectionId;

        public void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteUInt64(SenderSteamId);
            writer.WriteString(SectionId ?? "");
        }

        public void Deserialize(RocketBinaryReader reader)
        {
            SenderSteamId = reader.ReadUInt64();
            SectionId = reader.ReadString();
        }

        public void Process(long clientId)
        {
            BeefsRecipesPlugin.Log.LogInfo(
                $"NotesVoteRedact.Process - SenderSteamId={SenderSteamId}, sectionId={SectionId}");

            BeefsRecipesPlugin.Instance?.ServerNoteManager?
                .HandleRedactVote(SenderSteamId, SectionId);
        }
    }

    public class NotesVoteUpdate : INetworkMessage
    {
        public string SectionId;
        public int VoteCount;

        public void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteString(SectionId ?? "");
            writer.WriteInt32(VoteCount);
        }

        public void Deserialize(RocketBinaryReader reader)
        {
            SectionId = reader.ReadString();
            VoteCount = reader.ReadInt32();
        }

        public void Process(long clientId)
        {
            BeefsRecipesPlugin.Log.LogInfo(
                $"NotesVoteUpdate.Process - sectionId={SectionId}, voteCount={VoteCount}");

            BeefsRecipesPlugin.Instance?.ClientSyncManager?
                .HandleVoteUpdate(SectionId, VoteCount);
        }
    }

    public class NotesPlayerColor : INetworkMessage
    {
        public ulong SenderSteamId;
        public string ColorHex;

        public void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteUInt64(SenderSteamId);
            writer.WriteString(ColorHex ?? "");
        }

        public void Deserialize(RocketBinaryReader reader)
        {
            SenderSteamId = reader.ReadUInt64();
            ColorHex = reader.ReadString();
        }

        public void Process(long clientId)
        {
            BeefsRecipesPlugin.Log.LogInfo(
                $"NotesPlayerColor.Process - SenderSteamId={SenderSteamId}, color={ColorHex}");

            BeefsRecipesPlugin.Instance?.ServerNoteManager?
                .HandlePlayerColor(SenderSteamId, ColorHex);
        }
    }

    public class NotesPlayerColorBroadcast : INetworkMessage
    {
        public ulong ClientId;
        public string ColorHex;

        public void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteUInt64(ClientId);
            writer.WriteString(ColorHex ?? "");
        }

        public void Deserialize(RocketBinaryReader reader)
        {
            ClientId = reader.ReadUInt64();
            ColorHex = reader.ReadString();
        }

        public void Process(long clientId)
        {
            BeefsRecipesPlugin.Log.LogInfo(
                $"NotesPlayerColorBroadcast.Process - clientId={ClientId}, color={ColorHex}");

            BeefsRecipesPlugin.Instance?.ClientSyncManager?
                .HandlePlayerColorUpdate(ClientId, ColorHex);
        }
    }

    public class NotesPlayerPresence : INetworkMessage
    {
        public ulong ClientId;
        public bool IsOnline;

        public void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteUInt64(ClientId);
            writer.WriteBoolean(IsOnline);
        }

        public void Deserialize(RocketBinaryReader reader)
        {
            ClientId = reader.ReadUInt64();
            IsOnline = reader.ReadBoolean();
        }

        public void Process(long clientId)
        {
            BeefsRecipesPlugin.Log.LogInfo(
                $"NotesPlayerPresence.Process - clientId={ClientId}, isOnline={IsOnline}");

            BeefsRecipesPlugin.Instance?.ClientSyncManager?
                .HandlePlayerPresence(ClientId, IsOnline);
        }
    }

    public class NotesBroadcastUpdate : INetworkMessage
    {
        public string SectionJson;
        public bool IsDelete;

        public void Serialize(RocketBinaryWriter writer)
        {
            writer.WriteString(SectionJson ?? "");
            writer.WriteBoolean(IsDelete);
        }

        public void Deserialize(RocketBinaryReader reader)
        {
            SectionJson = reader.ReadString();
            IsDelete = reader.ReadBoolean();
        }

        public void Process(long clientId)
        {
            BeefsRecipesPlugin.Log.LogInfo(
                $"NotesBroadcastUpdate.Process - isDelete={IsDelete}");

            try
            {
                var section = JsonConvert.DeserializeObject<BeefsRecipesPlugin.RecipeSection>(SectionJson);
                if (section == null) return;

                BeefsRecipesPlugin.Instance?.ClientSyncManager?
                    .HandleBroadcastUpdate(section, IsDelete);
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Error processing broadcast update: {ex.Message}");
            }
        }
    }
}