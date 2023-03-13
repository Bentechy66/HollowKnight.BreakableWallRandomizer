using System.Collections.Generic;
using Modding;
using BreakableWallRandomiser.IC;
using BreakableWallRandomiser.Rando;
using RandoSettingsManager;
using RandoSettingsManager.SettingsManagement;

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
        public override string GetVersion() => "2.0.2.3"; 
        public override void Initialize()
        {
            Log("Initializing...");

            ICManager manager = new();

            RandoMenuPage.Hook();

            if (ModHooks.GetMod("RandoSettingsManager") is Mod)
            {
                HookRSM();
            }

            manager.RegisterItemsAndLocations();
            manager.Hook();

            // On.GameManager.BeginSceneTransition += LogTransName;

            Log("Initialized.");
        }

        private void HookRSM()
        {
            RandoSettingsManagerMod.Instance.RegisterConnection(new SimpleSettingsProxy<WallRandoSettings>(
                this,
                (settings) => RandoMenuPage.Instance.PasteSettings(settings),
                () => settings.AnyWalls ? settings : null
            ));
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
