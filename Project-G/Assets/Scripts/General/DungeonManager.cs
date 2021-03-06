﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DungeonManager : MonoBehaviour
{
	private enum Tiles { Bridges, Corridors, Floors, Walls, Waters }
	private enum Objects { Enemies = 5, Traps, Lamps, Chests }
	private enum LampObjectsTypes { Lamp, CableH, LampCableH, CableV, LampCableV }

	private struct SpawnData { 
		int max, current;

		public SpawnData(int max, int current) {
			this.max = max;
			this.current = current;
		}
		public int Max { get { return max; } }
		public int Current { get { return current; } set { current = value; } }
	}

	private GameManager _gameManager;
	public PlayerManager Player;
	public GameObject Dungeon;
	public GameObject[] Enemies;
	private GameObject[,] _dungeonFloorPositions;
	private int[,] _dungeonTiles;		// the tiles that players and other NPCs can walk on
	private int[,] _objectSpawnPos;
	private int[,] _enemyIndexes;
	private ExitController _exitDoor;
	List<Vector2Int> _bridgeTilesPos;
	private SpawnData _enemySpawnData = new SpawnData(25, 0);
	private SpawnData _trapSpawnData = new SpawnData(12, 0);
	private Vector3 _playerSpawnPos;
	private Vector3 _randomPos;
	private SubDungeon _rootSubDungeon;
	private Vector3 _invalidPos = new Vector3(0, 0, 1);

	public int[,] DungeonMap { get {return _dungeonTiles;} }


	public class SubDungeon {
		public SubDungeon left, right;
		public Rect rect;
		public Rect room = new Rect(-1, -1, 0, 0);     // null
		public Rect removedPiece = new Rect(-1, -1, 0, 0);     // null
		public List<Rect> corridors = new List<Rect>();
		public List<Rect> bridges = new List<Rect>();
		public int debugId;
		public bool hasTurret;

		private static int debugCounter = 0;

		public SubDungeon(Rect mrect) {
			rect = mrect;
			debugId = debugCounter;
			debugCounter++;
			hasTurret = false;
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

				int shouldEditRoom = Random.Range(0,4);       // 25% chance for editing the shape of the room
				if (shouldEditRoom == 0) {
					removedPiece = GetPieceToRemove();
				}
			}
		}

		private Rect GetPieceToRemove() {
			int x = 0, y = 0, xMax = 0, yMax = 0;
			int randWidth = Random.Range(1, (int)(room.width / 2));
			int randHeight = Random.Range(1, (int)(room.height / 2));
			int randCorner = Random.Range(0,4);
			switch (randCorner) {
				case 0:     // bottom left
					x = (int)room.x;
					y = (int)room.y;
					break;
				case 1:     // bottom right
					x = (int)room.xMax - randWidth;
					y = (int)room.y;
					break;
				case 2:     // top left
					x = (int)room.x;
					y = (int)room.yMax - randHeight;
					break;
				case 3:     // top right
					x = (int)room.xMax - randWidth;
					y = (int)room.yMax - randHeight;
					break;
			}
			xMax = x + randWidth;
			if(xMax > room.xMax)
				xMax = (int)room.xMax;
			yMax = y + randHeight;
			if (yMax > room.yMax)
				yMax = (int)room.yMax;

			return new Rect(x, y, xMax - x, yMax - y);
		}

		public void CreateCorridorBetween(SubDungeon left, SubDungeon right) {
			Rect lroom = left.GetRoom();
			Rect rroom = right.GetRoom();

			//Debug.Log("Creating corridor(s) between " + left.debugId + "(" + lroom + ") and " + right.debugId + " (" + rroom + ")");

			// attach the corridor to a random point in each room
			Vector2 lpoint, rpoint;

			do {
				lpoint = new Vector2((int)Random.Range(lroom.x + 1, lroom.xMax - 1), (int)Random.Range(lroom.y + 1, lroom.yMax - 1));
			} while (lpoint.x >= left.removedPiece.x && lpoint.x <= left.removedPiece.xMax && lpoint.y >= left.removedPiece.y && lpoint.y <= left.removedPiece.yMax);

			do {
				rpoint = new Vector2((int)Random.Range(rroom.x + 1, rroom.xMax - 1), (int)Random.Range(rroom.y + 1, rroom.yMax - 1));
			} while (rpoint.x >= right.removedPiece.x && rpoint.x <= right.removedPiece.xMax && rpoint.y >= right.removedPiece.y && rpoint.y <= right.removedPiece.yMax);

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
				Rect rroom = right.GetRoom();
				if (rroom.x != -1)
					return rroom;
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


	private void Update() {
		if (Player.HasKey && !_exitDoor.IsDoorOpen) {
			_exitDoor.OpenTheDoor();
		}
	}

	private void CreateBSP(SubDungeon subDungeon) {
		//Debug.Log("Splitting sub-dungeon " + subDungeon.debugId + ": " + subDungeon.rect);
		if (subDungeon.IAmLeaf()) {
			// if the subdungeon is too large
			if (subDungeon.rect.width > GameConfigData.Instance.MaxRoomSize || subDungeon.rect.height > GameConfigData.Instance.MaxRoomSize || Random.Range(0.0f, 1.0f) > 0.25) {
				if (subDungeon.Split (GameConfigData.Instance.MinRoomSize, GameConfigData.Instance.MaxRoomSize)) {
					//Debug.Log ("Splitted sub-dungeon " + subDungeon.debugId + " in " + subDungeon.left.debugId + ": " + subDungeon.left.rect + ", "
					//+ subDungeon.right.debugId + ": " + subDungeon.right.rect);

					CreateBSP(subDungeon.left);
					CreateBSP(subDungeon.right);
				}
			}
		}
	}

	private void DrawRooms(SubDungeon subDungeon) {
		if (subDungeon == null)
			return;

		if (subDungeon.IAmLeaf()) {
			for (int i = (int)subDungeon.room.x; i < subDungeon.room.xMax; i++) {
				for (int j = (int)subDungeon.room.y; j < subDungeon.room.yMax; j++) {
					if (!(i >= subDungeon.removedPiece.x && i <= subDungeon.removedPiece.xMax && j >= subDungeon.removedPiece.y && j <= subDungeon.removedPiece.yMax)) {
						GameObject instance = Instantiate(GameConfigData.Instance.FloorTiles[(int)Random.Range(0, GameConfigData.Instance.FloorTiles.Length)], new Vector3(i, j, 0f), Quaternion.identity) as GameObject;
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

	private void DrawCorridors(SubDungeon subDungeon) {
		if (subDungeon == null)
			return;
		
		DrawCorridors(subDungeon.left);
		DrawCorridors(subDungeon.right);

		foreach (Rect corridor in subDungeon.corridors) {
			for (int i = (int)corridor.x; i < corridor.xMax; i++) {
				for (int j = (int)corridor.y; j < corridor.yMax; j++) {
					if (_dungeonFloorPositions[i, j] == null) {
						GameObject instance = Instantiate(GameConfigData.Instance.FloorTiles[(int)Random.Range(0, GameConfigData.Instance.FloorTiles.Length)], new Vector3 (i, j, 0f), Quaternion.identity) as GameObject;
						instance.transform.SetParent(Dungeon.transform.GetChild((int)Tiles.Corridors).gameObject.transform);
						_dungeonFloorPositions[i, j] = instance;
						_dungeonTiles[i, j] = 1;
					}
				}
			}
		} 
	}
	
	private void DetermineBridges(SubDungeon subDungeon) {
		if (subDungeon == null)
			return;
		
		DetermineBridges(subDungeon.left);
		DetermineBridges(subDungeon.right);

		foreach (Rect bridge in subDungeon.bridges) {
			for (int i = (int)bridge.x; i < bridge.xMax; i++) {
				for (int j = (int)bridge.y; j < bridge.yMax; j++) {
					if (_dungeonFloorPositions[i, j] == null) {
						_bridgeTilesPos.Add(new Vector2Int(i, j));
						_dungeonTiles[i, j] = -1;
					}
				}
			}
		}
	}

	private void DrawBridges() {
		int index;

		int [,] kernelMatrix = {{0, 1, 0}, {8, 0, 2}, {0, 4, 0}};

		foreach (var bridgePos in _bridgeTilesPos) {
			index = 0;
			for (int j = 1; j >= -1; j--) {
				for (int i = -1; i <= 1; i++) {
					index += Mathf.Abs(_dungeonTiles[bridgePos.x + i, bridgePos.y + j]) * kernelMatrix[1 - j, i + 1];
				}
			}
			if (_dungeonFloorPositions[bridgePos.x, bridgePos.y] == null) {
				GameObject instance = Instantiate(GameConfigData.Instance.BridgeTiles[index], new Vector3 (bridgePos.x, bridgePos.y, 0f), Quaternion.identity) as GameObject;
				instance.transform.SetParent(Dungeon.transform.GetChild((int)Tiles.Bridges).gameObject.transform);
				_dungeonFloorPositions[bridgePos.x, bridgePos.y] = instance;
			}
		}
	}

	private void DrawWalls() {
		int matrixSize = 3, index;

		int [,] kernelMatrix = {{4, 64, 2}, {128, 0, 32}, {8, 16, 1}};
		
		for (int j = GameConfigData.Instance.DungeonColumns + (2 * GameConfigData.Instance.DungeonPadding) - matrixSize; j >= 0; j--) {
			for (int i = 0; i <= GameConfigData.Instance.DungeonRows + (2 * GameConfigData.Instance.DungeonPadding) - matrixSize; i++) {
				index = 0;
				for (int l = 0; l < matrixSize; l++) {
					for (int k = 0; k < matrixSize; k++) {
						index += Mathf.Abs(_dungeonTiles[i + k, j + l]) * kernelMatrix[l, k];
					}
				}

				GameObject instance = null;
				int wallPosX = i + 1, wallPosY = j + 1;
				if (_dungeonFloorPositions[wallPosX, wallPosY] == null && _dungeonTiles[wallPosX, wallPosY] == 0) {
					instance = Instantiate(GameConfigData.Instance.WallTiles[index], new Vector3 (wallPosX, wallPosY, 0f), Quaternion.identity) as GameObject;
					instance.transform.SetParent(Dungeon.transform.GetChild((int)Tiles.Walls).gameObject.transform);
					_dungeonFloorPositions[wallPosX, wallPosY] = instance;

					if (index != 0) {		// placing floor tile under the walls
						instance = Instantiate(GameConfigData.Instance.FloorTiles[(int)Random.Range(0, GameConfigData.Instance.FloorTiles.Length)], new Vector3 (wallPosX, wallPosY, 0f), Quaternion.identity) as GameObject;
						instance.transform.SetParent(Dungeon.transform.GetChild((int)Tiles.Floors).gameObject.transform);
					}
				}
			}
		}
	}

	private void DrawWaters() {
		_bridgeTilesPos = _bridgeTilesPos.OrderByDescending(pos => pos.y).ToList();
		foreach (var bridgePos in _bridgeTilesPos) {
			for (int j = 1; j >= -1; j--) {
				for (int i = -1; i <= 1; i++) {
					if (_dungeonFloorPositions[bridgePos.x + i, bridgePos.y + j] == null || (i == 0 && j == 0)) {
						GameObject instance;
						if (_dungeonTiles[bridgePos.x + i, bridgePos.y + j + 1] != -1)
							instance = Instantiate(GameConfigData.Instance.WaterTiles[0], new Vector3 (bridgePos.x + i, bridgePos.y + j, 0f), Quaternion.identity) as GameObject;
						else
							instance = Instantiate(GameConfigData.Instance.WaterTiles[1], new Vector3 (bridgePos.x + i, bridgePos.y + j, 0f), Quaternion.identity) as GameObject;
						instance.transform.SetParent(Dungeon.transform.GetChild((int)Tiles.Waters).gameObject.transform);
						if (i == 0 && j == 0)
							instance.gameObject.GetComponent<BoxCollider2D>().enabled = false;
						_dungeonFloorPositions[bridgePos.x + i, bridgePos.y + j] = instance;
						_dungeonTiles[bridgePos.x + i, bridgePos.y + j] = -1;
					}
				}
			}
		}
		foreach (var bridgePos in _bridgeTilesPos) {
			_dungeonTiles[bridgePos.x, bridgePos.y] = 1;
		}
	}

	private void PlaceLamps(int[,] dungeonTiles) {
		int[,] lightTiles = dungeonTiles.Clone() as int[,];
		int matrixSize = 3, mulResult;
		int [,] kernelMatrix = {{1, 1, 1}, {1, 1, 1}, {1, 1, 1}};

		for (int j = GameConfigData.Instance.DungeonColumns + (2 * GameConfigData.Instance.DungeonPadding) - matrixSize; j >= 0; j--) {
			for (int i = 0; i <= GameConfigData.Instance.DungeonRows + (2 * GameConfigData.Instance.DungeonPadding) - matrixSize; i++) {
				mulResult = 0;
				for (int l = 0; l < matrixSize; l++) {
					for (int k = 0; k < matrixSize; k++) {
						mulResult += Mathf.Abs(lightTiles[i + k, j + l]) * kernelMatrix[l, k];
					}
				}

				if (mulResult >= 6) {
					GameObject lamp = Instantiate(GameConfigData.Instance.LampObjects[(int)LampObjectsTypes.Lamp], new Vector3(i + 1, j + 1, 0f), Quaternion.identity) as GameObject;
					lamp.transform.SetParent(Dungeon.transform.GetChild((int)Objects.Lamps).gameObject.transform);

					for (int l = 0; l < matrixSize; l++) {
						for (int k = 0; k < matrixSize; k++) {
							lightTiles[i + k, j + l] = 0;
						}
					}
				}
			}
		}
	}

	public void CreateDungeon(GameManager gameManager) {
		//Debug.Log("Creating dungeon...");
		_gameManager = gameManager; // assigning Game Manager
		_rootSubDungeon = new SubDungeon(new Rect(GameConfigData.Instance.DungeonPadding, GameConfigData.Instance.DungeonPadding, GameConfigData.Instance.DungeonRows, GameConfigData.Instance.DungeonColumns));
		CreateBSP(_rootSubDungeon);
		_rootSubDungeon.CreateRoom();

		_dungeonFloorPositions = new GameObject[GameConfigData.Instance.DungeonRows + (2 * GameConfigData.Instance.DungeonPadding), GameConfigData.Instance.DungeonColumns + (2 * GameConfigData.Instance.DungeonPadding)];
		_dungeonTiles = new int[GameConfigData.Instance.DungeonRows + (2 * GameConfigData.Instance.DungeonPadding), GameConfigData.Instance.DungeonColumns + (2 * GameConfigData.Instance.DungeonPadding)];
		_objectSpawnPos = new int[GameConfigData.Instance.DungeonRows + (2 * GameConfigData.Instance.DungeonPadding), GameConfigData.Instance.DungeonColumns + (2 * GameConfigData.Instance.DungeonPadding)];
		_bridgeTilesPos = new List<Vector2Int>();
		
		DrawRooms(_rootSubDungeon);
		DrawCorridors(_rootSubDungeon);
		DetermineBridges(_rootSubDungeon);
		DrawBridges();
		DrawWaters();
		_bridgeTilesPos.Clear();		// deleting the list since it completes its purpose
		DrawWalls();
		PlaceLamps(_dungeonTiles);
		_enemyIndexes = new int[,] {{0, 1}, {0, 2}, {1, 2}, {0, 3}, {1, 3}, {1, 4}, {1, 5}};		// start and end indexes of Enemies array accorcding to the dungeon level
		//Debug.Log("Dungeon creation ended.");
	}

	public void SpawnEverything(int dungeonLevel) {
		PlayerSpawner();
		RandomEnemySpawner(dungeonLevel);
		RandomTrapSpawner(dungeonLevel);
		RandomChestSpawner(dungeonLevel);

		_objectSpawnPos = null;		// after the spawning everything, set it to null
	}

	private void PlayerSpawner() {
		//Debug.Log("Spawning player...");
		GetRandomPos(_rootSubDungeon);		// getting random position in the dungeon for the player
		Player.transform.position = _randomPos;
		_playerSpawnPos = _randomPos;
		_objectSpawnPos[(int)_randomPos.x, (int)_randomPos.y] = 1;

		GetRandomPos(_rootSubDungeon);		// getting random position in the dungeon for the exit
		_exitDoor = Instantiate(GameConfigData.Instance.ExitTile, new Vector3(_randomPos.x, _randomPos.y, 0f), Quaternion.identity).GetComponent<ExitController>();
		_exitDoor.transform.SetParent(Dungeon.transform);
		_exitDoor.GameManager = _gameManager;
		_objectSpawnPos[(int)_randomPos.x, (int)_randomPos.y] = 1;

		GetRandomPos(_rootSubDungeon);		// getting random position in the dungeon for the object
		GameObject key = Instantiate(GameConfigData.Instance.Key, new Vector3(_randomPos.x, _randomPos.y, 0f), Quaternion.identity) as GameObject;
		key.transform.SetParent(Dungeon.transform);
		_objectSpawnPos[(int)_randomPos.x, (int)_randomPos.y] = 1;
		//Debug.Log("Player spawn ended.");
	}

	private void RandomChestSpawner(int dungeonLevel) {
		//Debug.Log("Spawning chests...");
		// spawning barrels
		for (int i = 0; i < 3; i++) {
			GetRandomPos(_rootSubDungeon);		// getting random position in the dungeon
			GameObject barrel = Instantiate(GameConfigData.Instance.ItemChests[0], new Vector3(_randomPos.x, _randomPos.y, 0f), Quaternion.identity) as GameObject;
			barrel.transform.SetParent(Dungeon.transform.GetChild((int)Objects.Chests).gameObject.transform);
			_objectSpawnPos[(int)_randomPos.x, (int)_randomPos.y] = 1;
		}

		//spawning weapon chests if the level is the multiple of 2
		if (dungeonLevel % 2 == 1) {
			GetRandomPos(_rootSubDungeon);		// getting random position in the dungeon
			GameObject weaponChest = Instantiate(GameConfigData.Instance.ItemChests[1], new Vector3(_randomPos.x, _randomPos.y, 0f), Quaternion.identity) as GameObject;
			weaponChest.transform.SetParent(Dungeon.transform.GetChild((int)Objects.Chests).gameObject.transform);
			_objectSpawnPos[(int)_randomPos.x, (int)_randomPos.y] = 1;
		}
		//Debug.Log("Chest spawn ended.");
	}

	private void RandomEnemySpawner(int dungeonLevel) {
		//Debug.Log("Spawning enemies...");
		SpawnEnemies(_rootSubDungeon, dungeonLevel);
		// after creating copies, disable the original ones
		foreach (var enemy in Enemies) {
			enemy.SetActive(false);
		}
		//Debug.Log("Enemy spawn ended.");
	}

	private void SpawnEnemies(SubDungeon subDungeon, int dungeonLevel) {
		if (subDungeon == null)
			return;

		if (subDungeon.IAmLeaf()) {
			if (_enemySpawnData.Current <= _enemySpawnData.Max) {
				int minEnemyNumber = (int)((subDungeon.room.width * subDungeon.room.height) / 8);
				int enemyNumberForThisRoom = Random.Range(minEnemyNumber, minEnemyNumber + 1);
				for (int i = 0; i < enemyNumberForThisRoom; i++) {
					_randomPos = GetRandomPosInRoom(subDungeon.room);
					// if the randomPos != invalidPos, then spawn the object
					if (Vector3.Distance(_randomPos, _invalidPos) != 0) {
						int enemyIndex = 0;
						do {		// make sure that there is only one turret in a room
							enemyIndex = (int)Random.Range(_enemyIndexes[dungeonLevel, 0], _enemyIndexes[dungeonLevel, 1] + 1);
						} while (subDungeon.hasTurret && enemyIndex == 2);		// check if the room has a turret and new enemy is turret

						GameObject instance = Instantiate(Enemies[enemyIndex], _randomPos, Quaternion.identity) as GameObject;
						instance.transform.SetParent(Dungeon.transform.GetChild((int)Objects.Enemies).gameObject.transform);
						_enemySpawnData.Current++;
						_objectSpawnPos[(int)_randomPos.x, (int)_randomPos.y] = 1;
						if (enemyIndex == 2)
							subDungeon.hasTurret = true;
					}
				}
			}
		}
		else {
			SpawnEnemies(subDungeon.left, dungeonLevel);
			SpawnEnemies(subDungeon.right, dungeonLevel);
		}
	}

	private void RandomTrapSpawner(int dungeonLevel) {
		//Debug.Log("Spawning traps...");
		SpawnTraps(_rootSubDungeon, dungeonLevel);
		//Debug.Log("Trap spawn ended.");
	}

	private void SpawnTraps(SubDungeon subDungeon, int dungeonLevel) {
		if (subDungeon == null)
			return;

		if (subDungeon.IAmLeaf()) {
			if (_trapSpawnData.Current <= _trapSpawnData.Max) {
				int minTrapNumber = (int)((subDungeon.room.width * subDungeon.room.height) / 12);
				int trapNumberForThisRoom = Random.Range(minTrapNumber, minTrapNumber + 1);
				for (int i = 0; i < trapNumberForThisRoom; i++) {
					_randomPos = GetRandomPosInRoom(subDungeon.room);
					// if the randomPos != invalidPos, then spawn the object
					if (Vector3.Distance(_randomPos, _invalidPos) != 0) {
						int trapIndex = Random.Range(0, 2);

						GameObject instance = Instantiate(GameConfigData.Instance.Traps[trapIndex], _randomPos, Quaternion.identity) as GameObject;
						instance.transform.SetParent(Dungeon.transform.GetChild((int)Objects.Traps).gameObject.transform);
						_trapSpawnData.Current++;
						_objectSpawnPos[(int)_randomPos.x, (int)_randomPos.y] = 1;
					}
				}
			}
		}
		else {
			SpawnTraps(subDungeon.left, dungeonLevel);
			SpawnTraps(subDungeon.right, dungeonLevel);
		}
	}

	// Utility functions
	/// <summary> Gets a random position in the whole dungeon </summary>
	private void GetRandomPos(SubDungeon subDungeon) {
		if (subDungeon == null)
			return;

		if (subDungeon.IAmLeaf()) {
			int randPosX, randPosY, findingPosAttempt = 0, maxAttemptLimit = 500;
			do {
				randPosX = Random.Range((int)subDungeon.room.x, (int)subDungeon.room.xMax);
				randPosY = Random.Range((int)subDungeon.room.y, (int)subDungeon.room.yMax);
				findingPosAttempt++;
			} while ((_dungeonTiles[randPosX, randPosY] != 1 || _objectSpawnPos[randPosX, randPosY] == 1) && findingPosAttempt <= maxAttemptLimit);
			_randomPos = new Vector3(randPosX, randPosY, 0);
		}
		else {
			if (Random.Range(0, 2) == 0)
				GetRandomPos(subDungeon.left);
			else
				GetRandomPos(subDungeon.right);
		}
	}
	/// <summary> Gets a random position in the given room </summary>
	private Vector3 GetRandomPosInRoom(Rect room) {
		int randPosX, randPosY, findingPosAttempt = 0, maxAttemptLimit = 500;
		do {
			randPosX = Random.Range((int)room.x, (int)room.xMax);
			randPosY = Random.Range((int)room.y, (int)room.yMax);
			findingPosAttempt++;
		} while ((_dungeonTiles[randPosX, randPosY] != 1 || _objectSpawnPos[randPosX, randPosY] == 1) && findingPosAttempt <= maxAttemptLimit);
		if (findingPosAttempt > maxAttemptLimit)	Debug.Log("Could not find a pos in the room.");
		return (findingPosAttempt <= maxAttemptLimit) ? new Vector3(randPosX, randPosY, 0) : _invalidPos;
	}
}
