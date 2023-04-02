using System;
using System.Collections.Generic;
using System.Linq;
using ItemChanger;
using Newtonsoft.Json;
using RandomizerCore.Logic;
using RandomizerCore.LogicItems;
using RandomizerMod.RC;
using RandomizerMod.Settings;
using RandomizerCore;
using System.Text.RegularExpressions;
using ItemChanger.UIDefs;
using RandomizerMod.Menu;
using Modding;
using RandomizerMod.Logging;
using MoreDoors.Rando;
using UnityEngine;
using MonoMod.Utils;
using Mono.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace BreakableWallRandomiser.IC
{
    public class ICManager
    {
#pragma warning disable 0649
        // This field is assigned to by JSON deserialization
        [Serializable]
        public class WallData
        {
            [Serializable]
            public class RelativeMapLocation
            {
                public string sceneName;
                public float x;
                public float y;

                [Newtonsoft.Json.JsonIgnore]
                public (string, float, float) repr => (sceneName, x, y);
            }

            [Serializable]
            public class LightWallDef
            {
                public string sceneName;
                public string gameObject;
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
            public List<string> destroyUnconditionally;
            public List<string> overrideTermNames;
            public string group;

            public List<WallData> secondaryWalls;

            public string cleanGameObjectPath() => rgx.Replace(gameObject, "");
            public string cleanSceneName() => rgx.Replace(sceneName, "");
            public string getLocationName() => niceName != "" ? rgx_with_spaces.Replace(niceName, "") : $"Loc_Wall_{cleanSceneName()}_{cleanGameObjectPath()}";
            public string getItemName() => niceName != "" ? rgx_with_spaces.Replace(niceName, "") : $"Itm_Wall_{cleanSceneName()}_{cleanGameObjectPath()}";
            public List<string> getTermNames() => overrideTermNames ?? new List<string> {$"BREAKABLE_{cleanSceneName()}_{cleanGameObjectPath()}"};
            public string getGroupName() => WALL_GROUPS[fsmType].Item1;

            private bool excludeWallDueToMoreDoors(GenerationSettings gs)
            {
                // If the PoP door is randomized, don't include our wall. Recipe for disaster, trust me.

                // TODO: This is a horrible hack. Let's figure out a nicer way to do this in future. 
                MoreDoors.Rando.LocalSettings LS = new();
                System.Random r = new(gs.Seed + 13);
                LS.EnabledDoorNames = LS.Settings.ComputeActiveDoors(gs, r);

                return LS.IncludeDoor("Pain");
            }

            public bool shouldBeIncluded(GenerationSettings gs)
            {
                if (!BreakableWallRandomiser.settings.AnyWalls && !BreakableWallRandomiser.settings.RandomizeDiveFloors) { return false; }

                // -- Patches for other mods --

                // MoreDoors PoP door
                if (sceneName == "White_Palace_06" && gameObject == "/Breakable Wall Ruin Lift")
                {
                    if (ModHooks.GetMod("MoreDoors") is Mod)
                    {
                        if (excludeWallDueToMoreDoors(gs)) { return false; }
                    }
                }

                // -- End Patches --

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

        public readonly static System.Collections.ObjectModel.ReadOnlyCollection<WallData> rawWallData = JsonConvert.DeserializeObject<List<WallData>>(
            System.Text.Encoding.UTF8.GetString(Properties.Resources.BreakableWallData)
        ).AsReadOnly();

        readonly string[] wallShopDescriptions =
        {
            "What am I supposed to put in this description? It's a wall.",
            "Truly one of the walls of all time.",
            "Quite possibly my most favouritest wall in the game.",
            "Quite possibly my least favouritest wall in the game.",
            "Call in Bob the Builder. He won't fix it, he'll break it.",
            "Donate to Menderbug so that he can have a day off and break a wall instead of fixing one.",
            "This is probably just another useless shortcut wall. Still...",
            "Fun fact: this mod adds exactly 100 breakable wall checks, and even more dive floor checks!",
            "There's only one breakable wall in the game which isn't randomized. Do you know which one?",
            "Writing shop descriptions for these things is kinda hard.",
            "Vague and non-specific description somehow tangentially related to walls goes here.",
            "I bet you don't even know where this one is, do you?",
            "These wall descriptions aren't very descriptive, are they?",
            "How am I going to break this wall without leaving your sight? I have my ways...",
            "This wall is begging to be shattered. Do it for the thrill.",
            "Behind this wall lies a mystery waiting to be uncovered. Unless you use a map mod. Cheat.",
            "I'm pretty sure the wall won't see this one coming.",
            "This wall was asking for it. I just answered the call.",
            "Rumour has it that breaking this wall will bring good luck. Worth a shot, right?",
            "This wall is a roadblock on the path to victory. It's time to remove it.",
            "Maybe, if you run really fast at this wall, it'll just let you through instead?",
            "Hey kid, wanna buy some cracks?",
            "This one's definitely the one you've been looking for. Trust me, I checked.",
            "All craftsmanship is of the lowest quality.",
            "Menderbug has been trying to get his hands on this one for years!",


            "I'll cast some Bad Magic to break this wall for ya -- for a fee.",
            "Bring in a Sock Mower to mow down this wall.",
            "What even *is* a Sockmower?",
            "FlibberZERO this wall.",
            "You Onrywon't be seeing this wall any more after you purchase this product.",

            "Hot Loading Screen Tip: Walls which you've unlocked, but haven't checked, will be transparent. You can walk through them!",
            "Hot Loading Screen Tip: It can be kinda hard to tell if a wall is transparent. You can also completely just not see them! You should double check!",
            "Hot Loading Screen Tip: Breakable Walls in the white palace follow the WP Rando setting.",
        };

        readonly string[] diveFloorDescriptions =
        {
            "Real Estate has really suffered in recent years. Clear out this piece of quality land!",
            "This one's just someone's driveway.",
            "We need to go deeper!",
            "How do we break these? We just give Oro a sledgehammer.",
            "So... how much soul did you waste before you remembered this mod was on?",
            "We'll send someone around to Desolate Dive harder than Bitcoin prices into this wall.",
            "Fun fact: this mod adds exactly 46 dive floor checks, and even more breakable wall checks!",
            "Vague and non-specific description somehow tangentially related to floors goes here.",

            "I'll cast some Bad Magic to break this floor for ya -- for a fee.",
            "We'll drop a Sock Mower onto this floor; that aughtta break it.",
            
            "Hot Loading Screen Tip: Floors which you've unlocked, but haven't checked, will be transparent. You can fall through them!",
            "Hot Loading Screen Tip: It can be kinda hard to tell if a floor is transparent. You can also completely just not see them! You should double check!",
            "Hot Loading Screen Tip: Dive Floors in the white palace follow the WP Rando setting.",
        };

        public void RegisterItemsAndLocations()
        {
            System.Random random = new(0x1337);

            // UnityEngine.Sprite scaledSprite = UnityEngine.Sprite.Create(uiSprite.texture, uiSprite.rect, new UnityEngine.Vector2(0.5f, 0.5f), 100);

            // ew.
            var wallData = getSerializedWallData(true);
            wallData.AddRange(getSerializedWallData(false));

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
                        shopDesc = new BoxedString("\n" + 
                            (wall.getGroupName() == "Desolate Dive Floors" ? 
                            diveFloorDescriptions[random.Next(0, diveFloorDescriptions.Length)] :
                            wallShopDescriptions[random.Next(0, wallShopDescriptions.Length)])
                        ),
                        sprite = new WallSprite(wall.sprite)
                    },
                    tags = new() {
                        InteropTagFactory.CmiSharedTag(poolGroup: wall.getGroupName())
                    }
                };

                // Modding.Logger.LogDebug(wall.getLocationName() + " -> term: " + wall.getTermName() + " / itm: " + wall.getItemName());

                if (Finder.GetLocation(wallLocation.name) == null) { Finder.DefineCustomLocation(wallLocation); };
                if (Finder.GetItem(wallItem.name) == null) { Finder.DefineCustomItem(wallItem); }
            }
        }

        public static T DeepClone<T>(T obj)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                ms.Position = 0;

                return (T)formatter.Deserialize(ms);
            }
        }

        public List<WallData> getSerializedWallData(bool groupNearbyWalls)
        {
            if (!groupNearbyWalls) { return rawWallData.ToList(); }

            // Add all the walls which are grouped together to a single Location / Item pairing.
            List<WallData> wallData = new List<WallData>();
            List<string> addedGroups = new List<string>();

            var rawWallDataList = DeepClone(rawWallData.ToList()); // mmmmmm non-explicit references!
            foreach (var wall in rawWallDataList)
            {
                
                if (wall.group == null) { wallData.Add(wall); continue; }
                if (addedGroups.Contains(wall.group)) { continue; }

                wall.overrideTermNames ??= wall.getTermNames();
                wall.niceName = wall.group;

                foreach (var secondaryWall in rawWallDataList.FindAll(i => i.group == wall.group))
                {
                    if (wall.getTermNames() == secondaryWall.getTermNames()) { continue;  }

                    wall.overrideTermNames.AddRange(secondaryWall.getTermNames());

                    wall.secondaryWalls.Add(secondaryWall);

                    wall.logicOverrides.AddRange(secondaryWall.logicOverrides);
                    wall.logicSubstitutions.AddRange(secondaryWall.logicSubstitutions); 
                    if (secondaryWall.alsoDestroy != null) {
                        wall.alsoDestroy ??= new List<string>();
                        wall.alsoDestroy.AddRange(secondaryWall.alsoDestroy); 
                    }
                    if (secondaryWall.destroyUnconditionally != null) {
                        wall.destroyUnconditionally ??= new List<string>(); 
                        wall.destroyUnconditionally.AddRange(secondaryWall.destroyUnconditionally); 
                    }
                }

                wall.logic = "(" + String.Join(") | (", rawWallDataList.FindAll(i => i.group == wall.group).Select(i => i.logic)) + ")";

                addedGroups.Add(wall.group);
                wallData.Add(wall);
            }

            return wallData;
        }

        public void Hook()
        {
            RegisterItemsAndLocations();

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
            var wallData = getSerializedWallData(BreakableWallRandomiser.settings.GroupTogetherNearbyWalls);

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
            var wallData = getSerializedWallData(BreakableWallRandomiser.settings.GroupTogetherNearbyWalls);

            foreach (var wall in wallData)
            {
                // Always define terms, unless all options are off.
                if (!BreakableWallRandomiser.settings.AnyWalls) { continue; }

                var wallTerms = wall.getTermNames().Select(x => new TermValue(lmb.GetOrAddTerm(x), 1));

                lmb.AddItem(new MultiItem(wall.getItemName(), wallTerms.ToArray()));

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
