using System;
using System.Collections.Generic;
using System.IO;
using Assets.Scripts;
using Assets.Scripts.Objects;
using BepInEx;
using Newtonsoft.Json;

namespace BeefsRecipes
{
    public static class BeefsRecipesSaveManager
    {
        [Serializable]
        public class RecipesData
        {
            public string worldName;
            public string worldId;
            public string saveFileName;
            public List<BeefsRecipesPlugin.RecipeSection> sections;
            public string lastModified;
            public string gameVersion;
            public int notesVersion;
            public int fontSizeOffset;
            public float panelHeight;
            public string panelMode;
            public float panelYOffset;
            public float scrollPosition;
        }

        [Serializable]
        public class PersonalNotesData
        {
            public string sessionKey;
            public List<BeefsRecipesPlugin.RecipeSection> sections;
            public string lastModified;
            public int fontSizeOffset;
            public float panelHeight;
            public string panelMode;
            public float panelYOffset;
            public List<string> hiddenSectionIds;
            public float scrollPosition;
        }

        private const string NotesFolder = "notes";
        private const string NotesSuffix = "_notes.json";
        private const string PersonalNotesFileName = "personal_notes.json";
        private const string ConfigSubFolder = "BeefsRecipes";

        public static RecipesData CreateDefaultRecipesData()
        {
            return new RecipesData
            {
                sections = new List<BeefsRecipesPlugin.RecipeSection>(),
                fontSizeOffset = 0,
                panelHeight = 600f,
                panelMode = "Hidden"
            };
        }

        public static PersonalNotesData CreateDefaultPersonalData()
        {
            return new PersonalNotesData
            {
                sections = new List<BeefsRecipesPlugin.RecipeSection>(),
                fontSizeOffset = 0,
                panelHeight = 600f,
                panelMode = "Hidden",
                hiddenSectionIds = new List<string>()
            };
        }

        public static string GetNotesPath(string worldName, string saveId)
        {
            if (string.IsNullOrEmpty(worldName))
            {
                throw new ArgumentException("World name null", nameof(worldName));
            }

            if (string.IsNullOrEmpty(saveId))
            {
                saveId = worldName;
            }

            var worldDir = StationSaveUtils.GetWorldSaveDirectory(worldName);
            var notesDir = Path.Combine(worldDir.FullName, NotesFolder);

            if (!Directory.Exists(notesDir))
            {
                Directory.CreateDirectory(notesDir);
            }

            return Path.Combine(notesDir, $"{saveId}{NotesSuffix}");
        }

        public static string GetPersonalNotesPath(string sessionKey)
        {
            if (string.IsNullOrEmpty(sessionKey))
            {
                throw new ArgumentException("Session key null", nameof(sessionKey));
            }

            string dir = Path.Combine(Paths.ConfigPath, ConfigSubFolder, NotesFolder, sessionKey);

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            return Path.Combine(dir, PersonalNotesFileName);
        }

        public static void SaveNotes(
            string worldName,
            string saveId,
            List<BeefsRecipesPlugin.RecipeSection> sections,
            int fontSizeOffset,
            float panelHeight,
            float panelYOffset,
            string panelMode,
            float scrollPosition)
        {
            try
            {
                string filePath = GetNotesPath(worldName, saveId);

                RecipesData data = new RecipesData
                {
                    worldName = worldName,
                    worldId = World.CurrentId,
                    saveFileName = saveId,
                    sections = sections ?? new List<BeefsRecipesPlugin.RecipeSection>(),
                    lastModified = DateTime.UtcNow.ToString("o"),
                    gameVersion = GameManager.GetGameVersion(),
                    fontSizeOffset = fontSizeOffset,
                    panelHeight = panelHeight,
                    panelMode = panelMode,
                    panelYOffset = panelYOffset,
                    scrollPosition = scrollPosition,
                    notesVersion = 1
                };

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(filePath, json);

                BeefsRecipesPlugin.Log.LogInfo($"Notes saved: {saveId}");
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Failed to save notes: {ex.Message}");
                throw;
            }
        }

        public static List<BeefsRecipesPlugin.RecipeSection> LoadNotes(string worldName, string saveId)
        {
            return LoadNotesData(worldName, saveId).sections;
        }

        public static RecipesData LoadNotesData(string worldName, string saveId)
        {
            try
            {
                string filePath = GetNotesPath(worldName, saveId);

                if (!File.Exists(filePath))
                {
                    return CreateDefaultRecipesData();
                }

                string json = File.ReadAllText(filePath);
                RecipesData data = JsonConvert.DeserializeObject<RecipesData>(json);

                if (data == null || data.sections == null)
                {
                    return CreateDefaultRecipesData();
                }

                foreach (var section in data.sections)
                {
                    if (string.IsNullOrEmpty(section.id))
                    {
                        section.id = Guid.NewGuid().ToString();
                    }
                }

                if (data.panelHeight == 0f)
                {
                    data.panelHeight = 600f;
                }
                if (string.IsNullOrEmpty(data.panelMode))
                {
                    data.panelMode = "Hidden";
                }

                BeefsRecipesPlugin.Log.LogInfo($"Notes loaded: {saveId}");
                return data;
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Failed to load notes: {ex.Message}");
                return CreateDefaultRecipesData();
            }
        }

        public static void DeleteNotes(string worldName, string saveName)
        {
            try
            {
                string filePath = GetNotesPath(worldName, saveName);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Failed to delete notes: {ex.Message}");
                throw;
            }
        }

        public static void SavePersonalNotes(
            string sessionKey,
            List<BeefsRecipesPlugin.RecipeSection> sections,
            int fontSizeOffset,
            float panelHeight,
            float panelYOffset,
            string panelMode,
            List<string> hiddenSectionIds,
            float scrollPosition)
        {
            try
            {
                string filePath = GetPersonalNotesPath(sessionKey);

                PersonalNotesData data = new PersonalNotesData
                {
                    sessionKey = sessionKey,
                    sections = sections ?? new List<BeefsRecipesPlugin.RecipeSection>(),
                    lastModified = DateTime.UtcNow.ToString("o"),
                    fontSizeOffset = fontSizeOffset,
                    panelHeight = panelHeight,
                    panelMode = panelMode,
                    panelYOffset = panelYOffset,
                    hiddenSectionIds = hiddenSectionIds ?? new List<string>(),
                    scrollPosition = scrollPosition
                };

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(filePath, json);

                BeefsRecipesPlugin.Log.LogInfo($"Personal notes saved for session: {sessionKey.Substring(0, 8)}...");
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Failed to save personal notes: {ex.Message}");
            }
        }

        public static PersonalNotesData LoadPersonalNotesData(string sessionKey)
        {
            try
            {
                string filePath = GetPersonalNotesPath(sessionKey);

                if (!File.Exists(filePath))
                {
                    return CreateDefaultPersonalData();
                }

                string json = File.ReadAllText(filePath);
                PersonalNotesData data = JsonConvert.DeserializeObject<PersonalNotesData>(json);

                if (data == null || data.sections == null)
                {
                    return CreateDefaultPersonalData();
                }

                foreach (var section in data.sections)
                {
                    if (string.IsNullOrEmpty(section.id))
                    {
                        section.id = Guid.NewGuid().ToString();
                    }
                }

                if (data.panelHeight == 0f)
                {
                    data.panelHeight = 600f;
                }
                if (string.IsNullOrEmpty(data.panelMode))
                {
                    data.panelMode = "Hidden";
                }
                if (data.hiddenSectionIds == null)
                {
                    data.hiddenSectionIds = new List<string>();
                }

                BeefsRecipesPlugin.Log.LogInfo($"Personal notes loaded for session: {sessionKey.Substring(0, 8)}...");
                return data;
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Failed to load personal notes: {ex.Message}");
                return CreateDefaultPersonalData();
            }
        }
    }
}