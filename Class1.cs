using System.Collections.Generic;
using System.Collections;
using ThunderRoad;
using UnityEngine;

namespace MeteorFall
{
    public class MeteorSpell : SpellCastProjectile
    {
        public override void Fire(bool active)
        {
            base.Fire(active);
            if (active)
            {
                spellCaster.SetMagicOffset(new Vector3(-0.04f, 0.2f, -0.07f));
            }
        }
        public override void Throw(Vector3 velocity)
        {
            base.Throw(velocity);
            guidedProjectile.OnProjectileCollisionEvent += GuidedProjectile_OnProjectileCollisionEvent;
            guidedProjectile.item.disallowDespawn = true;
        }

        private void GuidedProjectile_OnProjectileCollisionEvent(ItemMagicProjectile projectile, CollisionInstance collisionInstance)
        {
            Catalog.GetData<ItemData>("MeteorFall").SpawnAsync(meteor =>
            {
                meteor.gameObject.AddComponent<MeteorComponent>().creature = spellCaster.mana.creature;
                meteor.physicBody.AddForce(Vector3.down * 500, ForceMode.VelocityChange);
                meteor.Throw();
            }, new Vector3(collisionInstance.contactPoint.x, collisionInstance.contactPoint.y + 2000, collisionInstance.contactPoint.z));
            projectile.OnProjectileCollisionEvent -= GuidedProjectile_OnProjectileCollisionEvent;
        }
    }
    public class MeteorComponent : MonoBehaviour
    {
        Item item;
        public Creature creature;
        public void Awake()
        {
            item = GetComponent<Item>();
            item.mainCollisionHandler.OnCollisionStartEvent += MainCollisionHandler_OnCollisionStartEvent;
            item.disallowDespawn = true;
        }
        private void MainCollisionHandler_OnCollisionStartEvent(CollisionInstance collisionInstance)
        {
            item.mainCollisionHandler.OnCollisionStartEvent -= MainCollisionHandler_OnCollisionStartEvent;
            StartCoroutine(Impact(collisionInstance.contactPoint, collisionInstance.contactNormal, collisionInstance.sourceColliderGroup.transform.up));
            item.Hide(true);
            item.colliderGroups[0].colliders[0].isTrigger = true;
            item.physicBody.velocity = Vector3.zero;
            item.physicBody.rigidBody.Sleep();
            item.disallowDespawn = false;
        }
        private IEnumerator Impact(Vector3 contactPoint, Vector3 contactNormal, Vector3 contactNormalUpward)
        {
            EffectInstance effectInstance = Catalog.GetData<EffectData>("MeteorShockwave").Spawn(contactPoint, Quaternion.LookRotation(-contactNormal, contactNormalUpward));
            effectInstance.SetIntensity(100f);
            effectInstance.Play();
            Collider[] sphereContacts = Physics.OverlapSphere(contactPoint, 100, 218119169);
            List<Creature> creaturesPushed = new List<Creature>();
            List<Rigidbody> rigidbodiesPushed = new List<Rigidbody>();
            rigidbodiesPushed.Add(item.physicBody.rigidBody);
            creaturesPushed.Add(creature);
            float waveDistance = 0.0f;
            yield return new WaitForEndOfFrame();
            while (waveDistance < 100)
            {
                waveDistance += 70f * 0.1f;
                foreach(Creature creature in Creature.allActive)
                {
                    if(!creature.isKilled && Vector3.Distance(contactPoint, creature.transform.position) < waveDistance && !creaturesPushed.Contains(creature))
                    {
                        CollisionInstance collision = new CollisionInstance(new DamageStruct(DamageType.Energy, 200 - (Vector3.Distance(contactPoint, creature.transform.position) * 2)));
                        collision.damageStruct.hitRagdollPart = creature.ragdoll.rootPart;
                        creature.Damage(collision);
                        if (!creature.isPlayer)
                            creature.ragdoll.SetState(Ragdoll.State.Destabilized);
                        if (item?.lastHandler?.creature != null)
                        {
                            creature.lastInteractionTime = Time.time;
                            creature.lastInteractionCreature = item.lastHandler.creature;
                        }
                        creature.gameObject.AddComponent<MeteorBurning>();
                        creaturesPushed.Add(creature);
                    }
                }
                foreach (Collider collider in sphereContacts)
                {
                    Breakable breakable = collider.attachedRigidbody?.GetComponentInParent<Breakable>();
                    if (breakable != null && Vector3.Distance(contactPoint, collider.transform.position) < waveDistance)
                    {
                        if(!breakable.IsBroken && breakable.canInstantaneouslyBreak)
                        breakable.Break();
                        for (int index = 0; index < breakable.subBrokenItems.Count; ++index)
                        {
                            Rigidbody rigidBody = breakable.subBrokenItems[index].physicBody.rigidBody;
                            if (rigidBody && !rigidbodiesPushed.Contains(rigidBody))
                            {
                                rigidBody.AddExplosionForce(75, contactPoint, 100, 0f, ForceMode.VelocityChange);
                                rigidbodiesPushed.Add(rigidBody);
                            }
                        }
                        for (int index = 0; index < breakable.subBrokenBodies.Count; ++index)
                        {
                            PhysicBody subBrokenBody = breakable.subBrokenBodies[index];
                            if (subBrokenBody && !rigidbodiesPushed.Contains(subBrokenBody.rigidBody))
                            {
                                subBrokenBody.rigidBody.AddExplosionForce(75, contactPoint, 100, 0f, ForceMode.VelocityChange);
                                rigidbodiesPushed.Add(subBrokenBody.rigidBody);
                            }
                        }
                    }
                    if (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic && Vector3.Distance(contactPoint, collider.transform.position) < waveDistance)
                    {
                        if (collider.attachedRigidbody.gameObject.layer != GameManager.GetLayer(LayerName.NPC) && !rigidbodiesPushed.Contains(collider.attachedRigidbody))
                        {
                            collider.attachedRigidbody.AddExplosionForce(75, contactPoint, 100, 0.5f, ForceMode.VelocityChange);
                            rigidbodiesPushed.Add(collider.attachedRigidbody);
                        }
                    }
                }
                yield return new WaitForSeconds(0.05f);
            }
            item.Despawn();
        }
    }
    public class MeteorBurning : MonoBehaviour
    {
        Creature creature;
        EffectInstance instance;
        float timer;
        public void Start()
        {
            creature = GetComponent<Creature>();
            instance = Catalog.GetData<EffectData>("MeteorRagdoll").Spawn(creature.ragdoll.rootPart.transform, null, true);
            instance.SetRenderer(creature.GetRendererForVFX(), false);
            instance.SetIntensity(1f);
            instance.Play();
            timer = Time.time;
        }
        public void FixedUpdate()
        {
            if (Time.time - timer >= 10f)
            {
                instance.Stop();
                Destroy(this);
            }
            else if (!creature.isKilled)
            {
                CollisionInstance collision = new CollisionInstance(new DamageStruct(DamageType.Energy, 5 * Time.fixedDeltaTime));
                collision.damageStruct.hitRagdollPart = creature.ragdoll.rootPart;
                creature.Damage(collision);
            }
        }
    }
}
