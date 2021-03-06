﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Inventory {
    public GameConfigData.CollectibleType Type;
    public Consumable Item;
    public int Count;
};

public class PlayerManager : MonoBehaviour
{
    public PlayerController PlayerController;
    public PlayerHandController PlayerHandController;
    public HealthController HealthController;
    [SerializeField] private GameObject _shield = null;

    [SerializeField] private Inventory[] _inventory = null;         // Inventory
    public event Action<Inventory[], HealthController> CollectPUB;  // Item collection Publisher
    private bool _hasKey = false;
    private float _shieldTime;

    public Inventory[] Inventory { get { return _inventory; } }     // Getter for Inventory
    public bool HasKey { get { return _hasKey; } }      // Getter for Key

    // Update is called once per frame
    private void Update() {
        if (Input.GetKeyDown (KeyCode.Alpha1)) {
            UseMedkit();
        }

        if (Input.GetKeyDown (KeyCode.Alpha2) && !_shield.activeSelf) {
            UseShield();
        }

        if (_shield.activeSelf) {
            _shieldTime -=Time.deltaTime;
            if (_shieldTime <= 0) {
                _shield.SetActive(false);
            }
        }
    }

    // Using Medkit
    private void UseMedkit() {
        Inventory inventory = _inventory[(int)GameConfigData.CollectibleType.Medkit]; 
        if (inventory.Count > 0 && HealthController.Health < 100){
            inventory.Count -= 1;
            HealthController.Heal(inventory.Item.Value);
        }
    }

    // Using Medkit
    private void UseShield() {
        Inventory inventory = _inventory[(int)GameConfigData.CollectibleType.Shield]; 
        if (inventory.Count > 0) {
            inventory.Count -= 1;
            _shield.SetActive(true);
            _shieldTime = inventory.Item.Value;
        }
    }

    public void LoadPlayerData() {
        HealthController.Health = DataManager.Instance.Health;
        _inventory[(int)GameConfigData.CollectibleType.Medkit].Count = DataManager.Instance.Medkits;
        _inventory[(int)GameConfigData.CollectibleType.Shield].Count = DataManager.Instance.Shields;

        GameObject weapon = Instantiate(GameConfigData.Instance.Weapons[DataManager.Instance.WeaponID]) as GameObject;		// instantiating player's weapon
		PlayerHandController.EquipWeapon(weapon.GetComponent<WeaponBase>());
    }

    public void SavePlayerData() {
        DataManager.Instance.Health = HealthController.Health;		// storing player's health
        DataManager.Instance.Medkits = _inventory[(int)GameConfigData.CollectibleType.Medkit].Count;
        DataManager.Instance.Shields = _inventory[(int)GameConfigData.CollectibleType.Shield].Count; 
		DataManager.Instance.WeaponID = PlayerHandController.transform.GetChild(0).gameObject.GetComponent<WeaponPrefab>().ID;			// storing player's weapon
	}

    private void OnTriggerEnter2D(Collider2D other) {
        // If collide with an consumable item
		if (other.gameObject.CompareTag("Item")){
            // If there are items
            CollectPUB?.Invoke(_inventory, HealthController);
        }
        else if (other.gameObject.CompareTag("Key")){
            // If there are Key
            _hasKey = true;
            other.gameObject.SetActive(false);
        }
	}
}
