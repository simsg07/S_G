using UnityEngine;

public static class CameraHighlightSharedResources3D
{
    private static Mesh lineCubeMesh;
    private static Mesh solidCubeMesh;
    private static Material outlineMaterial;
    private static Material markerMaterial;

    public static Mesh LineCubeMesh => lineCubeMesh != null ? lineCubeMesh : lineCubeMesh = CreateLineCube();
    public static Mesh SolidCubeMesh => solidCubeMesh != null ? solidCubeMesh : solidCubeMesh = CreateSolidCube();
    public static Material OutlineMaterial => outlineMaterial != null ? outlineMaterial : outlineMaterial = CreateMaterial("Shared Camera Outline Material");
    public static Material MarkerMaterial => markerMaterial != null ? markerMaterial : markerMaterial = CreateMaterial("Shared Camera Marker Material");

    private static Material CreateMaterial(string name)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        return new Material(shader) { name = name, hideFlags = HideFlags.HideAndDontSave };
    }

    private static Mesh CreateLineCube()
    {
        Mesh mesh = new Mesh { name = "Shared Camera Outline Cube", hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = CubeVertices();
        mesh.SetIndices(new[] { 0,1,1,2,2,3,3,0,4,5,5,6,6,7,7,4,0,4,1,5,2,6,3,7 }, MeshTopology.Lines, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateSolidCube()
    {
        Mesh mesh = new Mesh { name = "Shared Camera Marker Cube", hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = CubeVertices();
        mesh.triangles = new[] { 0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,2,3,7,2,7,6,1,2,6,1,6,5,3,0,4,3,4,7 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Vector3[] CubeVertices()
    {
        return new[] { new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f) };
    }
}
