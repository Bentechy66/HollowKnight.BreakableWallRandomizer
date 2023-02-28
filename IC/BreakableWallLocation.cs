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

        // Recursively set all colliders as triggers on a given gameObject.
        // Also recursively set any SpriteRenderers on a given gameObject to 0.5 alpha.
        // Also remove any object called "Camera lock" or any textures beginning with msk_. 
        private void MakeWallPassable(GameObject go)
        {
            foreach (var collider in go.GetComponents<Collider2D>())
            {
                // Triggers can still be hit by a nail, but won't impede player movement.
                collider.isTrigger = true;
            }

            // Make sprites transparent
            foreach (var sprite in go.GetComponents<SpriteRenderer>())
            {
                Color tmp = sprite.color;
                tmp.a = 0.7f;
                sprite.color = tmp;

                if (sprite.sprite && sprite.sprite.name.StartsWith("msk"))
                {
                    sprite.enabled = false;
                }
            }

            if (go.name == "Camera Locks")
            {
                UnityEngine.GameObject.Destroy(go);
            }

            for (var i = 0; i < go.transform.childCount; i++)
            {
                MakeWallPassable(go.transform.GetChild(i).gameObject);
            }
        }

        private void ModifyWallBehaviour(PlayMakerFSM fsm)
        {
            // The wall will delete itself based on its state if we don't do this.
            if (fsmType == "break_floor" || fsmType == "FSM")
            {
                fsm.ChangeTransition("Initiate", "ACTIVATE", "Idle");
            } else if (fsmType == "breakable_wall_v2")
            {
                fsm.ChangeTransition("Activated?", "ACTIVATE", "Ruin Lift?");
            }

            fsm.AddState("GiveItem");
            fsm.AddCustomAction("GiveItem", () => {
                ItemUtility.GiveSequentially(Placement.Items, Placement, new GiveInfo()
                {
                    FlingType = FlingType.Everywhere,
                    MessageType = MessageType.Corner,
                });

                Placement.AddVisitFlag(VisitState.Opened);

                if (BreakableWallRandomiser.saveData.unlockedBreakableWalls.Contains(wallData.getTermName()))
                {
                    // Delete the wall entirely.
                    fsm.SetState("Break");
                }
            });

            // If we already unlocked this wall, and items are still left there, make it passable.
            if (BreakableWallRandomiser.saveData.unlockedBreakableWalls.Contains(wallData.getTermName()))
            {
                // If items are left, make wall semi-transparent and passable
                if (Placement.Items.Any(x => !x.IsObtained()))
                {
                    MakeWallPassable(fsm.gameObject);
                } else
                {
                    // Ensure the wall deletes on-load.
                    fsm.ChangeTransition("Initiate", "FINISHED", "Activated");
                    fsm.ChangeTransition("Initiate", "ACTIVATE", "Activated");
                }
            } else
            // If we didn't unlock this door yet...
            {
                // ...and we already obtained the item at this location, set the wall to an unhittable state:
                if (Placement.Items.All(x => x.IsObtained()))
                {
                    fsm.SetState("GiveItem");
                }
                // ...and there are items left to collect:
                else
                {
                    // Copy sound and particles from original
                    foreach (var action in fsm.GetState("Break").Actions)
                    {
                        if (action is AudioPlayerOneShotSingle or PlayParticleEmitter)
                        {
                            fsm.AddAction("GiveItem", action);
                        }
                    }

                    // In case we're in the same scene when it breaks, check if there are items left,
                    // and then set states accordingly

                    fsm.AddState("BreakSameScene");

                    fsm.InsertCustomAction("BreakSameScene", () => {
                        if (Placement.Items.Any(x => !x.IsObtained()))
                        {
                            MakeWallPassable(fsm.gameObject);
                            fsm.SetState("Idle");
                        } else
                        {
                            fsm.SetState("Break");
                        }

                        Placement.AddVisitFlag(VisitState.Opened);
                    }, 0);
                }
            }

            if (fsmType == "breakable_wall_v2") { 
                fsm.ChangeTransition("PD Bool?", "FINISHED", "GiveItem"); 
            } else if (fsmType == "FSM")
            {
                fsm.ChangeTransition("Pause Frame", "FINISHED", "GiveItem");
                fsm.ChangeTransition("Spell Destroy", "FINISHED", "GiveItem");
            } else if (fsmType == "break_floor")
            {
                fsm.ChangeTransition("Hit", "HIT 3", "GiveItem");
            } 
        }
    }
}
