using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Attack", menuName = "Attack")]
public class AttackData : ScriptableObject
{
    public string attackName;
    public float damage;
    public float range;
    public float attackRate;
    //public bool isRanged;
    public GameObject projectilePrefab;
    public DamageFactor factor = DamageFactor.Force;
}