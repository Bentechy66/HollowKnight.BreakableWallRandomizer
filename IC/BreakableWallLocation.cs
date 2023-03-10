using System.Linq;
using ItemChanger.Locations;
using ItemChanger;
using Satchel;
using ItemChanger.Util;
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
            Events.AddFsmEdit(sceneName, new(objectName, fsmType), ModifyWallBehaviour);
        }

        protected override void OnUnload()
        {
            Events.RemoveFsmEdit(sceneName, new(objectName, fsmType), ModifyWallBehaviour);
        }

        private void MakeWallPassable(GameObject go)
        {
            if (wallData.alsoDestroy != null)
            {
                foreach (var objectName in wallData.alsoDestroy)
                {
                    try
                    {
                        var obj = GameObject.Find(objectName);
                        GameObject.Destroy(obj);
                    } catch { }
                }
            }
            Recursive_MakeWallPassable(go);
        }

        // Recursively set all colliders as triggers on a given gameObject.
        // Also recursively set any SpriteRenderers on a given gameObject to 0.5 alpha.
        // Also remove any object called "Camera lock" or any textures beginning with msk_. 
        private void Recursive_MakeWallPassable(GameObject go)
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
                if (fsmType == "Detect Quake" || fsmType == "quake_floor")
                {
                    tmp.a = 0.4f;
                } else
                {
                    tmp.a = 0.5f;
                }
                sprite.color = tmp;

                if (sprite.sprite && sprite.sprite.name.StartsWith("msk"))
                {
                    sprite.enabled = false;
                }
            }

            if (go.name.Contains("Camera"))
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
            }
            else if (fsmType == "breakable_wall_v2")
            {
                fsm.ChangeTransition("Activated?", "ACTIVATE", "Ruin Lift?");
            } else if (fsmType == "quake_floor")
            {
                fsm.ChangeTransition("Init", "ACTIVATE", "Solid");
                fsm.RemoveAction("Transient", 0); // Sets the floor to a trigger
                if (fsm.GetState("Solid").GetActions<SetBoxColliderTrigger>().Length >= 1)
                {
                    fsm.RemoveAction("Solid", 0); // Sets the floor to a triggern't
                }

                var collider = fsm.gameObject.GetComponent<BoxCollider2D>();
                collider.isTrigger = true; // Make the first collider always a trigger

                // Add our own collider for physics collision.
                var newCollider = fsm.gameObject.AddComponent<BoxCollider2D>();
                newCollider.offset = collider.offset;
                newCollider.size = collider.size;
            } else if (fsmType == "Detect Quake")
            {
                fsm.ChangeTransition("Init", "ACTIVATE", "Detect");
            }

            fsm.AddState("GiveItem");
            fsm.AddCustomAction("GiveItem", () =>
            {
                ItemUtility.GiveSequentially(Placement.Items, Placement, new GiveInfo()
                {
                    FlingType = FlingType.Everywhere,
                    MessageType = MessageType.Corner,
                });

                Placement.AddVisitFlag(VisitState.Opened);

                if (BreakableWallRandomiser.saveData.unlockedBreakableWalls.Contains(wallData.getTermName()))
                {
                    // Delete the wall entirely.
                    if (fsmType == "quake_floor") { fsm.SetState("Destroy"); }
                    else if (fsmType == "Detect Quake") { fsm.SetState("Break 2"); }
                    else { fsm.SetState("Break"); }
                }
            });

            // If we already unlocked this wall, and items are still left there, make it passable.
            if (BreakableWallRandomiser.saveData.unlockedBreakableWalls.Contains(wallData.getTermName()))
            {
                // If items are left, make wall semi-transparent and passable
                if (Placement.Items.Any(x => !x.IsObtained()))
                {
                    MakeWallPassable(fsm.gameObject);
                }
                else
                {
                    // Ensure the wall deletes on-load.
                    if (fsmType == "quake_floor")
                    {
                        fsm.ChangeTransition("Init", "FINISHED", "Activate");
                        fsm.ChangeTransition("Init", "ACTIVATE", "Activate");
                    } else if (fsmType == "Detect Quake") {
                        fsm.ChangeTransition("Init", "ACTIVATE", "Activate !!!");
                        fsm.ChangeTransition("Init", "FINISHED", "Activate !!!");
                    } else
                    {
                        fsm.ChangeTransition("Initiate", "FINISHED", "Activated");
                        fsm.ChangeTransition("Initiate", "ACTIVATE", "Activated");
                    }
                }
            }
            else
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
                    var originalIdleStateName = fsmType switch
                    {
                        "quake_floor" => "Solid",

                        "Detect Quake" => "Detect",

                        _ => "Idle"
                    };

                    // Copy sound and particles from original
                    var originalBreakStateName = fsmType switch
                    {
                        "quake_floor" => "Glass",

                        "Detect Quake" => "Break 2",

                        _ => "Break"
                    };

                    foreach (var action in fsm.GetState(originalBreakStateName).Actions)
                    {
                        if (action is AudioPlayerOneShotSingle or PlayParticleEmitter or AudioPlayerOneShot)
                        {
                            fsm.AddAction("GiveItem", action);
                        }
                    }

                    // In case we're in the same scene when it breaks, check if there are items left,
                    // and then set states accordingly

                    fsm.AddState("BreakSameScene");

                    fsm.InsertCustomAction("BreakSameScene", () =>
                    {
                        if (Placement.Items.Any(x => !x.IsObtained()))
                        {
                            MakeWallPassable(fsm.gameObject);
                            fsm.SetState(originalIdleStateName);
                        }
                        else
                        {
                            if (fsmType == "quake_floor") { MakeWallPassable(fsm.gameObject); } // ensure everything is passable.
                            fsm.SetState(originalBreakStateName);
                        }

                        Placement.AddVisitFlag(VisitState.Opened);
                    }, 0);
                }
            }

            if (fsmType == "breakable_wall_v2")
            {
                fsm.ChangeTransition("PD Bool?", "FINISHED", "GiveItem");
            }
            else if (fsmType == "FSM")
            {
                fsm.ChangeTransition("Pause Frame", "FINISHED", "GiveItem");
                fsm.ChangeTransition("Spell Destroy", "FINISHED", "GiveItem");
            }
            else if (fsmType == "break_floor")
            {
                fsm.ChangeTransition("Hit", "HIT 3", "GiveItem");
            } else if (fsmType == "quake_floor")
            {
                fsm.ChangeTransition("PD Bool?", "FINISHED", "GiveItem");
            } else if (fsmType == "Detect Quake")
            {
                fsm.ChangeTransition("Quake Hit", "FINISHED", "GiveItem");
            }
        }
    }
}
