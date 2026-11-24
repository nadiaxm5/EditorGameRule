using UnityEditor;
using UnityEngine;

namespace GameRuleEditor.Core
{
    /// <summary>
    /// Keeps actor physics inheritance and overrides consistent between the editor preview
    /// and scenes generated from JSON. Physics components are intentionally resolved only
    /// on the actor root, matching the runtime actions that operate on the root GameObject.
    /// </summary>
    public static class ActorPhysicsUtility
    {
        public const float DefaultFriction = 0.6f;
        public const float DefaultBounciness = 0f;
        public const float MinimumDensity = 0.0001f;

        public static GameObject LoadPrefab(ActorJson actor)
        {
            if (actor == null || string.IsNullOrWhiteSpace(actor.PrefabName))
                return null;

            return Resources.Load<GameObject>("Prefabs/" + actor.PrefabName);
        }

        /// <summary>
        /// JSON files created before explicit override flags existed used a non-zero value
        /// to mean "overridden". Preserve that behaviour when importing those files.
        /// </summary>
        public static bool NormalizeLegacyOverrides(ActorJson actor)
        {
            if (actor == null) return false;
            bool changed = false;

            if (!actor.OverrideDensity && !Mathf.Approximately(actor.Density, 0f))
            {
                actor.OverrideDensity = true;
                changed = true;
            }
            if (!actor.OverrideFriction && !Mathf.Approximately(actor.Friction, 0f))
            {
                actor.OverrideFriction = true;
                changed = true;
            }
            if (!actor.OverrideBounciness && !Mathf.Approximately(actor.Bounciness, 0f))
            {
                actor.OverrideBounciness = true;
                changed = true;
            }
            if (!actor.OverrideDrag && !Mathf.Approximately(actor.Drag, 0f))
            {
                actor.OverrideDrag = true;
                changed = true;
            }

            return changed;
        }

        public static bool HasOverrides(ActorJson actor)
        {
            return actor != null &&
                ((actor.Velocity != null && actor.Velocity.Length >= 3) ||
                 (actor.AngularVelocity != null && actor.AngularVelocity.Length >= 3) ||
                 actor.OverrideDensity || actor.OverrideFriction ||
                 actor.OverrideBounciness || actor.OverrideDrag);
        }

        public static void ClearOverrides(ActorJson actor)
        {
            if (actor == null) return;

            actor.Velocity = null;
            actor.AngularVelocity = null;
            actor.Density = 0f;
            actor.Friction = 0f;
            actor.Bounciness = 0f;
            actor.Drag = 0f;
            actor.OverrideDensity = false;
            actor.OverrideFriction = false;
            actor.OverrideBounciness = false;
            actor.OverrideDrag = false;
        }

        public static float GetFriction(Collider collider)
        {
            return collider != null && collider.sharedMaterial != null
                ? collider.sharedMaterial.dynamicFriction
                : DefaultFriction;
        }

        public static float GetBounciness(Collider collider)
        {
            return collider != null && collider.sharedMaterial != null
                ? collider.sharedMaterial.bounciness
                : DefaultBounciness;
        }

        /// <summary>Applies effective values (override or prefab default) to an actor instance.</summary>
        public static bool ApplyTo(GameObject target, ActorJson actor, GameObject prefab = null, bool recordUndo = false)
        {
            if (target == null || actor == null) return false;
            prefab ??= LoadPrefab(actor);

            bool changed = false;
            Rigidbody targetBody = target.GetComponent<Rigidbody>();
            Rigidbody prefabBody = prefab != null ? prefab.GetComponent<Rigidbody>() : null;
            if (targetBody != null && prefabBody != null)
            {
                Vector3 velocity = ToVector3(actor.Velocity, prefabBody.linearVelocity);
                Vector3 angularVelocity = ToVector3(actor.AngularVelocity, prefabBody.angularVelocity);
                float density = actor.OverrideDensity ? Mathf.Max(MinimumDensity, actor.Density) : prefabBody.mass;
                float drag = actor.OverrideDrag ? Mathf.Max(0f, actor.Drag) : prefabBody.linearDamping;

                if (Different(targetBody.linearVelocity, velocity) ||
                    Different(targetBody.angularVelocity, angularVelocity) ||
                    Different(targetBody.mass, density) ||
                    Different(targetBody.linearDamping, drag) ||
                    targetBody.interpolation == RigidbodyInterpolation.None)
                {
                    if (recordUndo) Undo.RecordObject(targetBody, "Apply Actor Physics");
                    targetBody.linearVelocity = velocity;
                    targetBody.angularVelocity = angularVelocity;
                    targetBody.mass = density;
                    targetBody.linearDamping = drag;
                    if (targetBody.interpolation == RigidbodyInterpolation.None)
                        targetBody.interpolation = RigidbodyInterpolation.Interpolate;
                    changed = true;
                }
            }

            Collider targetCollider = target.GetComponent<Collider>();
            Collider prefabCollider = prefab != null ? prefab.GetComponent<Collider>() : null;
            if (targetCollider != null && prefabCollider != null)
            {
                float friction = actor.OverrideFriction
                    ? Mathf.Clamp01(actor.Friction)
                    : GetFriction(prefabCollider);
                float bounciness = actor.OverrideBounciness
                    ? Mathf.Clamp01(actor.Bounciness)
                    : GetBounciness(prefabCollider);

                if (Different(GetFriction(targetCollider), friction) ||
                    Different(GetBounciness(targetCollider), bounciness))
                {
                    if (recordUndo) Undo.RecordObject(targetCollider, "Apply Actor Physics");
                    PhysicsMaterial material = targetCollider.material;
                    if (material == null)
                    {
                        material = new PhysicsMaterial(target.name + " Physics");
                        targetCollider.material = material;
                    }

                    if (recordUndo) Undo.RecordObject(material, "Apply Actor Physics");
                    material.dynamicFriction = friction;
                    material.staticFriction = friction;
                    material.bounciness = bounciness;
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>
        /// Captures Inspector edits made to root physics components. Inherited values remain
        /// un-serialized until the scene value actually differs from the prefab default.
        /// </summary>
        public static bool CaptureFrom(GameObject target, ActorJson actor, GameObject prefab = null)
        {
            if (target == null || actor == null) return false;
            prefab ??= LoadPrefab(actor);
            bool changed = false;

            Rigidbody targetBody = target.GetComponent<Rigidbody>();
            Rigidbody prefabBody = prefab != null ? prefab.GetComponent<Rigidbody>() : null;
            if (targetBody != null && prefabBody != null)
            {
                Vector3 expectedVelocity = ToVector3(actor.Velocity, prefabBody.linearVelocity);
                if (Different(targetBody.linearVelocity, expectedVelocity))
                {
                    actor.Velocity = ToArray(targetBody.linearVelocity);
                    changed = true;
                }

                Vector3 expectedAngularVelocity = ToVector3(actor.AngularVelocity, prefabBody.angularVelocity);
                if (Different(targetBody.angularVelocity, expectedAngularVelocity))
                {
                    actor.AngularVelocity = ToArray(targetBody.angularVelocity);
                    changed = true;
                }

                float expectedDensity = actor.OverrideDensity ? actor.Density : prefabBody.mass;
                if (Different(targetBody.mass, expectedDensity))
                {
                    actor.Density = targetBody.mass;
                    actor.OverrideDensity = true;
                    changed = true;
                }

                float expectedDrag = actor.OverrideDrag ? actor.Drag : prefabBody.linearDamping;
                if (Different(targetBody.linearDamping, expectedDrag))
                {
                    actor.Drag = targetBody.linearDamping;
                    actor.OverrideDrag = true;
                    changed = true;
                }
            }

            Collider targetCollider = target.GetComponent<Collider>();
            Collider prefabCollider = prefab != null ? prefab.GetComponent<Collider>() : null;
            if (targetCollider != null && prefabCollider != null)
            {
                float currentFriction = GetFriction(targetCollider);
                float expectedFriction = actor.OverrideFriction ? actor.Friction : GetFriction(prefabCollider);
                if (Different(currentFriction, expectedFriction))
                {
                    actor.Friction = currentFriction;
                    actor.OverrideFriction = true;
                    changed = true;
                }

                float currentBounciness = GetBounciness(targetCollider);
                float expectedBounciness = actor.OverrideBounciness ? actor.Bounciness : GetBounciness(prefabCollider);
                if (Different(currentBounciness, expectedBounciness))
                {
                    actor.Bounciness = currentBounciness;
                    actor.OverrideBounciness = true;
                    changed = true;
                }
            }

            return changed;
        }

        public static Vector3 ToVector3(float[] values, Vector3 fallback)
        {
            return values != null && values.Length >= 3
                ? new Vector3(values[0], values[1], values[2])
                : fallback;
        }

        private static float[] ToArray(Vector3 value) => new[] { value.x, value.y, value.z };
        private static bool Different(float a, float b) => Mathf.Abs(a - b) > 0.001f;
        private static bool Different(Vector3 a, Vector3 b) => (a - b).sqrMagnitude > 0.000001f;
    }
}
