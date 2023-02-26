using System;
using UnityEngine;
using ItemChanger;
using ItemChanger.Internal;

namespace BreakableWallRandomiser.IC
{
    [Serializable]
    public class WallSprite : ISprite
    {
        private static SpriteManager EmbeddedSpriteManager = new(typeof(WallSprite).Assembly, "BreakableWallRandomiser.Resources.Sprites.");

        public string key;
        public WallSprite(string key)
        {
            this.key = key;
        }

        [Newtonsoft.Json.JsonIgnore]
        public Sprite Value => EmbeddedSpriteManager.GetSprite(key);
        public ISprite Clone() => (ISprite)MemberwiseClone();
    }
}