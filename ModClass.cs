using System;
using System.Collections;
using System.Collections.Generic;
using Modding;
using UnityEngine;
using UObject = UnityEngine.Object;
using BreakableWallRandomiser.IC;

namespace BreakableWallRandomiser
{
    public class SaveData
    {
        public List<string> unlockedBreakableWalls = new List<string>();
    }

    public class BreakableWallRandomiser : Mod, ILocalSettings<SaveData>
    {
        public static SaveData saveData = new SaveData();

        new public string GetName() => "Rope Randomizer";
        public override string GetVersion() => "v1.0";
        public override void Initialize()
        {
            Log("Initializing...");

            ICManager manager = new();

            manager.RegisterItemsAndLocations();
            manager.Hook();

            On.GameManager.BeginSceneTransition += LogTransName;

            Log("Initialized.");
        }

        private void LogTransName(On.GameManager.orig_BeginSceneTransition orig, GameManager self, GameManager.SceneLoadInfo info)
        {
            // Log(info.SceneName + "[" + info.EntryGateName + "]");

            orig(self, info);
        }

        public void OnLoadLocal(SaveData s)
        {
            saveData = s;
        }

        public SaveData OnSaveLocal()
        {
            return saveData;
        }
    }
}