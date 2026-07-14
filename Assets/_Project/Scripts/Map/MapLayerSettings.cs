using UnityEngine;

[CreateAssetMenu(menuName = "Project/Map/Map Layer Settings")]
public class MapLayerSettings : ScriptableObject
{
    public float backgroundWallZ = 2f;
    public float decorationZ = 1f;
    public float floorZ = 0f;
    public float wallZ = 0f;
    public float tileZ = 0f;
    public float platformZ = 0f;
    public float structureZ = 0f;
    public float objectZ = -0.2f;

    public float GetZ(MapPieceType type)
    {
        switch (type)
        {
            case MapPieceType.BackgroundWall:
                return backgroundWallZ;
            case MapPieceType.Decoration:
                return decorationZ;
            case MapPieceType.Object:
                return objectZ;
            case MapPieceType.Floor:
            case MapPieceType.FloorCollision:
                return floorZ;
            case MapPieceType.Wall:
                return wallZ;
            case MapPieceType.Platform:
                return platformZ;
            case MapPieceType.Structure:
                return structureZ;
            case MapPieceType.Tile:
            case MapPieceType.VisualTile:
            default:
                return tileZ;
        }
    }
}
