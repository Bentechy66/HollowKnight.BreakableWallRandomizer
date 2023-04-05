using MenuChanger;
using MenuChanger.MenuElements;
using MenuChanger.MenuPanels;
using MenuChanger.Extensions;
using RandomizerMod.Menu;
using static RandomizerMod.Localization;

namespace BreakableWallRandomiser.Rando
{
    public class WallRandoSettings
    {
        public bool RandomizeBreakableWoodenPlankWalls = false;
        public bool RandomizeBreakableRockWalls = false;
        public bool RandomizeDiveFloors = false;

        public bool RandomizeTutorialBreakableFloor = false;

        [MenuChanger.Attributes.MenuRange(-1, 99)]
        public int WoodenPlankWallGroup = -1;

        [MenuChanger.Attributes.MenuRange(-1, 99)]
        public int RockWallGroup = -1;

        [MenuChanger.Attributes.MenuRange(-1, 99)]
        public int DiveFloorGroup = -1;

        public bool GroupTogetherNearbyWalls = false;
        public bool ExcludeWallsWhichMaySoftlockYou = false;

        [Newtonsoft.Json.JsonIgnore]
        public bool AnyWalls => RandomizeBreakableRockWalls || RandomizeBreakableWoodenPlankWalls || RandomizeDiveFloors;
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

        private void SetTopLevelButtonColor()
        {
            if (OpenWallRandoSettings != null)
            {
                OpenWallRandoSettings.Text.color = BreakableWallRandomiser.settings.AnyWalls ? Colors.TRUE_COLOR : Colors.DEFAULT_COLOR;
            }
        }

        private static void ConstructMenu(MenuPage landingPage) => Instance = new(landingPage);

        public void PasteSettings(WallRandoSettings settings)
        {
            if (settings == null)
            {
                wallMEF.ElementLookup[nameof(WallRandoSettings.RandomizeBreakableRockWalls)].SetValue(false);
                wallMEF.ElementLookup[nameof(WallRandoSettings.RandomizeBreakableWoodenPlankWalls)].SetValue(false);
                wallMEF.ElementLookup[nameof(WallRandoSettings.RandomizeDiveFloors)].SetValue(false);

                wallMEF.ElementLookup[nameof(WallRandoSettings.WoodenPlankWallGroup)].SetValue(-1);
                wallMEF.ElementLookup[nameof(WallRandoSettings.DiveFloorGroup)].SetValue(-1);
                wallMEF.ElementLookup[nameof(WallRandoSettings.RockWallGroup)].SetValue(-1);

                wallMEF.ElementLookup[nameof(WallRandoSettings.ExcludeWallsWhichMaySoftlockYou)].SetValue(false);
                wallMEF.ElementLookup[nameof(WallRandoSettings.GroupTogetherNearbyWalls)].SetValue(false);

                return;
            }

            wallMEF.SetMenuValues(settings);
        }

        private RandoMenuPage(MenuPage landingPage)
        {
            WallRandoPage = new MenuPage("BreakableWallSettings", landingPage);
            wallMEF = new(WallRandoPage, BreakableWallRandomiser.settings);
            wallVIP = new(WallRandoPage, new(0, 300), 75f, true, wallMEF.Elements);

            foreach (IValueElement e in wallMEF.Elements)
            {
                e.SelfChanged += obj => SetTopLevelButtonColor();
            }

            OpenWallRandoSettings = new(landingPage, Localize("Breakable Walls"));
            OpenWallRandoSettings.AddHideAndShowEvent(landingPage, WallRandoPage);

            SetTopLevelButtonColor();
        }
    } 
}
