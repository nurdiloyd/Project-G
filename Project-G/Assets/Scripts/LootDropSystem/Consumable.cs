﻿using UnityEngine;

[CreateAssetMenu(fileName = "New Consumable", menuName = "Scriptable Objects/Consumable", order = 1)]
public class Consumable : ScriptableObject {
    public GameConfigData.CollectibleType Type;
    public int Value;
    public Sprite Sprite;

}
