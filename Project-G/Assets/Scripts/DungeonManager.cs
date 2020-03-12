﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DungeonManager : MonoBehaviour
{
	private enum Tiles { Bridges, Corridors, Floors, Walls, Waters }
	public GameObject Player;
	public GameObject Dungeon;
	public GameObject FloorTile;
	public GameObject CorridorTile;
	public GameObject BridgeTile;
	public GameObject WaterTile;
	public GameObject[] WallTiles;
	public GameObject ExitTile;
	private GameObject[,] _dungeonFloorPositions;
	private int[,] _dungeonTiles;		// the tiles that players and other NPCs can walk on
	List<Vector2Int> _bridgeTilesPos;
	public int DungeonRows, DungeonColumns;
	public int DungeonPadding;
	public int MinRoomSize, MaxRoomSize;
	private Vector3 _playerSpawnPos;

	public int[,] DungeonMap { get {return _dungeonTiles;} }

	public class SubDungeon {
		public SubDungeon left, right;
		public Rect rect;
		public Rect room = new Rect(-1, -1, 0, 0);     // null
		public List<Rect> corridors = new List<Rect>();
		public List<Rect> bridges = new List<Rect>();
		public int debugId;

		private static int debugCounter = 0;

		public SubDungeon(Rect mrect) {
			rect = mrect;
			debugId = debugCounter;
			debugCounter++;
		}

		public void CreateRoom() {
			if (left != null)
				left.CreateRoom();
			if (right != null)
				right.CreateRoom();
			if (left != null && right != null)
				CreateCorridorBetween(left, right);

			if (IAmLeaf()) {
				int roomWidth = (int)Random.Range(rect.width / 2, rect.width - 2);
				int roomHeight = (int)Random.Range(rect.height / 2, rect.height - 2);
				int roomX = (int)Random.Range(1, rect.width - roomWidth - 1);
				int roomY = (int)Random.Range(1, rect.height - roomHeight - 1);

				// room position will be absolute in the board, not relative to the sub-deungeon
				room = new Rect(rect.x + roomX, rect.y + roomY, roomWidth, roomHeight);
				//Debug.Log("Created room " + room + " in sub-dungeon " + debugId + " " + rect);
			}
		}

		public void CreateCorridorBetween(SubDungeon left, SubDungeon right) {
			Rect lroom = left.GetRoom();
			Rect rroom = right.GetRoom();

			//Debug.Log("Creating corridor(s) between " + left.debugId + "(" + lroom + ") and " + right.debugId + " (" + rroom + ")");

			// attach the corridor to a random point in each room
			Vector2 lpoint = new Vector2((int)Random.Range(lroom.x + 1, lroom.xMax - 1), (int)Random.Range(lroom.y + 1, lroom.yMax - 1));
			Vector2 rpoint = new Vector2((int)Random.Range(rroom.x + 1, rroom.xMax - 1), (int)Random.Range(rroom.y + 1, rroom.yMax - 1));

			// always be sure that left point is on the left to simplyfy code
			if (lpoint.x > rpoint.x) {
				Vector2 temp = lpoint;
				lpoint = rpoint;
				rpoint = temp;
			}

			int w = (int)(lpoint.x - rpoint.x);
			int h = (int)(lpoint.y - rpoint.y);

			int thickness = Random.Range(1, 4);     // getting a random thickness
			List<Rect> connections = (thickness > 1) ? corridors : bridges;     // if the tickness > 1, it is a corridor; otherwise, it's a bridge

			//Debug.Log("lpoint: " + lpoint + ", rpoint: " + rpoint + ", w: " + w + ", h: " + h);

			// if the points are not aligned horizontally
			if ( w != 0) {
				if (Random.Range (0, 2) > 0) {
      				// add a corridor to the right
					connections.Add(new Rect(lpoint.x, lpoint.y, Mathf.Abs(w) + 1, thickness));

					// if left point is below right point go up
					// otherwise go down
					if (h < 0)
						connections.Add(new Rect(rpoint.x, lpoint.y, thickness, Mathf.Abs(h)));
					else
						connections.Add(new Rect(rpoint.x, rpoint.y, thickness, Mathf.Abs(h)));
				}
				else {
					// go up or down
					if (h < 0)
						connections.Add(new Rect(lpoint.x, lpoint.y, thickness, Mathf.Abs(h)));
					else
						connections.Add(new Rect(lpoint.x, rpoint.y, thickness, Mathf.Abs(h)));

					// then go right
					connections.Add(new Rect(lpoint.x, rpoint.y, Mathf.Abs(w) + 1, thickness));
				}
			}
			else {
				// if the points are aligned horizontally
				// go up or down depending on the positions
				if (h < 0)
					connections.Add(new Rect((int)lpoint.x, (int)lpoint.y, thickness, Mathf.Abs(h)));
				else
					connections.Add(new Rect((int)rpoint.x, (int)rpoint.y, thickness, Mathf.Abs(h)));
			}

			/*Debug.Log("Corridors: ");
			foreach(Rect corridor in corridors) {
				Debug.Log("corridor: " + corridor);
			}*/
		}
		
		public Rect GetRoom() {
			if (IAmLeaf())
				return room;

				if (left != null) {
					Rect lroom = left.GetRoom();
					if (lroom.x != -1)
						return lroom;
				}

				if (right != null) {
					Rect rrom = right.GetRoom();
					if (rrom.x != -1)
						return rrom;
				}

				// workaround non nullable structs
				return new Rect(-1, -1, 0, 0);
		}

		public bool IAmLeaf() {
			return left == null && right == null;
		}

		/*
		choose a vertical or horizontal split depending on the proportion
		i.e. if too wide split vertically, or too long horizontally,
		or if nearly square choose vertical or horizontal at random
		*/
		public bool Split(int minRoomSize, int maxRoomSize) {
			if (!IAmLeaf())
				return false;

			bool splitH;
			if (rect.width / rect.height >= 1.25)
				splitH = false;
			else if (rect.height / rect.width >= 1.25)
				splitH = true;
			else
				splitH = Random.Range(0.0f, 1.0f) > 0.5;

			if (Mathf.Min(rect.height, rect.width) / 2 < minRoomSize) {
				//Debug.Log("Sub-dungeon " + debugId + " will be a leaf");
				return false;
			}

			if (splitH) {
				// split so that the resulting sub-dungeons widths are not too small
				// (since we are splitting horizontally)
				int split = Random.Range(minRoomSize, (int)(rect.width - minRoomSize));

				left = new SubDungeon(new Rect(rect.x, rect.y, rect.width, split));
				right = new SubDungeon(new Rect(rect.x, rect.y + split, rect.width, rect.height - split));
			}
			else {
				int split = Random.Range(minRoomSize, (int)(rect.height - minRoomSize));

				left = new SubDungeon(new Rect(rect.x, rect.y, split, rect.height));
				right = new SubDungeon(new Rect(rect.x + split, rect.y, rect.width - split, rect.height));
			}
			return true;
		}
	}

	public void CreateBSP(SubDungeon subDungeon) {
		//Debug.Log("Splitting sub-dungeon " + subDungeon.debugId + ": " + subDungeon.rect);
		if (subDungeon.IAmLeaf()) {
			// if the subdungeon is too large
			if (subDungeon.rect.width > MaxRoomSize || subDungeon.rect.height > MaxRoomSize || Random.Range(0.0f, 1.0f) > 0.25) {
				if (subDungeon.Split (MinRoomSize, MaxRoomSize)) {
					//Debug.Log ("Splitted sub-dungeon " + subDungeon.debugId + " in " + subDungeon.left.debugId + ": " + subDungeon.left.rect + ", "
					//+ subDungeon.right.debugId + ": " + subDungeon.right.rect);

					CreateBSP(subDungeon.left);
					CreateBSP(subDungeon.right);
				}
			}
		}
	}

	public void DrawRooms(SubDungeon subDungeon) {
		if (subDungeon == null)
			return;

		if (subDungeon.IAmLeaf()) {
			int editRoom = Random.Range(0,4);       // 25% chance for editing the room
			int x = 0, y = 0, xMax = 0, yMax = 0;
			if (editRoom == 0) {
				int randWidth = Random.Range(1, (int)(subDungeon.room.width / 2));
				int randHeight = Random.Range(1, (int)(subDungeon.room.height / 2));
				int randCorner = Random.Range(0,4);
				switch (randCorner) {
					case 0:     // bottom left
						x = (int)subDungeon.room.x;
						y = (int)subDungeon.room.y;
						break;
					case 1:     // bottom right
						x = (int)subDungeon.room.xMax - randWidth;
						y = (int)subDungeon.room.y;
						break;
					case 2:     // top left
						x = (int)subDungeon.room.x;
						y = (int)subDungeon.room.yMax - randHeight;
						break;
					case 3:     // top right
						x = (int)subDungeon.room.xMax - randWidth;
						y = (int)subDungeon.room.yMax - randHeight;
						break;
				}
				xMax = x + randWidth;
				if(xMax > subDungeon.room.xMax)
					xMax = (int)subDungeon.room.xMax;
				yMax = y + randHeight;
				if (yMax > subDungeon.room.yMax)
					yMax = (int)subDungeon.room.yMax;
			}

			for (int i = (int)subDungeon.room.x; i < subDungeon.room.xMax; i++) {
				for (int j = (int)subDungeon.room.y; j < subDungeon.room.yMax; j++) {
					if (!(i >= x && i <= xMax && j >= y && j <= yMax)) {
						GameObject instance = Instantiate(FloorTile, new Vector3(i, j, 0f), Quaternion.identity) as GameObject;
						instance.transform.SetParent(Dungeon.transform.GetChild((int)Tiles.Floors).gameObject.transform);
						_dungeonFloorPositions[i, j] = instance;
						_dungeonTiles[i, j] = 1;
					}
				}
			}
		}
		else {
			DrawRooms(subDungeon.left);
			DrawRooms(subDungeon.right);
		}
	}
	
	void DrawBridges(SubDungeon subDungeon) {
		if (subDungeon == null)
			return;
		
		DrawBridges(subDungeon.left);
		DrawBridges(subDungeon.right);

		foreach (Rect bridge in subDungeon.bridges) {
			for (int i = (int)bridge.x; i < bridge.xMax; i++) {
				for (int j = (int)bridge.y; j < bridge.yMax; j++) {
					if (_dungeonFloorPositions[i, j] == null) {
						GameObject instance = Instantiate(BridgeTile, new Vector3 (i, j, 0f), Quaternion.identity) as GameObject;
						instance.transform.SetParent(Dungeon.transform.GetChild((int)Tiles.Bridges).gameObject.transform);
						_dungeonFloorPositions[i, j] = instance;
						_bridgeTilesPos.Add(new Vector2Int(i, j));
						_dungeonTiles[i, j] = 1;
					}
				}
			}
		}
	}
	
	void DrawCorridors(SubDungeon subDungeon) {
		if (subDungeon == null)
			return;
		
		DrawCorridors(subDungeon.left);
		DrawCorridors(subDungeon.right);

		foreach (Rect corridor in subDungeon.corridors) {
			for (int i = (int)corridor.x; i < corridor.xMax; i++) {
				for (int j = (int)corridor.y; j < corridor.yMax; j++) {
					if (_dungeonFloorPositions[i, j] == null) {
						GameObject instance = Instantiate(CorridorTile, new Vector3 (i, j, 0f), Quaternion.identity) as GameObject;
						instance.transform.SetParent(Dungeon.transform.GetChild((int)Tiles.Corridors).gameObject.transform);
						_dungeonFloorPositions[i, j] = instance;
						_dungeonTiles[i, j] = 1;
					}
				}
			}
		} 
	}

	void DrawWalls() {
		int matrixSize = 3, index;

		int [,] kernelMatrix = {{4, 64, 2}, {128, 0, 32}, {8, 16, 1}};
		
		for (int j = DungeonColumns + (2 * DungeonPadding) - matrixSize; j >= 0; j--) {
			for (int i = 0; i <= DungeonRows + (2 * DungeonPadding) - matrixSize; i++) {
				index = 0;
				for (int l = 0; l < matrixSize; l++) {
					for (int k = 0; k < matrixSize; k++) {
						index += Mathf.Abs(_dungeonTiles[i + k, j + l]) * kernelMatrix[l, k];
					}
				}

				GameObject instance = null;
				int wallPosX = i + 1, wallPosY = j + 1;
				if (_dungeonFloorPositions[wallPosX, wallPosY] == null && _dungeonTiles[wallPosX, wallPosY] == 0) {
					instance = Instantiate(WallTiles[index], new Vector3 (wallPosX, wallPosY, 0f), Quaternion.identity) as GameObject;
					instance.transform.SetParent(Dungeon.transform.GetChild((int)Tiles.Walls).gameObject.transform);
					_dungeonFloorPositions[wallPosX, wallPosY] = instance;

					if (index != 0) {		// placing floor tile under the walls
						instance = Instantiate(FloorTile, new Vector3 (wallPosX, wallPosY, 0f), Quaternion.identity) as GameObject;
						instance.transform.SetParent(Dungeon.transform.GetChild((int)Tiles.Floors).gameObject.transform);
					}
				}
			}
		}
	}

	void DrawWaters() {
		foreach (var bridgePos in _bridgeTilesPos) {
			for (int i = -1; i <= 1; i++) {
				for (int j = -1; j <= 1; j++) {
					if (i == 0 && j == 0)		// skip the bridge tile
						continue;
					if (_dungeonFloorPositions[bridgePos.x + i, bridgePos.y + j] == null) {
						GameObject instance = Instantiate(WaterTile, new Vector3 (bridgePos.x + i, bridgePos.y + j, 0f), Quaternion.identity) as GameObject;
						instance.transform.SetParent(Dungeon.transform.GetChild((int)Tiles.Waters).gameObject.transform);
						_dungeonFloorPositions[bridgePos.x + i, bridgePos.y + j] = instance;
						_dungeonTiles[bridgePos.x + i, bridgePos.y + j] = -1;
					}
				}
			}
		}
	}

	void SetSpawnPos(SubDungeon subDungeon) {
		if (subDungeon == null)
			return;

		if (subDungeon.IAmLeaf()) {
			int spawnPosX, spawnPosY;
			int padding = (int)(((int)subDungeon.room.xMax - (int)subDungeon.room.x) / 4);
			do {
				spawnPosX = Random.Range((int)subDungeon.room.x + padding, (int)subDungeon.room.xMax - padding);
				spawnPosY = Random.Range((int)subDungeon.room.y + padding, (int)subDungeon.room.yMax - padding);
			} while(_dungeonTiles[spawnPosX, spawnPosY] != 1);
			_playerSpawnPos = new Vector3(spawnPosX, spawnPosY, 0f);
		}
		else
			SetSpawnPos(subDungeon.left);
	}

	void SetExitPos(SubDungeon subDungeon) {
		if (subDungeon == null)
			return;

		if (subDungeon.IAmLeaf()) {
			int exitPosX, exitPosY;
			do {
				exitPosX = Random.Range((int)subDungeon.room.x, (int)subDungeon.room.xMax);
				exitPosY = Random.Range((int)subDungeon.room.y, (int)subDungeon.room.yMax);
			} while(_dungeonTiles[exitPosX, exitPosY] == 0);
			GameObject instance = Instantiate(ExitTile, new Vector3(exitPosX, exitPosY, 0f), Quaternion.identity) as GameObject;
			instance.transform.SetParent(Dungeon.transform);
		}
		else
			SetExitPos(subDungeon.right);
	}

	void SetupPlayerSpawn(SubDungeon rootSubDungeon) {
		SetSpawnPos(rootSubDungeon);
		Player.transform.position = _playerSpawnPos;
		SetExitPos(rootSubDungeon);
	}

	public void CreateDungeon() {
		System.DateTime start = System.DateTime.Now;
		SubDungeon rootSubDungeon = new SubDungeon(new Rect(DungeonPadding, DungeonPadding, DungeonRows, DungeonColumns));
		CreateBSP(rootSubDungeon);
		rootSubDungeon.CreateRoom();

		_dungeonFloorPositions = new GameObject[DungeonRows + (2 * DungeonPadding), DungeonColumns + (2 * DungeonPadding)];
		_dungeonTiles = new int[DungeonRows + (2 * DungeonPadding), DungeonColumns + (2 * DungeonPadding)];
		_bridgeTilesPos = new List<Vector2Int>();
		DrawRooms(rootSubDungeon);
		DrawBridges(rootSubDungeon);
		DrawCorridors(rootSubDungeon);
		DrawWaters();
		_bridgeTilesPos.Clear();		// deleting the list since it completes its purpose
		DrawWalls();
		SetupPlayerSpawn(rootSubDungeon);
		System.DateTime end = System.DateTime.Now;
		Debug.Log("Dungeon Creation Time: " + end.Subtract(start).Milliseconds);
	}
}
