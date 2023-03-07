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
                GameObject.Find(objectName).LocateMyFSM(wallData.fsmType).SetState("BreakSameScene");
            }
        }
    }
}
