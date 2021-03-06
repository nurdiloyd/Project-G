﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurretController : HandControllerBase {
	public TurretController() {
		WeaponPosition = new Vector3(0f, 0.497f, 0f);
	}
	
	public HealthController HealthController;
	private bool _targetInRange = false;
	private int _environmentLayer = 9;
	private int _playerLayer = 10;
	private int _shieldLayer = 12;
	private LayerMask _hittableLayersByEnemy;

	public override void SpecialStart() {
		CurrentWeapon = transform.GetChild(0).GetComponent<WeaponBase>();
		CurrentWeapon.transform.localPosition = WeaponPosition;
        AimDeviation = 2f;
		_hittableLayersByEnemy = (1 << _playerLayer) | (1 << _environmentLayer) | (1 << _shieldLayer);
    }

	public override void SpecialUpdate() {
		CheckPlayerInRange();
	}
	
    public override void UseWeapon() {
        if (!HealthController.IsDead && _targetInRange) {
			CurrentWeapon.Trigger();
		}
		else {
			CurrentWeapon.ReleaseTrigger();
		}
    }

	private void CheckPlayerInRange() {
		if (TargetObject != null) {
			Vector3 direction = TargetObject.transform.position - transform.position;
			direction.y -= .23f;
			RaycastHit2D hitInfo = Physics2D.Raycast(transform.position, direction, 4f, _hittableLayersByEnemy);
			//Debug.DrawRay(transform.position, direction, Color.blue, .1f);
	
			if (hitInfo.collider != null) {
				_targetInRange = hitInfo.collider.tag == "Player" || hitInfo.collider.tag == "Shield";
			}
			else {
				_targetInRange = false;
			}
		}
		else {
			_targetInRange = false;
		}
	}
}
