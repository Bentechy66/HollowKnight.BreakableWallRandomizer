using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ItemChanger;
using UnityEngine;

namespace BreakableWallRandomiser.IC
{
    public class BreakableWallItem : AbstractItem
    {
        public string sceneName;
        public string objectName;
        public ICManager.WallData wallData;

        public override void GiveImmediate(GiveInfo info)
        {
            // Set data in the save to indicate we got the wall
            BreakableWallRandomiser.saveData.unlockedBreakableWalls.Add(wallData.getTermName());
            if (wallData.persistentBool != "") { PlayerData.instance.SetBool(wallData.persistentBool, true); }

            // If we're already in the same scene as the wall, break it. The wall's FSM should spawn a shiny.
            if (GameManager.instance.sceneName == sceneName)
            {
                // Conveniently, both of the FSM types use the state "Break" to indicate the wall breaking.
                GameObject.Find(objectName).LocateMyFSM(wallData.fsmType).SetState("Break");
            }
        }
    }
}
