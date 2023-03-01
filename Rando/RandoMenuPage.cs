using MenuChanger;
using MenuChanger.MenuElements;
using MenuChanger.MenuPanels;
using MenuChanger.Extensions;
using RandomizerMod.Menu;
using static RandomizerMod.Localization;
using UnityEngine;

namespace BreakableWallRandomiser.Rando
{
    public class WallRandoSettings
    {
        public bool RandomizeBreakableWoodenPlankWalls = true;
        public bool RandomizeBreakableRockWalls = true;
        
        public bool RandomizeTutorialBreakableFloor = false;

        [MenuChanger.Attributes.MenuRange(-1, 99)]
        public int WoodenPlankWallGroup = -1;
        public int RockWallGroup = -1;

        [Newtonsoft.Json.JsonIgnore]
        public bool Any => RandomizeBreakableRockWalls || RandomizeBreakableWoodenPlankWalls;
    }

    public class RandoMenuPage
    {
        internal MenuPage WallRandoPage;
        internal MenuElementFactory<WallRandoSettings> wallMEF;
        internal VerticalItemPanel wallVIP;

        internal SmallButton OpenWallRandoSettings;

        internal static RandoMenuPage Instance { get; private set; }

        public static void OnExitMenu()
        {
            Instance = null; 
        }

        public static void Hook()
        {
            RandomizerMenuAPI.AddMenuPage(ConstructMenu, HandleButton);
            MenuChangerMod.OnExitMainMenu += OnExitMenu;
        }

        private static bool HandleButton(MenuPage landingPage, out SmallButton button)
        {
            button = Instance.OpenWallRandoSettings;
            return true;
        }

        private static void ConstructMenu(MenuPage landingPage) => Instance = new(landingPage);

        private RandoMenuPage(MenuPage landingPage)
        {
            WallRandoPage = new MenuPage("BreakableWallSettings", landingPage);
            wallMEF = new(WallRandoPage, BreakableWallRandomiser.settings);
            wallVIP = new(WallRandoPage, new(0, 300), 75f, true, wallMEF.Elements);

            OpenWallRandoSettings = new(landingPage, Localize("Breakable Walls"));
            OpenWallRandoSettings.AddHideAndShowEvent(landingPage, WallRandoPage);
        }
    } 
}
