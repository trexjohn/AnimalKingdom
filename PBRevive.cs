﻿using UnityEngine;
using Landfall.TABS;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using Unity.Entities;
using Landfall.TABS.AI;
using System.Reflection;
using Landfall.TABS.GameMode;
using System.Linq;
using Landfall.TABS.AI.Components;
using Landfall.TABS.AI.Components.Tags;
using Landfall.TABS.AI.Systems;

namespace AnimalKingdom
{
    public class PBRevive : MonoBehaviour
    {
        public void Start()
        {
            unit = transform.root.GetComponent<Unit>();
            eyeSpawner = unit.GetComponentInChildren<EyeSpawner>();
            plaguePhases = GetComponent<PlaguePhases>();
            unit.data.healthHandler.willBeRewived = true;
            
            if (unit.data.weaponHandler.rightWeapon != null && unit.data.weaponHandler.rightWeapon.GetComponent<Holdable>())
            {
                rightWeaponOriginal = unit.data.weaponHandler.rightWeapon.gameObject;
                if (!letGoOfWeapons) unit.data.weaponHandler.rightWeapon.GetComponent<Holdable>().ignoreDissarm = true;
            }
            if (unit.data.weaponHandler.leftWeapon != null && unit.data.weaponHandler.leftWeapon.GetComponent<Holdable>())
            {
                leftWeaponOriginal = unit.data.weaponHandler.leftWeapon.gameObject;
                if (!letGoOfWeapons) unit.data.weaponHandler.leftWeapon.GetComponent<Holdable>().ignoreDissarm = true;
            }
        }

        public void DoRevive()
        {
            if (plaguePhases.currentState == PlaguePhases.PlagueState.Sickly) StartCoroutine(Revival());
            
            else if (unit.data.healthHandler.willBeRewived)
            {
                Debug.Log("its doing what its supposed to");
                unit.data.healthHandler.willBeRewived = false;
                unit.data.healthHandler.Die();
                Destroy(this);
            }
        }

        public IEnumerator Revival()
        {
            var effect = unit.GetComponentsInChildren<UnitEffectBase>().ToList().Find(x => x.effectID == 1984 || x.effectID == 1987);
            if (unit.data.health > 0f || effect || !unit.data.healthHandler.willBeRewived)
            {
                Debug.Log("revive failed!");
                unit.data.healthHandler.willBeRewived = false;
                ServiceLocator.GetService<GameModeService>().CurrentGameMode.OnUnitDied(unit);
                Destroy(this);
                yield break;
            }
            
            Debug.Log("reviving!");

            preReviveEvent.Invoke();
            
            StartCoroutine(plaguePhases.PlayPartWithDelay(reviveDelay - 0.5f));
            
            yield return new WaitForSeconds(reviveDelay);
            
            unit.data.Dead = false;
            unit.dead = false;
            unit.data.hasBeenRevived = true;
            unit.data.healthHandler.willBeRewived = false;
            plaguePhases.currentState = PlaguePhases.PlagueState.Zombie;
            
            unit.data.ragdollControl = 1f;
            unit.data.muscleControl = 1f;
            
            unit.data.health = unit.data.maxHealth * reviveHealthMultiplier;

            if (letGoOfWeapons)
            {
                if (rightWeaponToSpawn)
                {
                    var weapon = unit.unitBlueprint.SetWeapon(unit, unit.Team, rightWeaponToSpawn, new PropItemData(), HoldingHandler.HandType.Right, unit.data.mainRig.rotation, new List<GameObject>());
                    weapon.rigidbody.mass *= unit.unitBlueprint.massMultiplier;
                    if (holdWithTwoHands)
                    {
                        unit.holdingHandler.leftHandActivity = HoldingHandler.HandActivity.HoldingRightObject;
                    }
                }
                else if (useWeaponsAfterRevive && rightWeaponOriginal)
                {
                    var weapon = unit.unitBlueprint.SetWeapon(unit, unit.Team, rightWeaponOriginal, new PropItemData(), HoldingHandler.HandType.Right, unit.data.mainRig.rotation, new List<GameObject>());
                    weapon.rigidbody.mass *= unit.unitBlueprint.massMultiplier;
                }
                if (!holdWithTwoHands)
                {
                    if (leftWeaponToSpawn)
                    {
                        var weapon = unit.unitBlueprint.SetWeapon(unit, unit.Team, leftWeaponToSpawn, new PropItemData(), HoldingHandler.HandType.Left, unit.data.mainRig.rotation, new List<GameObject>());
                        weapon.rigidbody.mass *= unit.unitBlueprint.massMultiplier;
                    }
                    else if (useWeaponsAfterRevive && leftWeaponOriginal)
                    {
                        var weapon = unit.unitBlueprint.SetWeapon(unit, unit.Team, leftWeaponOriginal, new PropItemData(), HoldingHandler.HandType.Left, unit.data.mainRig.rotation, new List<GameObject>());
                        weapon.rigidbody.mass *= unit.unitBlueprint.massMultiplier;
                    }
                }

                if (rightWeaponOriginal)
                {
                    rightWeaponOriginal.transform.SetParent(null);
                    if (removeWeaponsAfterSeconds > 0f)
                    {
                        var sec = rightWeaponOriginal.AddComponent<RemoveAfterSeconds>();
                        sec.shrink = true;
                        sec.seconds = removeWeaponsAfterSeconds;
                    }
                    else if (removeWeaponsAfterSeconds < 0f) Destroy(rightWeaponOriginal);
                }
                if (leftWeaponOriginal)
                {
                    leftWeaponOriginal.transform.SetParent(null);
                    if (removeWeaponsAfterSeconds > 0f)
                    {
                        var sec = leftWeaponOriginal.AddComponent<RemoveAfterSeconds>();
                        sec.shrink = true;
                        sec.seconds = removeWeaponsAfterSeconds;
                    }
                    else if (removeWeaponsAfterSeconds < 0f) Destroy(leftWeaponOriginal);
                }
            }
            
            
            if (openEyes && eyeSpawner && eyeSpawner.spawnedEyes != null) 
            {
                foreach (var eye in eyeSpawner.spawnedEyes) 
                {
                    eye.dead.SetActive(false);
                    eye.currentEyeState = GooglyEye.EyeState.Open;
                    eye.SetState(GooglyEye.EyeState.Open);
                    GooglyEyes.instance.AddEye(eye);
                }
            }
            
            if (unit.unitBlueprint.MovementComponents != null && unit.unitBlueprint.MovementComponents.Count > 0)
            {
                foreach (var mov in unit.unitBlueprint.MovementComponents)
                {
                    var mi = (MethodInfo)typeof(UnitAPI).GetMethod("CreateGenericRemoveComponentData", (BindingFlags)(-1)).Invoke(unit.api, new object[] { mov.GetType() });
                    mi.Invoke(unit.GetComponent<GameObjectEntity>().EntityManager, new object[] { unit.GetComponent<GameObjectEntity>().Entity });
                }
            }
            
            unit.data.healthHandler.deathEvent.RemoveAllListeners();
            foreach (var rigidbodyOnDeath in unit.GetComponentsInChildren<AddRigidbodyOnDeath>()) {

                unit.data.healthHandler.RemoveDieAction(rigidbodyOnDeath.Die);
            }
            foreach (var deathEvent in unit.GetComponentsInChildren<DeathEvent>()) {

                unit.data.healthHandler.RemoveDieAction(deathEvent.Die);
            }
            
            ServiceLocator.GetService<UnitHealthbars>().HandleUnitSpawned(unit);
            unit.api.SetTargetingType(unit.unitBlueprint.TargetingComponent);
            unit.api.UpdateECSValues();
            unit.InitializeUnit(unit.Team);

            reviveEvent.Invoke();
            plaguePhases.SetRenderer();

            plaguePhases.part.Play();

            Destroy(this);
        }

        private Unit unit;

        private EyeSpawner eyeSpawner;

        private PlaguePhases plaguePhases;
        
        [Header("Revive Settings")]

        public UnityEvent preReviveEvent = new UnityEvent();

        public UnityEvent reviveEvent = new UnityEvent();

        public float reviveDelay = 4f;
        
        [Range(0f, 1f)]
        public float reviveHealthMultiplier = 0.5f;

        public bool openEyes = true;

        [Header("Weapon Settings")] 
        
        public bool letGoOfWeapons;

        public bool useWeaponsAfterRevive = true;
        
        public GameObject rightWeaponToSpawn;
        
        public GameObject leftWeaponToSpawn;

        private GameObject rightWeaponOriginal;
        
        private GameObject leftWeaponOriginal;

        public bool holdWithTwoHands;

        public float removeWeaponsAfterSeconds;
    }
}