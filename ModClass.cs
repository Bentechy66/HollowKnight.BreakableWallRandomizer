using System;
using System.Collections;
using System.Collections.Generic;
using Modding;
using UnityEngine;
using UObject = UnityEngine.Object;
using BreakableWallRandomiser.IC;
using BreakableWallRandomiser.Rando;

namespace BreakableWallRandomiser
{
    public class SaveData
    {
        public List<string> unlockedBreakableWalls = new List<string>();
    }

    public class BreakableWallRandomiser : Mod, ILocalSettings<SaveData>, IGlobalSettings<WallRandoSettings>
    {
        public static SaveData saveData = new SaveData();
        public static WallRandoSettings settings = new WallRandoSettings();

        new public string GetName() => "Breakable Wall Randomizer";
        public override string GetVersion() => "v1.0";
        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            Log("Initializing...");

            ICManager manager = new()
            {
                // For now, at least, use this for every wall in the game.
                uiSprite = preloadedObjects["Crossroads_10"]["Breakable Wall"].GetComponent<SpriteRenderer>().sprite
            };

            RandoMenuPage.Hook();

            manager.RegisterItemsAndLocations();
            if (settings.RandomizeWalls) { manager.Hook(); }

            // On.GameManager.BeginSceneTransition += LogTransName;

            Log("Initialized.");
        }

        private void LogTransName(On.GameManager.orig_BeginSceneTransition orig, GameManager self, GameManager.SceneLoadInfo info)
        {
            Log(info.SceneName + "[" + info.EntryGateName + "]");

            orig(self, info);
        }

        public void OnLoadLocal(SaveData s)
        {
            saveData = s;
        }

        public override List<(string, string)> GetPreloadNames()
        {
            // Used for shop textures.
            return new List<(string, string)> {("Crossroads_10", "Breakable Wall") };
        }

        public SaveData OnSaveLocal()
        {
            return saveData;
        }

        public void OnLoadGlobal(WallRandoSettings s)
        {
            settings = s;
        }

        public WallRandoSettings OnSaveGlobal()
        {
            return settings;
        }
    }
}