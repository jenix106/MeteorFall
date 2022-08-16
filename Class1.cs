using System.Collections.Generic;
using System.Collections;
using ThunderRoad;
using UnityEngine;

namespace MeteorFall
{
    public class MeteorProjectile : ItemModule
    {
        public override void OnItemLoaded(Item item)
        {
            base.OnItemLoaded(item);
            item.gameObject.AddComponent<MeteorProjectileComponent>();
        }
    }
    public class MeteorProjectileComponent : MonoBehaviour
    {
        Item item;
        public void Start()
        {
            item = GetComponent<Item>();
            item.mainCollisionHandler.OnCollisionStartEvent += MainCollisionHandler_OnCollisionStartEvent;
        }

        private void MainCollisionHandler_OnCollisionStartEvent(CollisionInstance collisionInstance)
        {
            collisionInstance.ignoreDamage = true;
            Catalog.GetData<ItemData>("MeteorFall").SpawnAsync(meteor =>
            {
                meteor.gameObject.AddComponent<MeteorComponent>();
                //meteor.transform.position = new Vector3(collisionInstance.contactPoint.x, collisionInstance.contactPoint.y + 300, collisionInstance.contactPoint.z);
                meteor.Throw(20);
            }, new Vector3(collisionInstance.contactPoint.x, collisionInstance.contactPoint.y + 200, collisionInstance.contactPoint.z));
        }
    }
    public class MeteorComponent : MonoBehaviour
    {
        Item item;
        EffectInstance instance;
        public void Start()
        {
            item = GetComponent<Item>();
            item.mainCollisionHandler.OnCollisionStartEvent += MainCollisionHandler_OnCollisionStartEvent;
            instance = Catalog.GetData<EffectData>("ImbueFire").Spawn(item.transform, true);
            instance.SetRenderer(item.renderers[0], false);
            instance.SetIntensity(50);
            instance.Play();
        }
        private void MainCollisionHandler_OnCollisionStartEvent(CollisionInstance collisionInstance)
        {
            StartCoroutine(Impact(collisionInstance.contactPoint, collisionInstance.contactNormal, collisionInstance.sourceColliderGroup.transform.up));
            instance.Stop();
            item.mainCollisionHandler.OnCollisionStartEvent -= MainCollisionHandler_OnCollisionStartEvent;
        }
        private IEnumerator Impact(Vector3 contactPoint, Vector3 contactNormal, Vector3 contactNormalUpward)
        {
            EffectInstance effectInstance = Catalog.GetData<EffectData>("MeteorShockwave").Spawn(contactPoint, Quaternion.LookRotation(-contactNormal, contactNormalUpward));
            effectInstance.SetIntensity(100);
            effectInstance.Play();
            Collider[] sphereContacts = Physics.OverlapSphere(contactPoint, 100, 218119169);
            List<Creature> creaturesPushed = new List<Creature>();
            List<Rigidbody> rigidbodiesPushed = new List<Rigidbody>();
            rigidbodiesPushed.Add(item.rb);
            creaturesPushed.Add(Player.local.creature);
            float waveDistance = 0.0f;
            yield return new WaitForEndOfFrame();
            while (waveDistance < 100)
            {
                waveDistance += 70f * 0.05f;
                foreach(Creature creature in Creature.allActive)
                {
                    if(!creature.isKilled && Vector3.Distance(contactPoint, creature.transform.position) < waveDistance && !creaturesPushed.Contains(creature))
                    {
                        CollisionInstance collision = new CollisionInstance(new DamageStruct(DamageType.Energy, 200 - (Vector3.Distance(contactPoint, creature.transform.position) * 2)));
                        collision.damageStruct.hitRagdollPart = creature.ragdoll.rootPart;
                        creature.Damage(collision);
                        creature.TryPush(Creature.PushType.Magic, (creature.ragdoll.rootPart.transform.position - contactPoint).normalized, 3); 
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
                    if (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic && Vector3.Distance(contactPoint, collider.transform.position) < waveDistance)
                    {
                        if (collider.attachedRigidbody.gameObject.layer != GameManager.GetLayer(LayerName.NPC) && !rigidbodiesPushed.Contains(collider.attachedRigidbody))
                        {
                            collider.attachedRigidbody.AddExplosionForce(50, contactPoint, 100, 0.5f, ForceMode.VelocityChange);
                            rigidbodiesPushed.Add(collider.attachedRigidbody);
                        }
                    }
                }
                yield return new WaitForSeconds(0.05f);
            }
            Destroy(this);
        }
    }
    public class MeteorBurning : MonoBehaviour
    {
        Creature creature;
        EffectInstance instance;
        float timer;
        float cooldown;
        public void Start()
        {
            creature = GetComponent<Creature>();
            instance = Catalog.GetData<EffectData>("MeteorRagdoll").Spawn(creature.transform, true);
            instance.SetRenderer(creature.GetRendererForVFX(), false);
            instance.SetIntensity(1f);
            instance.Play();
            timer = Time.time;
            cooldown = Time.time;
        }
        public void FixedUpdate()
        {
            if (Time.time - timer >= 10f)
            {
                instance.Stop();
                Destroy(this);
            }
            else if (Time.time - cooldown >= 1f && !creature.isKilled)
            {
                CollisionInstance collision = new CollisionInstance(new DamageStruct(DamageType.Energy, 5));
                collision.damageStruct.hitRagdollPart = creature.ragdoll.rootPart;
                cooldown = Time.time;
                creature.Damage(collision);
            }
        }
    }
}
