using System;
using System.Collections.Generic;
using System.IO;
using Assets.Scripts;
using Assets.Scripts.Objects;
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
        }

        private const string NotesFolder = "notes";
        private const string NotesSuffix = "_notes.json";

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

        public static void SaveNotes(
            string worldName,
            string saveId,
            List<BeefsRecipesPlugin.RecipeSection> sections,
            int fontSizeOffset,
            float panelHeight,
            float panelYOffset,
            string panelMode)
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
                    return new RecipesData
                    {
                        sections = new List<BeefsRecipesPlugin.RecipeSection>(),
                        fontSizeOffset = 0,
                        panelHeight = 600f,
                        panelMode = "Hidden"
                    };
                }

                string json = File.ReadAllText(filePath);
                RecipesData data = JsonConvert.DeserializeObject<RecipesData>(json);

                if (data == null || data.sections == null)
                {
                    return new RecipesData
                    {
                        sections = new List<BeefsRecipesPlugin.RecipeSection>(),
                        fontSizeOffset = 0,
                        panelHeight = 600f,
                        panelMode = "Hidden"
                    };
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
                return new RecipesData
                {
                    sections = new List<BeefsRecipesPlugin.RecipeSection>(),
                    fontSizeOffset = 0,
                    panelHeight = 600f,
                    panelMode = "Hidden"
                };
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
    }
}