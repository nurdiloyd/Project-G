﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConsumableObject : MonoBehaviour
{
    public Consumable Item;

    [SerializeField] private  SpriteRenderer _spriteRenderer = null;
    private PlayerManager _playerManager;

    void Start() {
        _spriteRenderer.sprite = Item.Sprite;
    }

    // Collision Controller
    private void OnTriggerEnter2D(Collider2D other) {
        // If collide with an Player, Subscribe the playerManager
		if (other.gameObject.CompareTag("Player") && !_playerManager){
            _playerManager = other.gameObject.GetComponent<PlayerManager>();
            _playerManager.CollectPUB += CollectSUB;
        }
	}

    // Collection Subscriber
    private void CollectSUB(Inventory[] inventory, HealthController playerHealthController) {
        bool collected = false;

        if(Item.Type == GameConfigData.CollectibleType.Snack){  // Snacks
            if(playerHealthController.Health < 100){            // If Health is not full
                playerHealthController.Heal(Item.Value);   
                collected = true;
            }
        }
        else if(inventory[(int)Item.Type].Count < 3) {      // Medkit or Shield, If inventory is not full
            inventory[(int)Item.Type].Count += 1;
            collected = true;
        }

        // If object is collected, destroy it
        if (collected == true){
            _playerManager.CollectPUB -= CollectSUB;
            Destroy(gameObject);
        }
    }
}
