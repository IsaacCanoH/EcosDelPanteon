using UnityEngine;
using UnityEngine.Tilemaps;

namespace EcosDelPanteon.LevelFour
{
    [CreateAssetMenu(menuName = "Ecos del Panteon/Nivel 4/Agua animada")]
    public class CemeteryWaterTile : Tile
    {
        public Sprite[] frames;
        [Min(0.1f)] public float framesPerSecond = 4f;

        public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap,
            ref TileAnimationData tileAnimationData)
        {
            if (frames == null || frames.Length == 0) return false;
            tileAnimationData.animatedSprites = frames;
            tileAnimationData.animationSpeed = framesPerSecond;
            tileAnimationData.animationStartTime = 0f;
            return true;
        }
    }
}
