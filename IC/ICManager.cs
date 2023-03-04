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
using Mono.Cecil;
using RandomizerMod.Menu;
using Modding;
using ItemChanger.FsmStateActions;
using RandomizerMod.Logging;

namespace BreakableWallRandomiser.IC
{
    public class ICManager
    {
        #pragma warning disable 0649
        // This field is assigned to by JSON deserialization
        public class WallData
        {
            public class RelativeMapLocation
            {
                public string sceneName;
                public float x;
                public float y;

                [Newtonsoft.Json.JsonIgnore]
                public (string, float, float) repr => (sceneName, x, y);
            }

            Regex rgx = new Regex("[^a-zA-Z0-9]");
            Regex rgx_with_spaces = new Regex("[^a-zA-Z0-9 ]");

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
            public List<RelativeMapLocation> mapLocations;
            public List<string> alsoDestroy;

            public string cleanGameObjectPath() => rgx.Replace(gameObject, "");
            public string cleanSceneName() => rgx.Replace(sceneName, "");
            public string getLocationName() => niceName != "" ? rgx_with_spaces.Replace(niceName, "") : $"Loc_Wall_{cleanSceneName()}_{cleanGameObjectPath()}";
            public string getItemName() => niceName != "" ? rgx_with_spaces.Replace(niceName, "") : $"Itm_Wall_{cleanSceneName()}_{cleanGameObjectPath()}";
            public string getTermName() => $"BREAKABLE_{cleanSceneName()}_{cleanGameObjectPath()}";
            public string getGroupName() => WALL_GROUPS[fsmType].Item1;
            public bool shouldBeIncluded(GenerationSettings gs)
            {
                if (!BreakableWallRandomiser.settings.AnyWalls && !BreakableWallRandomiser.settings.RandomizeDiveFloors) { return false; }

                if ((fsmType == "FSM" || fsmType == "breakable_wall_v2") && !BreakableWallRandomiser.settings.RandomizeBreakableRockWalls) { return false; }
                if (fsmType == "break_floor" && !BreakableWallRandomiser.settings.RandomizeBreakableWoodenPlankWalls) { return false; }
                if ((fsmType == "quake_floor" || fsmType == "Detect Quake") && !BreakableWallRandomiser.settings.RandomizeDiveFloors) { return false; }

                if (requiredSetting == null) { return true; }

                if (requiredSetting == "Rando_WP") {
                    return gs.LongLocationSettings.WhitePalaceRando != RandomizerMod.Settings.LongLocationSettings.WPSetting.ExcludeWhitePalace;
                }

                // TODO: Reflection here is probably a bad idea.
                var prop = BreakableWallRandomiser.settings.GetType().GetField(requiredSetting);
                
                if (prop == null) { 
                    Modding.Logger.LogWarn($"[Wall Rando] Unknown settings property referenced: {requiredSetting}"); 
                    return true; 
                }

                return (bool)prop.GetValue(BreakableWallRandomiser.settings);
            }
        }
        #pragma warning restore 0649

        // Map: FSM Name -> Group Name
        public readonly static Dictionary<string, (string, Func<int>, Func<bool>)> WALL_GROUPS = new() { 
            { "break_floor", ("Breakable Planks Walls", () => BreakableWallRandomiser.settings.WoodenPlankWallGroup, () => BreakableWallRandomiser.settings.RandomizeBreakableWoodenPlankWalls )},
            { "FSM", ("Breakable Planks Walls", () => BreakableWallRandomiser.settings.WoodenPlankWallGroup, () => BreakableWallRandomiser.settings.RandomizeBreakableWoodenPlankWalls )},

            { "breakable_wall_v2", ("Breakable Rock Walls", () => BreakableWallRandomiser.settings.RockWallGroup, () => BreakableWallRandomiser.settings.RandomizeBreakableRockWalls )},

            { "quake_floor", ("Desolate Dive Floors", () => BreakableWallRandomiser.settings.DiveFloorGroup, () => BreakableWallRandomiser.settings.RandomizeDiveFloors )},
            { "Detect Quake", ("Desolate Dive Floors", () => BreakableWallRandomiser.settings.DiveFloorGroup, () => BreakableWallRandomiser.settings.RandomizeDiveFloors )},
        };

        private static Dictionary<string, ItemGroupBuilder> definedGroups = new();

        public readonly static List<WallData> wallData = JsonConvert.DeserializeObject<List<WallData>>(
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
                    nonreplaceable = true,
                    tags = new() {
                        InteropTagFactory.CmiLocationTag(
                            poolGroup: wall.getGroupName(),
                            pinSprite: new WallSprite(wall.sprite),
                            sceneNames: new List<string> { wall.sceneName },
                            mapLocations: wall.mapLocations.Select(x => x.repr).ToArray()
                        ),
                    }
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
                    },
                    tags = new() {
                        InteropTagFactory.CmiSharedTag(poolGroup: wall.getGroupName())
                    }
                };

                // Modding.Logger.LogDebug(wall.getLocationName() + " -> term: " + wall.getTermName() + " / itm: " + wall.getItemName());

                Finder.DefineCustomLocation(wallLocation); 
                Finder.DefineCustomItem(wallItem);
            }
        }

        public void Hook()
        {
            RCData.RuntimeLogicOverride.Subscribe(15f, ApplyLogic);

            RequestBuilder.OnUpdate.Subscribe(0.3f, AddWalls);

            RandomizerMenuAPI.OnGenerateStartLocationDict += RandomizerMenuAPI_OnGenerateStartLocationDict;

            SettingsLog.AfterLogSettings += LogWallRandoSettings;
        }

        private static void LogWallRandoSettings(LogArguments args, System.IO.TextWriter tw)
        {
            tw.WriteLine("Logging Wall Rando settings:");
            using Newtonsoft.Json.JsonTextWriter jtw = new(tw) { CloseOutput = false, };
            RandomizerMod.RandomizerData.JsonUtil._js.Serialize(jtw, BreakableWallRandomiser.settings);
            tw.WriteLine();
        }

        private void RandomizerMenuAPI_OnGenerateStartLocationDict(Dictionary<string, RandomizerMod.RandomizerData.StartDef> startDefs)
        {
            try
            {
                if (!BreakableWallRandomiser.settings.AnyWalls) { return; }

                (string westBlueLakeName, RandomizerMod.RandomizerData.StartDef westBlueLakeStart)
                        = startDefs.First(pair => pair.Value.SceneName == SceneNames.Crossroads_50);

                startDefs[westBlueLakeName] = westBlueLakeStart with
                {
                    Logic = "FALSE",
                    RandoLogic = "FALSE"
                };

                (string coloStartName, RandomizerMod.RandomizerData.StartDef coloStart)
                        = startDefs.First(pair => pair.Value.SceneName == SceneNames.Deepnest_East_09);

                startDefs[coloStartName] = coloStart with
                {
                    Logic = $"({coloStart.Logic}) + OBSCURESKIPS + ENEMYPOGOS + PRECISEMOVEMENT",
                    RandoLogic = "FALSE" // Only about 5 checks are reachable even *with* the above settings.
                };
            } catch (InvalidOperationException)
            {
                Modding.Logger.LogWarn("[Breakable Walls] Couldn't patch start locations.");
            }
        }

        private void AddWalls(RequestBuilder rb)
        {
            foreach (var wall in wallData)
            {
                if (!wall.shouldBeIncluded(rb.gs)) { continue; }

                rb.EditItemRequest(wall.getItemName(), info =>
                {
                    info.getItemDef = () => new()
                    {
                        Name = wall.getItemName(),
                        Pool = wall.getGroupName(),
                        MajorItem = false,
                        PriceCap = 150
                    };
                });

                rb.EditLocationRequest(wall.getLocationName(), info =>
                {
                    info.getLocationDef = () => new()
                    {
                        Name = wall.getLocationName(),
                        SceneName = wall.sceneName,
                        FlexibleCount = false,
                        AdditionalProgressionPenalty = false
                    };
                });
            }

            // Create groups
            definedGroups.Clear();

            foreach (var group in WALL_GROUPS)
            {
                if (group.Value.Item2() > 0 && group.Value.Item3())
                {
                    ItemGroupBuilder wallGroup = null;
                    string label = RBConsts.SplitGroupPrefix + group.Value.Item2();

                    foreach (ItemGroupBuilder igb in rb.EnumerateItemGroups())
                    {
                        if (igb.label == label)
                        {
                            wallGroup = igb;
                            break;
                        }
                    }

                    wallGroup ??= rb.MainItemStage.AddItemGroup(label);

                    definedGroups[group.Key] = wallGroup;
                }
            }

            // Hook Groups
            rb.OnGetGroupFor.Subscribe(0.01f, ResolveWallGroup);

            bool ResolveWallGroup(RequestBuilder rb, string item, RequestBuilder.ElementType type, out GroupBuilder gb)
            {
                var wall = wallData.Find(x => x.getItemName() == item || x.getLocationName() == item);

                if (wall != null)
                {
                    if (definedGroups.ContainsKey(wall.fsmType))
                    {
                        gb = definedGroups[wall.fsmType];
                        return true;
                    }
                }

                gb = default;
                return false;
            }

            // Add to randomization request
            foreach (var wall in wallData)
            {
                if (wall.shouldBeIncluded(rb.gs)) {
                    rb.AddItemByName(wall.getItemName());
                    rb.AddLocationByName(wall.getLocationName());
                } else if (BreakableWallRandomiser.settings.AnyWalls)
                {
                    rb.AddToVanilla(new(wall.getItemName(), wall.getLocationName()));
                }
            } 
        }

        private void ApplyLogic(GenerationSettings gs, LogicManagerBuilder lmb)
        {
            foreach (var wall in wallData)
            {
                // Always define terms, unless all options are off.
                if (!BreakableWallRandomiser.settings.AnyWalls) { continue; }

                Term wallTerm = lmb.GetOrAddTerm(wall.getTermName());
                lmb.AddItem(new SingleItem(wall.getItemName(), new TermValue(wallTerm, 1)));

                lmb.AddLogicDef(new(wall.getLocationName(), wall.logic));

                if (!wall.shouldBeIncluded(gs)) { continue; }

                // Add to logic. Walls which aren't included shouldn't affect existing logic.
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
    }
}
