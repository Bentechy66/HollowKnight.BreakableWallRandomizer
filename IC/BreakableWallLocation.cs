using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ItemChanger.Locations;
using ItemChanger;
using Satchel;
using ItemChanger.Util;
using static ItemChanger.Internal.SpriteManager;
using UnityEngine;
using HutongGames.PlayMaker.Actions;

namespace BreakableWallRandomiser.IC
{
    public class BreakableWallLocation : ExistingContainerLocation
    {
        public string objectName;
        public string fsmType;
        public ICManager.WallData wallData;

        protected override void OnLoad()
        {
            if (!wallData.shouldBeIncluded()) { return; } // bit of a bodge

            Events.AddFsmEdit(sceneName, new(objectName, fsmType), ModifyWallBehaviour);
        }

        protected override void OnUnload()
        {
            if (!wallData.shouldBeIncluded()) { return; }

            Events.RemoveFsmEdit(sceneName, new(objectName, fsmType), ModifyWallBehaviour);
        }

        private void ModifyWallBehaviour(PlayMakerFSM fsm)
        {
            if (fsmType == "break_floor")
            {
                // Make sure the wall doesn't delete itself because playerdata is set
                fsm.ChangeTransition("Initiate", "ACTIVATE", "Idle");

                if (BreakableWallRandomiser.saveData.unlockedBreakableWalls.Contains(wallData.getTermName()))
                {
                    // Delete the door, regardless of player state. Spawn a shiny with any uncollected items.
                    fsm.ChangeTransition("Initiate", "FINISHED", "Activated");
                    fsm.ChangeTransition("Initiate", "ACTIVATE", "Activated");

                    foreach (var item in Placement.Items.FindAll(x => !x.IsObtained()))
                    {
                        GameObject shiny = ShinyUtility.MakeNewShiny(Placement, item, FlingType.StraightUp);
                        shiny.transform.SetPosition2D(fsm.transform.position.x, fsm.transform.position.y);
                        ShinyUtility.FlingShinyRandomly(shiny.LocateMyFSM("Shiny Control"));
                        shiny.SetActive(true);
                    }
                } else
                {
                    // Door should still be in place.
                    
                    fsm.AddState("GiveItem");
                    fsm.AddCustomAction("GiveItem", () => {
                        ItemUtility.GiveSequentially(Placement.Items, Placement, new GiveInfo()
                        {
                            FlingType = FlingType.Everywhere,
                            MessageType = MessageType.Corner,
                        });

                        Placement.AddVisitFlag(VisitState.Opened);
                    });

                    // If we already obtained the item at this location, set the wall to an unhittable state:
                    if (Placement.Items.All(x => x.IsObtained()))
                    {
                        fsm.SetState("GiveItem");
                    } else
                    {
                        // Copy sound and particles from original
                        foreach (var action in fsm.GetState("Break").Actions)
                        {
                            if (action is AudioPlayerOneShotSingle or PlayParticleEmitter)
                            {
                                fsm.AddAction("GiveItem", action);
                            }
                        }

                        // In case we're in the same scene when it breaks, spawn the items
                        fsm.AddCustomAction("Break", () => {
                            foreach (var item in Placement.Items.FindAll(x => !x.IsObtained()))
                            {
                                GameObject shiny = ShinyUtility.MakeNewShiny(Placement, item, FlingType.StraightUp);
                                shiny.transform.SetPosition2D(fsm.transform.position.x, fsm.transform.position.y);
                                ShinyUtility.FlingShinyRandomly(shiny.LocateMyFSM("Shiny Control"));
                                shiny.SetActive(true);
                            }

                            Placement.AddVisitFlag(VisitState.Opened);
                        });
                    }

                    fsm.ChangeTransition("Hit", "HIT 3", "GiveItem");
                }
            }
            else if (fsmType == "breakable_wall_v2" || fsmType == "FSM")
            {
                // Make sure the wall doesn't delete itself because playerdata is set
                if (fsmType == "breakable_wall_v2") { fsm.ChangeTransition("Activated?", "ACTIVATE", "Ruin Lift?"); }
                if (fsmType == "FSM") { fsm.ChangeTransition("Initiate", "ACTIVATE", "Idle"); }

                if (BreakableWallRandomiser.saveData.unlockedBreakableWalls.Contains(wallData.getTermName()))
                {
                    // Delete the door, regardless of player state. Spawn a shiny with any uncollected items.
                    fsm.ChangeTransition("Initiate", "FINISHED", "Activated");
                    fsm.ChangeTransition("Initiate", "ACTIVATE", "Activated");

                    foreach (var item in Placement.Items.FindAll(x => !x.IsObtained()))
                    {
                        GameObject shiny = ShinyUtility.MakeNewShiny(Placement, item, FlingType.StraightUp);
                        shiny.transform.SetPosition2D(fsm.transform.position.x, fsm.transform.position.y);
                        ShinyUtility.FlingShinyRandomly(shiny.LocateMyFSM("Shiny Control"));
                        shiny.SetActive(true);
                    }
                }
                else
                {
                    // The door should still be in place.

                    fsm.AddState("GiveItem");
                    fsm.AddCustomAction("GiveItem", () => {
                        ItemUtility.GiveSequentially(Placement.Items, Placement, new GiveInfo()
                        {
                            FlingType = FlingType.Everywhere,
                            MessageType = MessageType.Corner,
                        });
                        Placement.AddVisitFlag(VisitState.Opened);
                    });

                    // If we already obtained the item at this location, set the wall to an unhittable state:
                    if (Placement.Items.All(x => x.IsObtained()))
                    {
                        fsm.SetState("GiveItem");
                    } else
                    {
                        // Copy sound and particles from original
                        foreach (var action in fsm.GetState("Break").Actions)
                        {
                            if (action is AudioPlayerOneShotSingle or PlayParticleEmitter)
                            {
                                fsm.AddAction("GiveItem", action);
                            }
                        }

                        // In case we're in the same scene when it breaks, spawn the items
                        fsm.AddCustomAction("Break", () => {
                            foreach (var item in Placement.Items.FindAll(x => !x.IsObtained()))
                            {
                                GameObject shiny = ShinyUtility.MakeNewShiny(Placement, item, FlingType.StraightUp);
                                shiny.transform.SetPosition2D(fsm.transform.position.x, fsm.transform.position.y);
                                ShinyUtility.FlingShinyRandomly(shiny.LocateMyFSM("Shiny Control"));
                                shiny.SetActive(true);
                            }
                            Placement.AddVisitFlag(VisitState.Opened);
                        });
                    }

                    if (fsmType == "breakable_wall_v2") { fsm.ChangeTransition("PD Bool?", "FINISHED", "GiveItem"); }
                    if (fsmType == "FSM") { 
                        fsm.ChangeTransition("Pause Frame", "FINISHED", "GiveItem");
                        fsm.ChangeTransition("Spell Destroy", "FINISHED", "GiveItem");
                    }
                }
            }
        }
    }
}
