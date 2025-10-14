using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(MeshGenerator))]
public class MapGenerator : MonoBehaviour
{

    public int mapWidth = 100;
    public int mapHeight = 100;

    public string seed;
    public bool useRandomSeed = true;
    [Range(0, 100)]
    public float randomFillPercent = 45;
    public Slider fillPercentSlider;

    public int minRoomSize = 8;
    public int maxRoomSize = 20;
    public int roomAttempts = 12;
    public Slider roomSizeSlider;
    
    public int caveSmoothIterations = 3;

    public GameObject entrancePrefab;
    public GameObject[] roomPrefabs; //add 1 prefab per room

    int[,] map;
    int[,] room;
    List<GeneratedRoom> generatedRooms = new List<GeneratedRoom>();
    List<GameObject> spawnedRoomObjects = new List<GameObject>();

    bool start = true;

    void Start()
    {
        start = true;
        StartCoroutine(nameof(StartGame));
        fillPercentSlider.value = randomFillPercent;
        roomSizeSlider.value = (minRoomSize + maxRoomSize) / 2;
        GenerateMap();
    }

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
                Destroy(obj);
            }
        }

        spawnedRoomObjects.Clear(); //clear all spawned objects each time the map is regenrated
        generatedRooms.Clear(); //clear all rooms each time map is regenrated

        map = new int[mapWidth, mapHeight];
        room = new int[mapWidth, mapHeight];


        RandomFillMap();
        GenerateRectangularRooms(minRoomSize, maxRoomSize, roomAttempts);
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
                {
                    borderedMap[x, y] = map[x - borderSize, y - borderSize];

                }
                else
                {
                    borderedMap[x, y] = 1;
                }
            }
        }

        MeshGenerator meshGen = GetComponent<MeshGenerator>();
        meshGen.GenerateMesh(borderedMap, 1);

        SpawnRoomPrefabs();
    }
    void ProcessMap()
    {
        List<List<Coord>> wallRegions = GetRegions(1);
        int wallThresholdSize = 50;

        foreach (List<Coord> wallRegion in wallRegions)
        {
            if (wallRegion.Count < wallThresholdSize)
            {
                foreach (Coord tile in wallRegion)
                {
                    map[tile.tileX, tile.tileY] = 0;
                }
            }
        }

        List<List<Coord>> roomRegions = GetRegions(0);
        int roomThresholdSize = 50;
        List<Room> survivingRooms = new List<Room>();

        foreach (List<Coord> roomRegion in roomRegions)
        {
            if (roomRegion.Count < roomThresholdSize)
            {
                foreach (Coord tile in roomRegion)
                {
                    map[tile.tileX, tile.tileY] = 1;
                }
            }
            else
            {
                survivingRooms.Add(new Room(roomRegion, map));
            }
        }

        if (survivingRooms.Count > 0)
        {
            survivingRooms.Sort();
            survivingRooms[0].isMainRoom = true;
            survivingRooms[0].isAccessibleFromMainRoom = true;
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
                    List<Coord> newRegion = GetRegionTiles(x, y);
                    regions.Add(newRegion);

                    foreach (Coord tile in newRegion)
                    {
                        mapFlags[tile.tileX, tile.tileY] = 1;
                    }
                }
            }
        }

        return regions;
    }

    List<Coord> GetRegionTiles(int startX, int startY)
    {
        List<Coord> tiles = new List<Coord>();
        int[,] mapFlags = new int[mapWidth, mapHeight];
        int tileType = map[startX, startY];

        Queue<Coord> queue = new Queue<Coord>();
        queue.Enqueue(new Coord(startX, startY));
        mapFlags[startX, startY] = 1;

        while (queue.Count > 0)
        {
            Coord tile = queue.Dequeue();
            tiles.Add(tile);

            for (int x = tile.tileX - 1; x <= tile.tileX + 1; x++)
            {
                for (int y = tile.tileY - 1; y <= tile.tileY + 1; y++)
                {
                    if (x >= 0 && x < mapWidth && y >= 0 && y < mapHeight)
                    {
                        if ((y == tile.tileY || x == tile.tileX) && mapFlags[x, y] == 0 && map[x, y] == tileType)
                        {
                            mapFlags[x, y] = 1;
                            queue.Enqueue(new Coord(x, y));
                        }
                    }
                }
            }
        }

        return tiles;
    }

    bool IsInMapRange(int x, int y)
    {
        return x >= 0 && x < mapWidth && y >= 0 && y < mapHeight;
    }

    void RandomFillMap()
    {
        if (useRandomSeed)
        {
            seed = Time.time.ToString();
        }
        System.Random pseudoRandom = new System.Random(seed.GetHashCode());

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (x == 0 || y == 0 || x == mapWidth - 1 || y == mapHeight - 1)
                {
                    map[x, y] = 1;
                }
                else
                {
                    map[x, y] = (pseudoRandom.Next(0, 100) < randomFillPercent) ? 1 : 0;
                }
            }
        }
    }

    void SmoothMap()
    {
        int[,] newMap = new int[mapWidth, mapHeight];

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                int neighbourWallTiles = GetSurroundingWallCount(x, y);

                if (neighbourWallTiles > 4)
                {
                    newMap[x, y] = 1;
                }
                else if (neighbourWallTiles < 4)
                {
                    newMap[x, y] = 0;
                }
                else
                {
                    newMap[x, y] = map[x, y];
                }
            }
        }

        map = newMap;
    }
    int GetSurroundingWallCount(int gridX, int gridY)
    {
        int wallCount = 0;
        for (int neighbourX = gridX - 1; neighbourX <= gridX + 1; neighbourX++)
        {
            for (int neighbourY = gridY - 1; neighbourY <= gridY + 1; neighbourY++)
            {
                if (neighbourX >= 0 && neighbourX < mapWidth && neighbourY >= 0 && neighbourY < mapHeight)
                {
                    if (neighbourX != gridX || neighbourY != gridY)
                        wallCount += map[neighbourX, neighbourY];
                }
                else
                {
                    wallCount++;
                }
            }
        }

        return wallCount;
    }
    void GenerateRectangularRooms(int minRoomSize, int maxRoomSize, int generationAttempts)
    {
        System.Random random = new System.Random(seed.GetHashCode());

        for (int attempt = 0; attempt < generationAttempts; attempt++)
        {
            int roomWidth = random.Next(minRoomSize, maxRoomSize);
            int roomHeight = random.Next(minRoomSize, maxRoomSize);
            int startX = random.Next(1, mapWidth - roomWidth - 1);
            int startY = random.Next(1, mapHeight - roomHeight - 1);

  
            bool overlap = false;
            for (int checkX = startX - 1; checkX < startX + roomWidth + 1; checkX++)
            {
                for (int checkY = startY - 1; checkY < startY + roomHeight + 1; checkY++)
                {
                    if (checkX > 0 && checkY > 0 && checkX < mapWidth && checkY < mapHeight && room[checkX, checkY] == 1)
                    {
                        overlap = true;
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
            for (int tileX = startX; tileX < startX + roomWidth; tileX++)
            {
                for (int tileY = startY; tileY < startY + roomHeight; tileY++)
                {
                    map[tileX, tileY] = 0;
                    room[tileX, tileY] = 1;
                }
            }

            Vector3 roomCenter = new Vector3(-mapWidth / 2 + startX + roomWidth / 2f, 0, -mapHeight / 2 + startY + roomHeight / 2f);

            generatedRooms.Add(new GeneratedRoom(startX, startY, roomWidth, roomHeight, roomCenter));
        }
    }


    void ConnectRoomsWithTunnels()
    {
        if (generatedRooms.Count < 2)
        {
            return;
        }

        for (int i = 0; i < generatedRooms.Count - 1; i++)
        {
            GeneratedRoom roomA = generatedRooms[i];
            GeneratedRoom roomB = generatedRooms[i + 1];

            DrawTunnel(roomA.worldCenter, roomB.worldCenter);
        }
    }

    void DrawTunnel(Vector3 from, Vector3 to)
    {
        int x0 = Mathf.RoundToInt(from.x + mapWidth / 2f);
        int y0 = Mathf.RoundToInt(from.z + mapHeight / 2f);
        int x1 = Mathf.RoundToInt(to.x + mapWidth / 2f);
        int y1 = Mathf.RoundToInt(to.z + mapHeight / 2f);

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = (x0 < x1) ? 1 : -1;
        int sy = (y0 < y1) ? 1 : -1;
        int error = dx - dy;

        int radius = UnityEngine.Random.Range(2, 4);

        while (true) //Bresenham's line algorithm
        {
            DrawCircle(x0, y0, radius);
            int e2 = 2 * error;

            if (e2 > -dy)
            {
                if(x0 == x1)
                {
                    break;
                }
                error -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                if(y0 == y1)
                {
                    break;
                }
                error += dx;
                y0 += sy;
            }
        }
    }



    void DrawCircle(int cx, int cy, int r)
    {
        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                if (x * x + y * y <= r * r)
                {
                    int drawX = cx + x;
                    int darwY = cy + y;
                    if (IsInMapRange(drawX, darwY))
                    {
                        map[drawX, darwY] = 0;
                    }
                }
            }
        }
    }

    void SpawnRoomPrefabs()
    {
        Debug.Log($"Spawning {generatedRooms.Count} rooms");

        for (int i = 0; i < generatedRooms.Count; i++)
        {
            var room = generatedRooms[i];
            Vector3 spawnPos = room.worldCenter + Vector3.up * -13.9f;

            GameObject prefabToSpawn;

            if (i == 0)
            {
                prefabToSpawn = entrancePrefab;
            }
            else
            {
                if (roomPrefabs != null && roomPrefabs.Length > 0)
                {
                    prefabToSpawn = roomPrefabs[UnityEngine.Random.Range(0, roomPrefabs.Length)];

                }
                else
                {
                    prefabToSpawn = GameObject.CreatePrimitive(PrimitiveType.Cube);
                }
            }

            GameObject instance = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            instance.transform.parent = transform;
            spawnedRoomObjects.Add(instance);
        }
    }

    public struct Coord
    {
        public int tileX, tileY;
        public Coord(int x, int y)
        {
            tileX = x;
            tileY = y;
        }
    }
    public class GeneratedRoom
    {
        public int x, y, width, height;
        public Vector3 worldCenter;
        public GeneratedRoom(int _x, int _y, int _w, int _h, Vector3 _center)
        {
            x = _x; y = _y; width = _w; height = _h; worldCenter = _center;
        }
    }

    class Room : IComparable<Room>
    {
        public List<Coord> tiles;
        public List<Coord> edgeTiles;
        public List<Room> connectedRooms;
        public int roomSize;
        public bool isAccessibleFromMainRoom;
        public bool isMainRoom;

        public Room() { }

        public Room(List<Coord> roomTiles, int[,] map)
        {
            tiles = roomTiles;
            roomSize = tiles.Count;
            connectedRooms = new List<Room>();

            edgeTiles = new List<Coord>();
            foreach (Coord tile in tiles)
            {
                for (int x = tile.tileX - 1; x <= tile.tileX + 1; x++)
                {
                    for (int y = tile.tileY - 1; y <= tile.tileY + 1; y++)
                    {
                        if (x >= 0 && x < map.GetLength(0) && y >= 0 && y < map.GetLength(1))
                        {
                            if (x == tile.tileX || y == tile.tileY)
                            {
                                if (map[x, y] == 1)
                                {
                                    edgeTiles.Add(tile);
                                }
                            }
                        }
                    }
                }
            }
        }

        void SetAccessibleFromMainRoom()
        {
            if (!isAccessibleFromMainRoom)
            {
                isAccessibleFromMainRoom = true;
                foreach (Room connectedRoom in connectedRooms)
                {
                    connectedRoom.SetAccessibleFromMainRoom();
                }
            }
        }
        public static void ConnectRooms(Room roomA, Room roomB)
        {
            if (roomA.isAccessibleFromMainRoom)
            {
                roomB.SetAccessibleFromMainRoom();
            }
            else if (roomB.isAccessibleFromMainRoom)
            {
                roomA.SetAccessibleFromMainRoom();
            }

            roomA.connectedRooms.Add(roomB);
            roomB.connectedRooms.Add(roomA);
        }
        public bool IsConnected(Room otherRoom)
        {
            return connectedRooms.Contains(otherRoom);
        }

        public int CompareTo(Room otherRoom)
        {
            return otherRoom.roomSize.CompareTo(roomSize);
        }
    }

    void OnDrawGizmos()
    {
        if (generatedRooms == null || generatedRooms.Count == 0) return;

        Gizmos.color = Color.green;
        foreach (var room in generatedRooms)
        {
            Gizmos.DrawWireCube(room.worldCenter + Vector3.up * 0.2f, new Vector3(room.width, 0.1f, room.height));
        }
    }

    public void OnRoomSizeSliderChanged()
    {
        minRoomSize = (int)roomSizeSlider.value - 5;
        maxRoomSize = (int)roomSizeSlider.value + 5;
        GenerateMap();
    }

    public void OnFillPercentSliderChanged() //change the fill percent without regenrating entire map
    {
        if (start)
        {
            return;
        }

        randomFillPercent = fillPercentSlider.value;
        System.Random pseudoRandom = new System.Random(seed.GetHashCode());
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (room[x, y] == 1)
                {
                    map[x, y] = 0;
                }
                else if (x == 0 || y == 0 || x == mapWidth - 1 || y == mapHeight - 1)
                {
                    map[x, y] = 1;
                }
                else
                {
                    map[x, y] = (pseudoRandom.Next(0, 100) < fillPercentSlider.value) ? 1 : 0;
                }
            }
        }
        ConnectRoomsWithTunnels();
        for (int i = 0; i < 5; i++)
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
        meshGen.GenerateMesh(borderedMap, 1);
    }
}
