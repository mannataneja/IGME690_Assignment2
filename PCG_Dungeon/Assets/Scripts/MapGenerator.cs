using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(MeshGenerator))]
public class MapGenerator : MonoBehaviour
{

    [Header("Map Dimensions")]
    public int mapWidth = 100;
    public int mapHeight = 100;

    [Header("Randomization")]
    public string seed;
    public bool useRandomSeed = true;
    [Range(0, 100)]
    public int randomFillPercent = 45;
    public Slider fillPercentSlider;

    [Header("Rectangular Rooms")]
    public int minRoomSize = 8;
    public int maxRoomSize = 20;
    public int roomAttempts = 12;
    public Slider roomSizeSlider;

    [Header("Blending")]
    [Range(0, 10)] public int caveSmoothIterations = 3;
    [Range(0, 10)] public int postBlendSmooths = 0;

    [Header("Room Type Prefabs")]
    public GameObject normalPrefab;
    public GameObject treasurePrefab;
    public GameObject bossPrefab;
    public GameObject shrinePrefab;
    public GameObject entrancePrefab;


    int[,] map; // cave grid data
    int[,] roomMask; // tracks where rooms are carved
    List<GeneratedRoom> generatedRooms = new List<GeneratedRoom>();
    List<GameObject> spawnedRoomObjects = new List<GameObject>();

    bool start = true;
    void Start()
    {
        start = true;
        StartCoroutine(nameof(StartGame));
        fillPercentSlider.value = randomFillPercent;
        roomSizeSlider.value = (minRoomSize + maxRoomSize) / 2;
        GenerateMap(); // generate once at start
    }
/*    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // left click regenerates map
        {
            Debug.Log("[MapGenerator] Left click detected — regenerating map.");
            GenerateMap();
        }
    }*/

    IEnumerator StartGame()
    {
        yield return new WaitForSeconds(2f);
        start = false;
    }
    public void GenerateMap()
    {

        foreach (var obj in spawnedRoomObjects)
        {
            if (obj != null)
            {
                Destroy(obj); // clear previous prefabs
            }

        }

        spawnedRoomObjects.Clear();
        generatedRooms.Clear();

        map = new int[mapWidth, mapHeight];
        roomMask = new int[mapWidth, mapHeight];

        if (useRandomSeed)
        {
            seed = Time.time.ToString(); // randomize seed each run
        }

        RandomFillMap(); // start with random noise
        GenerateRectangularRooms(minRoomSize, maxRoomSize, roomAttempts); // carve rooms
        ConnectRoomsWithTunnels(); // connect rooms using tunnels
        for (int i = 0; i < caveSmoothIterations; i++)
        {
            SmoothMap(); // smooth walls around rooms
        }

        ProcessMap(); // remove tiny regions

        int borderSize = 1; // add map border
        int[,] borderedMap = new int[mapWidth + borderSize * 2, mapHeight + borderSize * 2];
        for (int x = 0; x < borderedMap.GetLength(0); x++)
        {
            for (int y = 0; y < borderedMap.GetLength(1); y++)
            {
                if (x >= borderSize && x < mapWidth + borderSize && y >= borderSize && y < mapHeight + borderSize)
                    borderedMap[x, y] = map[x - borderSize, y - borderSize];
                else borderedMap[x, y] = 1; // fill outer edges
            }
        }
        MeshGenerator meshGen = GetComponent<MeshGenerator>();
        meshGen.GenerateMesh(borderedMap, 1, roomMask); // build cave mesh

        SpawnRoomPrefabs(); // place visual markers

    }

    void GenerateRectangularRooms(int minSize, int maxSize, int attempts)
    {
        System.Random pseudoRandom = new System.Random(seed.GetHashCode());

        for (int i = 0; i < attempts; i++)
        {
            int roomWidth = UnityEngine.Random.Range(minSize, maxSize + 1); // room width
            int roomHeight = UnityEngine.Random.Range(minSize, maxSize + 1); // room height
            int x = UnityEngine.Random.Range(1, mapWidth - roomWidth); // random position
            int y = UnityEngine.Random.Range(1, mapHeight - roomHeight);

            bool overlap = false;
            for (int xx = x - 1; xx < x + roomWidth + 1; xx++)
            {
                for (int yy = y - 1; yy < y + roomHeight + 1; yy++)
                {
                    if (xx > 0 && yy > 0 && xx < mapWidth && yy < mapHeight && roomMask[xx, yy] == 1)
                    {
                        overlap = true; // skip if overlaps another room
                        break;
                    }
                }
                if (overlap)
                {
                    break;
                }

            }

            if (overlap)
            {
                continue;
            }

            for (int xx = x; xx < x + roomWidth; xx++)
            {
                for (int yy = y; yy < y + roomHeight; yy++)
                {
                    map[xx, yy] = 0; // carve floor
                    roomMask[xx, yy] = 1; // mark room
                }
            }
            Vector3 center = new Vector3(-mapWidth / 2 + x + roomWidth / 2f, 0, -mapHeight / 2 + y + roomHeight / 2f); // world center
            RoomType type = GetRandomRoomType(pseudoRandom , i); // assign random type
            generatedRooms.Add(new GeneratedRoom(x, y, roomWidth, roomHeight, center, type)); // save data
        }

        Debug.Log($"Generated {generatedRooms.Count} rectangular rooms.");
    }

    void ConnectRoomsWithTunnels()
    {
        if (generatedRooms.Count < 2)
        {
            return; // need at least two rooms
        }

        for (int i = 0; i < generatedRooms.Count - 1; i++)
        {
            GeneratedRoom roomA = generatedRooms[i];
            GeneratedRoom roomB = generatedRooms[i + 1];

            CarveTunnel(roomA.worldCenter, roomB.worldCenter); // link them
        }
    }

    void CarveTunnel(Vector3 from, Vector3 to)
    {
        int x0 = Mathf.RoundToInt(from.x + mapWidth / 2f);
        int y0 = Mathf.RoundToInt(from.z + mapHeight / 2f);
        int x1 = Mathf.RoundToInt(to.x + mapWidth / 2f);
        int y1 = Mathf.RoundToInt(to.z + mapHeight / 2f);

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = (x0 < x1) ? 1 : -1;
        int sy = (y0 < y1) ? 1 : -1;
        int err = dx - dy;

        int tunnelRadius = UnityEngine.Random.Range(2, 4); // tunnel width

        while (true)
        {
            CarveCircle(x0, y0, tunnelRadius); // carve at this step

            if (x0 == x1 && y0 == y1)
                break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }

    void CarveCircle(int cx, int cy, int r)
    {
        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                if (x * x + y * y <= r * r)
                {
                    int wx = cx + x;
                    int wy = cy + y;
                    if (wx >= 0 && wx < mapWidth && wy >= 0 && wy < mapHeight)
                    {
                        map[wx, wy] = 0; // carve open area
                    }
                }
            }
        }
    }

    RoomType GetRandomRoomType(System.Random pseudoRandom, int i)
    {
        if (i == 0)
        {
            return RoomType.Entrance; //always make entrance first

        }
        float roll = UnityEngine.Random.Range(0f, 1f);
        if (roll >= 0 && roll <= 0.25)
        {
            return RoomType.Treasure;
        }
        if (roll > 0.25 && roll <= 0.50f)
        {
            return RoomType.Boss;
        }
        if (roll > 0.50 && roll <= 0.75f)
        {
            return RoomType.Shrine;
        }
        else
        {
            return RoomType.Normal;
        }
    }

    void SpawnRoomPrefabs()
    {
        Debug.Log($"Spawning {generatedRooms.Count} rooms");

        foreach (var room in generatedRooms)
        {
            Vector3 spawnPos = room.worldCenter + Vector3.up * -4f; //lower prefab into dungeon

            GameObject prefabToSpawn = null;
            switch (room.type)
            {
                case RoomType.Normal: prefabToSpawn = normalPrefab; break;
                case RoomType.Treasure: prefabToSpawn = treasurePrefab; break;
                case RoomType.Boss: prefabToSpawn = bossPrefab; break;
                case RoomType.Shrine: prefabToSpawn = shrinePrefab; break;
                case RoomType.Entrance: prefabToSpawn = entrancePrefab; break;
            }

            if (prefabToSpawn == null)
            {
                prefabToSpawn = GameObject.CreatePrimitive(PrimitiveType.Cube); // fallback cube
                prefabToSpawn.transform.localScale = new Vector3(2, 2, 2);
            }

            GameObject instance = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            instance.name = $"Room_{room.type}_{room.x}_{room.y}";

            Renderer rend = instance.GetComponent<Renderer>();
            if (rend != null)
            {
                switch (room.type)
                {
                    case RoomType.Normal: rend.material.color = Color.gray; break;
                    case RoomType.Treasure: rend.material.color = Color.yellow; break;
                    case RoomType.Boss: rend.material.color = Color.red; break;
                    case RoomType.Shrine: rend.material.color = Color.cyan; break;
                    case RoomType.Entrance: rend.material.color = Color.green; break;
                }
            }
            instance.transform.parent = gameObject.transform;
            spawnedRoomObjects.Add(instance);
            Debug.Log($"Spawned {room.type} at {spawnPos}");
        }
    }

    void RandomFillMap()
    {
        System.Random pseudoRandom  = new System.Random(seed.GetHashCode());
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (x == 0 || y == 0 || x == mapWidth - 1 || y == mapHeight - 1)
                {
                    map[x, y] = 1; //border walls
                }
                else
                {
                    map[x, y] = (UnityEngine.Random.Range(0, 100) < randomFillPercent) ? 1 : 0; // random fill
                }
            }
        }
    }

    void SmoothMap()
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                int neighbours = GetSurroundingWallCount(x, y); // check nearby tiles
                if (neighbours > 5) map[x, y] = 1; // fill dense
                else if (neighbours < 3) map[x, y] = 0; // clear sparse
            }
        }
    }

    int GetSurroundingWallCount(int gridX, int gridY)
    {
        int count = 0;
        for (int nx = gridX - 1; nx <= gridX + 1; nx++)
        {
            for (int ny = gridY - 1; ny <= gridY + 1; ny++)
            {
                if (nx >= 0 && nx < mapWidth && ny >= 0 && ny < mapHeight)
                {
                    if (nx != gridX || ny != gridY) count += map[nx, ny]; // count wall tiles
                }
                else
                {
                    count++; // out of bounds = wall
                }
            }
        }
        return count;
    }

    void ProcessMap()
    {
        List<List<Coord>> wallRegions = GetRegions(1); // find wall groups
        foreach (var r in wallRegions)
        {
            if (r.Count < 40)
            {
                foreach (var t in r)
                {
                    map[t.tileX, t.tileY] = 0; // remove tiny walls
                }
            }
        }

    }

    List<List<Coord>> GetRegions(int tileType)
    {
        List<List<Coord>> regions = new List<List<Coord>>();
        int[,] mapFlags = new int[mapWidth, mapHeight];
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (mapFlags[x, y] == 0 && map[x, y] == tileType)
                {
                    regions.Add(FloodFillRegion(x, y, tileType, mapFlags)); // flood fill
                }
            }

        }

        return regions;
    }

    List<Coord> FloodFillRegion(int startX, int startY, int tileType, int[,] mapFlags)
    {
        List<Coord> tiles = new List<Coord>();
        Queue<Coord> q = new Queue<Coord>();
        q.Enqueue(new Coord(startX, startY));
        mapFlags[startX, startY] = 1;
        while (q.Count > 0)
        {
            Coord t = q.Dequeue();
            tiles.Add(t);
            for (int x = t.tileX - 1; x <= t.tileX + 1; x++)
            {
                for (int y = t.tileY - 1; y <= t.tileY + 1; y++)
                {
                    if (x >= 0 && y >= 0 && x < mapWidth && y < mapHeight)
                    {
                        if ((x == t.tileX || y == t.tileY) && mapFlags[x, y] == 0 && map[x, y] == tileType)
                        {
                            mapFlags[x, y] = 1;
                            q.Enqueue(new Coord(x, y)); // expand region
                        }
                    }
                }
            }
        }
        return tiles;
    }

    [System.Serializable]
    public struct Coord { public int tileX, tileY; public Coord(int x, int y) { tileX = x; tileY = y; } }

    [System.Serializable]
    public class GeneratedRoom
    {
        public int x, y, width, height;
        public Vector3 worldCenter;
        public RoomType type;
        public GeneratedRoom(int _x, int _y, int _w, int _h, Vector3 _center, RoomType _type)
        {
            x = _x; y = _y; width = _w; height = _h; worldCenter = _center; type = _type;
        }
    }

    public enum RoomType { Normal, Treasure, Boss, Shrine, Entrance }

    void OnDrawGizmos()
    {
        if (generatedRooms == null || generatedRooms.Count == 0) return;

        foreach (var room in generatedRooms)
        {
            switch (room.type)
            {
                case RoomType.Normal: Gizmos.color = Color.gray; break;
                case RoomType.Treasure: Gizmos.color = Color.yellow; break;
                case RoomType.Boss: Gizmos.color = Color.red; break;
                case RoomType.Shrine: Gizmos.color = Color.cyan; break;
                case RoomType.Entrance: Gizmos.color = Color.green; break;
            }
            Gizmos.DrawWireCube(room.worldCenter + Vector3.up * 0.2f, new Vector3(room.width, 0.1f, room.height)); // draw box for room
        }
    }

    public void OnRoomSizeSliderChanged()
    {
        minRoomSize = (int)roomSizeSlider.value - 5;
        maxRoomSize = (int)roomSizeSlider.value - 5;

        GenerateMap(); 
    }
    public void OnFillPercentSliderChanged()
    {
        if (start)
        {
            return;
        }

        System.Random rand = new System.Random(seed.GetHashCode());
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (roomMask[x, y] == 1)
                {
                    map[x, y] = 0; // always empty space inside rooms
                }
                else if (x == 0 || y == 0 || x == mapWidth - 1 || y == mapHeight - 1)
                {
                    map[x, y] = 1; // solid border
                }
                else
                {
                    map[x, y] = (rand.Next(0, 100) < fillPercentSlider.value) ? 1 : 0;
                }
            }
        }
        ConnectRoomsWithTunnels();
        for (int i = 0; i < caveSmoothIterations; i++)
        {
            SmoothMap();
        }

        ProcessMap();

        int borderSize = 1;
        int[,] borderedMap = new int[mapWidth + borderSize * 2, mapHeight + borderSize * 2];
        for (int x = 0; x < borderedMap.GetLength(0); x++)
        {
            for (int y = 0; y < borderedMap.GetLength(1); y++)
            {
                if (x >= borderSize && x < mapWidth + borderSize && y >= borderSize && y < mapHeight + borderSize)
                    borderedMap[x, y] = map[x - borderSize, y - borderSize];
                else borderedMap[x, y] = 1;
            }
        }

        MeshGenerator meshGen = GetComponent<MeshGenerator>();
        meshGen.GenerateMesh(borderedMap, 1, roomMask);
    }
}
