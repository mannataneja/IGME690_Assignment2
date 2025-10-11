using UnityEngine;
using System.Collections.Generic;

public class MeshGenerator : MonoBehaviour
{

    public SquareGrid squareGrid;
    public MeshFilter walls;
    public MeshFilter cave;
    public bool is2D;

    List<Vector3> vertices;
    List<int> triangles;

    Dictionary<int, List<Triangle>> triangleDictionary = new Dictionary<int, List<Triangle>>();
    List<List<int>> outlines = new List<List<int>>();
    HashSet<int> checkedVertices = new HashSet<int>();

    public float wallHeight = 15f;

    public GameObject sconcePrefab; // prefab to spawn along cave walls
    public float sconceSpacing = 6f; // distance between sconces

    List<GameObject> sconces = new List<GameObject>();
    public void GenerateMesh(int[,] map, float squareSize, int[,] roomMask = null)
    {
        triangleDictionary.Clear();
        outlines.Clear();
        checkedVertices.Clear();

        squareGrid = new SquareGrid(map, squareSize);
        vertices = new List<Vector3>();
        triangles = new List<int>();

        for (int x = 0; x < squareGrid.squares.GetLength(0); x++)
        {
            for (int y = 0; y < squareGrid.squares.GetLength(1); y++)
            {
                TriangulateSquare(squareGrid.squares[x, y]);
            }
        }

        Mesh mesh = new Mesh();
        cave.mesh = mesh;

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();

        int tileAmount = 10;
        Vector2[] uvs = new Vector2[vertices.Count];
        for (int i = 0; i < vertices.Count; i++)
        {
            float percentX = Mathf.InverseLerp(-map.GetLength(0) / 2f * squareSize, map.GetLength(0) / 2f * squareSize, vertices[i].x);
            float percentY = Mathf.InverseLerp(-map.GetLength(1) / 2f * squareSize, map.GetLength(1) / 2f * squareSize, vertices[i].z);
            uvs[i] = new Vector2(percentX * tileAmount, percentY * tileAmount);
        }

        Color[] colors = new Color[vertices.Count];
        int borderOffset = 1; // because borderedMap adds 1-cell border

        for (int i = 0; i < vertices.Count; i++)
        {
            int mapX = Mathf.RoundToInt(vertices[i].x + map.GetLength(0) / 2f);
            int mapY = Mathf.RoundToInt(vertices[i].z + map.GetLength(1) / 2f);

            // convert bordered coordinates back to roomMask range
            int rmX = mapX - borderOffset;
            int rmY = mapY - borderOffset;

            if (roomMask != null && rmX >= 0 && rmY >= 0 && rmX < roomMask.GetLength(0) && rmY < roomMask.GetLength(1))
            {
                colors[i] = (roomMask[rmX, rmY] == 1) ? Color.red : Color.gray;
            }
            else
            {
                colors[i] = Color.gray;
            }
        }

        mesh.colors = colors;


        if (is2D)
            Generate2DColliders();
        else
            CreateWallMesh();
    }

    void TriangulateSquare(Square square)
    {
        switch (square.configuration)
        {
            case 0: break;
            case 1: MeshFromPoints(square.centreLeft, square.centreBottom, square.bottomLeft); break;
            case 2: MeshFromPoints(square.bottomRight, square.centreBottom, square.centreRight); break;
            case 4: MeshFromPoints(square.topRight, square.centreRight, square.centreTop); break;
            case 8: MeshFromPoints(square.topLeft, square.centreTop, square.centreLeft); break;
            case 3: MeshFromPoints(square.centreRight, square.bottomRight, square.bottomLeft, square.centreLeft); break;
            case 6: MeshFromPoints(square.centreTop, square.topRight, square.bottomRight, square.centreBottom); break;
            case 9: MeshFromPoints(square.topLeft, square.centreTop, square.centreBottom, square.bottomLeft); break;
            case 12: MeshFromPoints(square.topLeft, square.topRight, square.centreRight, square.centreLeft); break;
            case 5: MeshFromPoints(square.centreTop, square.topRight, square.centreRight, square.centreBottom, square.bottomLeft, square.centreLeft); break;
            case 10: MeshFromPoints(square.topLeft, square.centreTop, square.centreRight, square.bottomRight, square.centreBottom, square.centreLeft); break;
            case 7: MeshFromPoints(square.centreTop, square.topRight, square.bottomRight, square.bottomLeft, square.centreLeft); break;
            case 11: MeshFromPoints(square.topLeft, square.centreTop, square.centreRight, square.bottomRight, square.bottomLeft); break;
            case 13: MeshFromPoints(square.topLeft, square.topRight, square.centreRight, square.centreBottom, square.bottomLeft); break;
            case 14: MeshFromPoints(square.topLeft, square.topRight, square.bottomRight, square.centreBottom, square.centreLeft); break;
            case 15:
                MeshFromPoints(square.topLeft, square.topRight, square.bottomRight, square.bottomLeft);
                checkedVertices.Add(square.topLeft.vertexIndex);
                checkedVertices.Add(square.topRight.vertexIndex);
                checkedVertices.Add(square.bottomRight.vertexIndex);
                checkedVertices.Add(square.bottomLeft.vertexIndex);
                break;
        }
    }

    void MeshFromPoints(params Node[] points)
    {
        AssignVertices(points);
        if (points.Length >= 3) CreateTriangle(points[0], points[1], points[2]);
        if (points.Length >= 4) CreateTriangle(points[0], points[2], points[3]);
        if (points.Length >= 5) CreateTriangle(points[0], points[3], points[4]);
        if (points.Length >= 6) CreateTriangle(points[0], points[4], points[5]);
    }

    void AssignVertices(Node[] points)
    {
        foreach (var p in points)
        {
            if (p.vertexIndex == -1)
            {
                p.vertexIndex = vertices.Count;
                vertices.Add(p.position);
            }
        }
    }

    void CreateTriangle(Node a, Node b, Node c)
    {
        triangles.Add(a.vertexIndex);
        triangles.Add(b.vertexIndex);
        triangles.Add(c.vertexIndex);
        Triangle tri = new Triangle(a.vertexIndex, b.vertexIndex, c.vertexIndex);
        AddTriangleToDictionary(tri.vertexIndexA, tri);
        AddTriangleToDictionary(tri.vertexIndexB, tri);
        AddTriangleToDictionary(tri.vertexIndexC, tri);
    }

    void AddTriangleToDictionary(int vertexIndexKey, Triangle tri)
    {
        if (triangleDictionary.ContainsKey(vertexIndexKey))
            triangleDictionary[vertexIndexKey].Add(tri);
        else
            triangleDictionary.Add(vertexIndexKey, new List<Triangle> { tri });
    }

    void CreateWallMesh()
    {
        CalculateMeshOutlines();

        List<Vector3> wallVerts = new List<Vector3>();
        List<int> wallTris = new List<int>();
        Mesh wallMesh = new Mesh();

        foreach (List<int> outline in outlines)
        {
            for (int i = 0; i < outline.Count - 1; i++)
            {
                int start = wallVerts.Count;
                wallVerts.Add(vertices[outline[i]]);
                wallVerts.Add(vertices[outline[i + 1]]);
                wallVerts.Add(vertices[outline[i]] - Vector3.up * wallHeight);
                wallVerts.Add(vertices[outline[i + 1]] - Vector3.up * wallHeight);

                wallTris.Add(start + 0); wallTris.Add(start + 2); wallTris.Add(start + 3);
                wallTris.Add(start + 3); wallTris.Add(start + 1); wallTris.Add(start + 0);
            }
        }

        wallMesh.vertices = wallVerts.ToArray();
        wallMesh.triangles = wallTris.ToArray();
        wallMesh.RecalculateNormals();
        walls.mesh = wallMesh;

        MeshCollider collider = GetComponent<MeshCollider>();
        if (collider) Destroy(collider);
        collider = gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = wallMesh;

        SpawnSconcesAlongWalls(wallVerts, outlines);
    }

    void Generate2DColliders()
    {
        foreach (var old in GetComponents<EdgeCollider2D>()) Destroy(old);
        CalculateMeshOutlines();
        foreach (List<int> outline in outlines)
        {
            EdgeCollider2D edge = gameObject.AddComponent<EdgeCollider2D>();
            Vector2[] points = new Vector2[outline.Count];
            for (int i = 0; i < outline.Count; i++)
                points[i] = new Vector2(vertices[outline[i]].x, vertices[outline[i]].z);
            edge.points = points;
        }
    }

    void CalculateMeshOutlines()
    {
        for (int i = 0; i < vertices.Count; i++)
        {
            if (checkedVertices.Contains(i)) continue;
            int next = GetConnectedOutlineVertex(i);
            if (next != -1)
            {
                checkedVertices.Add(i);
                List<int> newOutline = new List<int> { i };
                FollowOutline(next, newOutline);
                newOutline.Add(i);
                outlines.Add(newOutline);
            }
        }
    }

    void FollowOutline(int vertexIndex, List<int> outline)
    {
        outline.Add(vertexIndex);
        checkedVertices.Add(vertexIndex);
        int next = GetConnectedOutlineVertex(vertexIndex);
        if (next != -1)
        {
            FollowOutline(next, outline);
        }
    }

    int GetConnectedOutlineVertex(int vertexIndex)
    {
        if (!triangleDictionary.ContainsKey(vertexIndex)) return -1;
        List<Triangle> tris = triangleDictionary[vertexIndex];
        foreach (Triangle t in tris)
        {
            for (int i = 0; i < 3; i++)
            {
                int b = t[i];
                if (b != vertexIndex && !checkedVertices.Contains(b))
                {
                    if (IsOutlineEdge(vertexIndex, b)) return b;
                }
            }
        }
        return -1;
    }

    bool IsOutlineEdge(int a, int b)
    {
        int shared = 0;
        foreach (Triangle t in triangleDictionary[a]) if (t.Contains(b)) shared++;
        return shared == 1;
    }

    struct Triangle
    {
        public int vertexIndexA, vertexIndexB, vertexIndexC;
        int[] verts;
        public Triangle(int a, int b, int c)
        {
            vertexIndexA = a; vertexIndexB = b; vertexIndexC = c;
            verts = new int[3] { a, b, c };
        }
        public int this[int i] => verts[i];
        public bool Contains(int v) => v == vertexIndexA || v == vertexIndexB || v == vertexIndexC;
    }

    public class SquareGrid
    {
        public Square[,] squares;
        public SquareGrid(int[,] map, float size)
        {
            int nodeCountX = map.GetLength(0);
            int nodeCountY = map.GetLength(1);
            float mapWidth = nodeCountX * size;
            float mapHeight = nodeCountY * size;

            ControlNode[,] control = new ControlNode[nodeCountX, nodeCountY];
            for (int x = 0; x < nodeCountX; x++)
            {
                for (int y = 0; y < nodeCountY; y++)
                {
                    Vector3 pos = new Vector3(-mapWidth / 2 + x * size + size / 2, 0, -mapHeight / 2 + y * size + size / 2);
                    control[x, y] = new ControlNode(pos, map[x, y] == 1, size);
                }
            }

            squares = new Square[nodeCountX - 1, nodeCountY - 1];
            for (int x = 0; x < nodeCountX - 1; x++)
                for (int y = 0; y < nodeCountY - 1; y++)
                    squares[x, y] = new Square(control[x, y + 1], control[x + 1, y + 1], control[x + 1, y], control[x, y]);
        }
    }

    public class Square
    {
        public ControlNode topLeft, topRight, bottomRight, bottomLeft;
        public Node centreTop, centreRight, centreBottom, centreLeft;
        public int configuration;

        public Square(ControlNode tl, ControlNode tr, ControlNode br, ControlNode bl)
        {
            topLeft = tl; topRight = tr; bottomRight = br; bottomLeft = bl;
            centreTop = topLeft.right;
            centreRight = bottomRight.above;
            centreBottom = bottomLeft.right;
            centreLeft = bottomLeft.above;
            if (tl.active) configuration += 8;
            if (tr.active) configuration += 4;
            if (br.active) configuration += 2;
            if (bl.active) configuration += 1;
        }
    }

    public class Node
    {
        public Vector3 position;
        public int vertexIndex = -1;
        public Node(Vector3 pos) { position = pos; }
    }

    public class ControlNode : Node
    {
        public bool active;
        public Node above, right;
        public ControlNode(Vector3 pos, bool act, float size) : base(pos)
        {
            active = act;
            above = new Node(position + Vector3.forward * size / 2f);
            right = new Node(position + Vector3.right * size / 2f);
        }
    }

    void SpawnSconcesAlongWalls(List<Vector3> wallVerts, List<List<int>> outlines)
    {
        foreach (var sconce in sconces)
        {
            if (sconce != null)
            {
                Destroy(sconce); // clear previous prefabs
            }

        }
        if (sconcePrefab == null) return; // skip if none assigned

        float spacing = Mathf.Max(1f, sconceSpacing); // make sure it's never zero

        foreach (List<int> outline in outlines)
        {
            for (int i = 0; i < outline.Count - 1; i++)
            {
                if(i % sconceSpacing != 0)
                {
                    continue;
                }
                Vector3 a = vertices[outline[i]];
                Vector3 b = vertices[outline[i + 1]];
                Vector3 dir = (b - a).normalized;
                float length = Vector3.Distance(a, b);

                // step along wall segment using spacing
                for (float dist = 0; dist <= length; dist += spacing)
                {
                    Vector3 pos = a + dir * dist;

                    // find direction that faces inward into the cave
                    Vector3 inward = Vector3.Cross(dir, Vector3.up);
                    Quaternion rot = Quaternion.LookRotation(-inward, Vector3.up);

                    GameObject sconce = Instantiate(sconcePrefab, transform.TransformPoint(pos), rot);
                    sconce.transform.parent = gameObject.transform;
                    sconces.Add(sconce);
                }
            }
        }
    }


}
