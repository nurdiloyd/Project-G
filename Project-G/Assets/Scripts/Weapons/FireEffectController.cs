﻿using System.Collections;
using UnityEngine;

public class FireEffectController : MonoBehaviour {

    void Start() {
      StartCoroutine(TurnOffLight());
    }

    IEnumerator TurnOffLight() {
		  yield return new WaitForSeconds(0.05f);
		  Destroy(gameObject);
    }
}
