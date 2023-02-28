using ItemChanger;
using ItemChanger.Tags;
using System.Collections.Generic;

namespace BreakableWallRandomiser.IC
{
    public static class InteropTagFactory
    {
        private static void SetProperty(this InteropTag t, string prop, object value)
        {
            if (value != null)
            {
                t.Properties[prop] = value;
            }
        }

        private const string CmiModSourceProperty = "ModSource";
        private const string CmiPoolGroupProperty = "PoolGroup";
        private const string CmiSceneNamesProperty = "SceneNames";
        private const string CmiTitledAreasProperty = "TitledAreas";
        private const string CmiMapAreasProperty = "MapAreas";
        private const string CmiHighlightScenesProperty = "HighlightScenes";
        private const string CmiPinSpriteProperty = "PinSprite";
        private const string CmiMapLocationsProperty = "WorldMapLocations";
        private const string CmiMapNoPin = "DoNotMakePin";
        private const string CmiGridSort = "PinGridIndex";

        private const string RiMessageProperty = "DisplayMessage";
        private const string RiSourceProperty = "DisplaySource";
        private const string RiIgnoreProperty = "IgnoreItem";

        public static InteropTag CmiSharedTag(string poolGroup = null, ISprite pinSprite = null)
        {
            InteropTag t = new()
            {
                Message = "RandoSupplementalMetadata",
                Properties =
                {
                    [CmiModSourceProperty] = nameof(BreakableWallRandomiser)
                }
            };
            t.SetProperty(CmiPoolGroupProperty, poolGroup);
            t.SetProperty(CmiPinSpriteProperty, pinSprite);
            return t;
        }

        public static InteropTag CmiLocationTag(string poolGroup = null, ISprite pinSprite = null,
            IEnumerable<string> sceneNames = null, IEnumerable<string> titledAreas = null, IEnumerable<string> mapAreas = null,
            string[] highlightScenes = null, (string, float, float)[] mapLocations = null, bool? noPin = null, int? pinSort = null)
        {
            InteropTag t = CmiSharedTag(poolGroup: poolGroup, pinSprite: pinSprite);
            t.SetProperty(CmiSceneNamesProperty, sceneNames);
            t.SetProperty(CmiTitledAreasProperty, titledAreas);
            t.SetProperty(CmiMapAreasProperty, mapAreas);
            t.SetProperty(CmiHighlightScenesProperty, highlightScenes);
            t.SetProperty(CmiMapLocationsProperty, mapLocations);
            t.SetProperty(CmiMapNoPin, noPin);
            t.SetProperty(CmiGridSort, pinSort);
            return t;
        }

        public static InteropTag RecentItemsSharedTag(string messageOverride = null)
        {
            InteropTag t = new()
            {
                Message = "RecentItems"
            };
            t.SetProperty(RiMessageProperty, messageOverride);
            return t;
        }

        public static InteropTag RecentItemsLocationTag(string messageOverride = null, string sourceOverride = null,
            bool? ignore = null)
        {
            InteropTag t = RecentItemsSharedTag(messageOverride: messageOverride);
            t.SetProperty(RiSourceProperty, sourceOverride);
            t.SetProperty(RiIgnoreProperty, ignore);
            return t;
        }
    }
}