using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ItemChanger;
using Newtonsoft.Json;
using RandomizerCore.Logic;
using RandomizerCore.LogicItems;
using RandomizerMod.RC;
using RandomizerMod.Settings;
using UnityEngine.SceneManagement;
using RandomizerCore;
using System.Text.RegularExpressions;
using ItemChanger.UIDefs;

namespace BreakableWallRandomiser.IC
{
    public class ICManager
    {
        #pragma warning disable 0649
        // This field is assigned to by JSON deserialization
        public class WallData
        {
            Regex rgx = new Regex("[^a-zA-Z0-9]");
            public string gameObject;
            public string fsmType;
            public Dictionary<string, string> logicOverrides;
            public Dictionary<string, Dictionary<string, string>> logicSubstitutions;
            public string sceneName;
            public string niceName;
            public string logic; // The logic required to actually reach _and obtain_ the item at this wall.
            public string persistentBool;
            public string requiredSetting;
            public string sprite;

            public string cleanGameObjectPath() => rgx.Replace(gameObject, "");
            public string cleanSceneName() => rgx.Replace(sceneName, "");
            public string getLocationName() => niceName != "" ? "Wall_" + rgx.Replace(niceName, "") : $"Loc_Wall_{cleanSceneName()}_{cleanGameObjectPath()}";
            public string getItemName() => $"Itm_Wall_{cleanSceneName()}_{cleanGameObjectPath()}";
            public string getTermName() => $"BREAKABLE_{cleanSceneName()}_{cleanGameObjectPath()}";
            public bool shouldBeIncluded()
            {
                if (!BreakableWallRandomiser.settings.RandomizeWalls) { return false; }
                if (requiredSetting == null) { return true; }

                // TODO: Reflection here is probably a bad idea.
                var prop = BreakableWallRandomiser.settings.GetType().GetField(requiredSetting);
                
                if (prop == null) { 
                    Modding.Logger.LogWarn($"Unknown settings property referenced: {requiredSetting}"); 
                    return true; 
                }

                return (bool)prop.GetValue(BreakableWallRandomiser.settings);
            }
        }
        #pragma warning restore 0649

        readonly List<WallData> wallData = JsonConvert.DeserializeObject<List<WallData>>(
            System.Text.Encoding.UTF8.GetString(Properties.Resources.BreakableWallData)
        );

        readonly string[] wallShopDescriptions =
        {
            "What am I supposed to put in this description? It's a wall.",
            "Truly one of the walls of all time.",
            "Quite possibly my most favouritest wall in the game.",
            "Quite possibly my least favouritest wall in the game.",
            "Call in Bob the Builder. He won't fix it, he'll break it.",
            "Donate to Menderbug so that he can have a day off and break a wall instead of fixing one.",
            "This is probably just another useless shortcut wall. Still...",
            "Did you know there are exactly 100 breakable walls in this game? If you round a bit?",
            "Hot Loading Screen Tip: White Palace Breakable Walls and some others aren't randomized.",
            "Writing shop descriptions for these things is kinda hard.",
            "Vague and non-specific description somehow tangentially related to walls goes here.",
            "I bet you don't even know where this one is, do you?",
            "These wall names aren't very descriptive, are they?"
        };

        public void RegisterItemsAndLocations()
        {
            Random random = new Random(0x1337);

            // UnityEngine.Sprite scaledSprite = UnityEngine.Sprite.Create(uiSprite.texture, uiSprite.rect, new UnityEngine.Vector2(0.5f, 0.5f), 100);

            foreach (var wall in wallData)
            {
                BreakableWallLocation wallLocation = new()
                {
                    objectName = wall.gameObject,
                    fsmType = wall.fsmType,
                    name = wall.getLocationName(),
                    sceneName = wall.sceneName,
                    wallData = wall,
                    nonreplaceable = true
                };

                BreakableWallItem wallItem = new()
                {
                    objectName = wall.gameObject,
                    sceneName = wall.sceneName,
                    name = wall.getItemName(),
                    wallData = wall,
                    UIDef = new MsgUIDef
                    {
                        name = new BoxedString(wall.niceName != "" ? wall.niceName : wall.getItemName()),
                        shopDesc = new BoxedString("\n" + wallShopDescriptions[random.Next(0, wallShopDescriptions.Length)]),
                        sprite = new WallSprite(wall.sprite)
                    }
                };

                // Modding.Logger.LogDebug(wall.getLocationName() + " -> term: " + wall.getTermName() + " / itm: " + wall.getItemName());

                Finder.DefineCustomLocation(wallLocation); 
                Finder.DefineCustomItem(wallItem);
            }
        }

        public void Hook()
        {
            RCData.RuntimeLogicOverride.Subscribe(999, ApplyLogic);
            RequestBuilder.OnUpdate.Subscribe(0.3f, AddWalls);

            On.UIManager.StartNewGame += UIManager_StartNewGame;
        }

        private void AddWalls(RequestBuilder rb)
        {
            Modding.Logger.Log("Adding walls.");

            if (BreakableWallRandomiser.settings.WallGroup > 0)
            {
                Modding.Logger.Log(BreakableWallRandomiser.settings.WallGroup);

                ItemGroupBuilder wallGroup = null;
                string label = RBConsts.SplitGroupPrefix + BreakableWallRandomiser.settings.WallGroup;

                foreach (ItemGroupBuilder igb in rb.EnumerateItemGroups())
                {
                    if (igb.label == label)
                    {
                        wallGroup = igb;
                        break;
                    }
                }

                wallGroup ??= rb.MainItemStage.AddItemGroup(label);

                rb.OnGetGroupFor.Subscribe(0.01f, ResolveWallGroup);

                bool ResolveWallGroup(RequestBuilder rb, string item, RequestBuilder.ElementType type, out GroupBuilder gb)
                {
                    if (wallData.Any(x => x.getItemName() == item || x.getLocationName() == item))
                    {
                        gb = wallGroup;
                        return true;
                    }

                    gb = default;
                    return false;
                }
            }

            foreach (var wall in wallData)
            {
                if (!wall.shouldBeIncluded()) { continue; }

                rb.AddItemByName(wall.getItemName());
                rb.AddLocationByName(wall.getLocationName());
            }
        }

        private void ApplyLogic(GenerationSettings gs, LogicManagerBuilder lmb)
        {
            foreach (var wall in wallData)
            {
                if (!wall.shouldBeIncluded()) { continue; }

                Term wallTerm = lmb.GetOrAddTerm(wall.getTermName());
                lmb.AddItem(new SingleItem(wall.getItemName(), new TermValue(wallTerm, 1)));

                lmb.AddLogicDef(new(wall.getLocationName(), wall.logic));

                foreach (var logicOverride in wall.logicOverrides)
                {
                    lmb.DoLogicEdit(new(logicOverride.Key, logicOverride.Value));
                }

                foreach (var substitutionDef in wall.logicSubstitutions)
                { 
                    foreach (var substitution in substitutionDef.Value)
                    {
                        lmb.DoSubst(new(substitutionDef.Key, substitution.Key, substitution.Value));  
                    }
                }
            }
        }

        private void UIManager_StartNewGame(On.UIManager.orig_StartNewGame orig, UIManager self, bool permaDeath, bool bossRush)
        {
            orig(self, permaDeath, bossRush);

            ItemChangerMod.CreateSettingsProfile(false);

            foreach (var wall in wallData)
            {
                AbstractPlacement placement = Finder.GetLocation(wall.getLocationName()).Wrap();
                ItemChangerMod.AddPlacements(new List<AbstractPlacement>() { placement });
            }
        }
    }
}
