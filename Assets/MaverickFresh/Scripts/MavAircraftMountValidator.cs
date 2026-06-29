using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Lightweight aircraft setup validator for model swaps.
    /// It warns about missing or suspicious mounts but does not move them by default.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavAircraftMountValidator : MonoBehaviour
    {
        [Header("References")]
        public MavCASOrdnanceAssets ordnanceAssets;
        public MavTargetingPodSystem targetingPod;
        public Transform gunMuzzle;
        public Transform tgpMount;
        public Transform cameraFollowPoint;
        public Transform radarOrigin;
        public Transform leftMissileRail;
        public Transform rightMissileRail;
        public Transform missileRail;
        public Transform bombPylon;
        public Transform engineLeft;
        public Transform engineRight;

        [Header("Validation")]
        public bool validateOnStart = true;
        public bool validateInEditor;
        public bool createMissingFallbackMounts = false;
        public float forwardAngleWarningDeg = 20f;
        public float muzzleNearColliderDistance = 0.25f;

        [Header("Runtime")]
        public int warningCount;
        public string lastWarning = "";

        private void Start()
        {
            if (validateOnStart)
                ValidateMounts();
        }

        private void OnValidate()
        {
            if (!validateInEditor || Application.isPlaying)
                return;

            ResolveReferences();
            ValidateMounts();
        }

        [ContextMenu("Validate Aircraft Mounts")]
        public void ValidateMounts()
        {
            warningCount = 0;
            lastWarning = "";

            ResolveReferences();

            if (createMissingFallbackMounts)
                CreateMissingFallbackMounts();

            CheckMissingMounts();
            CheckDirections();
            CheckColliderOverlap();
        }

        private void ResolveReferences()
        {
            if (ordnanceAssets == null)
                ordnanceAssets = GetComponent<MavCASOrdnanceAssets>();

            if (targetingPod == null)
                targetingPod = GetComponent<MavTargetingPodSystem>();

            if (ordnanceAssets != null)
            {
                if (gunMuzzle == null) gunMuzzle = ordnanceAssets.gunMuzzle;
                if (missileRail == null) missileRail = ordnanceAssets.missileRail;
                if (bombPylon == null) bombPylon = ordnanceAssets.bombPylon;
            }

            if (targetingPod != null && tgpMount == null)
                tgpMount = targetingPod.podMount;

            if (gunMuzzle == null)
                gunMuzzle = FindChildByNames("GunMuzzle", "Gun_Muzzle", "MavGunMuzzle", "Muzzle");

            if (tgpMount == null)
                tgpMount = FindChildByNames("TGPMount", "TargetingPodMount", "MavTGP_Mount", "TGP_Mount");

            if (cameraFollowPoint == null)
                cameraFollowPoint = FindChildByNames("CameraFollowPoint", "Camera_Follow_Point", "CamFollow", "FollowPoint");

            if (radarOrigin == null)
                radarOrigin = FindChildByNames("RadarOrigin", "Radar_Origin", "RadarMount", "Radar");

            if (missileRail == null)
                missileRail = FindChildByNames("MissileRail", "Missile_Rail", "MissileMount");

            if (leftMissileRail == null)
                leftMissileRail = FindChildByNames("LeftMissileRail", "MissileRail_L", "L_MissileRail");

            if (rightMissileRail == null)
                rightMissileRail = FindChildByNames("RightMissileRail", "MissileRail_R", "R_MissileRail");

            if (bombPylon == null)
                bombPylon = FindChildByNames("BombPylon", "Bomb_Pylon", "BombMount");

            if (engineLeft == null)
                engineLeft = FindChildByNames("EngineLeft", "Engine_L", "LeftEngine");

            if (engineRight == null)
                engineRight = FindChildByNames("EngineRight", "Engine_R", "RightEngine");
        }

        private void CheckMissingMounts()
        {
            if (gunMuzzle == null)
                Warn("GunMuzzle missing. Gun shells will use a nose fallback.");

            if (cameraFollowPoint == null)
                Warn("CameraFollowPoint missing. Camera rig will use its configured fallback position.");

            if (targetingPod != null && tgpMount == null)
                Warn("TGP mount missing while MavTargetingPodSystem exists.");

            if (HasRadarComponent() && radarOrigin == null)
                Warn("Radar origin missing while a radar component appears to exist.");
        }

        private void CheckDirections()
        {
            if (gunMuzzle != null)
                CheckForward(gunMuzzle, "GunMuzzle", forwardAngleWarningDeg);

            if (radarOrigin != null)
                CheckForward(radarOrigin, "RadarOrigin", forwardAngleWarningDeg);

            if (missileRail != null)
                CheckForward(missileRail, "MissileRail", 35f);

            if (leftMissileRail != null)
                CheckForward(leftMissileRail, "LeftMissileRail", 35f);

            if (rightMissileRail != null)
                CheckForward(rightMissileRail, "RightMissileRail", 35f);
        }

        private void CheckColliderOverlap()
        {
            if (gunMuzzle == null)
                return;

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider c = colliders[i];
                if (c == null || c.isTrigger)
                    continue;

                if (gunMuzzle.IsChildOf(c.transform))
                    continue;

                if (c.bounds.Contains(gunMuzzle.position))
                {
                    Warn("GunMuzzle appears inside aircraft collider '" + c.name + "'. Move it forward or increase muzzleSpawnForwardOffset.");
                    return;
                }

                Vector3 closest = c.ClosestPoint(gunMuzzle.position);
                float distance = Vector3.Distance(closest, gunMuzzle.position);
                if (distance <= muzzleNearColliderDistance)
                {
                    Warn("GunMuzzle is very close to aircraft collider '" + c.name + "' (" + distance.ToString("0.00") + "m).");
                    return;
                }
            }
        }

        private void CheckForward(Transform mount, string label, float maxAngle)
        {
            float angle = Vector3.Angle(mount.forward, transform.forward);
            if (angle > maxAngle)
                Warn(label + ".forward differs from aircraft forward by " + angle.ToString("0.0") + " degrees.");
        }

        private void CreateMissingFallbackMounts()
        {
            if (gunMuzzle == null)
                gunMuzzle = CreateFallbackMount("GunMuzzle", new Vector3(0.85f, -1.1f, 11.5f));

            if (cameraFollowPoint == null)
                cameraFollowPoint = CreateFallbackMount("CameraFollowPoint", new Vector3(0f, 5.4f, -16.5f));

            if (radarOrigin == null)
                radarOrigin = CreateFallbackMount("RadarOrigin", new Vector3(0f, 0f, 9f));

            if (ordnanceAssets != null && ordnanceAssets.gunMuzzle == null)
                ordnanceAssets.gunMuzzle = gunMuzzle;

            if (targetingPod != null && targetingPod.podMount == null)
                targetingPod.podMount = tgpMount;
        }

        private Transform CreateFallbackMount(string mountName, Vector3 localPosition)
        {
            GameObject go = new GameObject(mountName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        private Transform FindChildByNames(params string[] names)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null || child == transform)
                    continue;

                for (int n = 0; n < names.Length; n++)
                {
                    if (child.name == names[n])
                        return child;
                }
            }

            string lowerNeedle;
            for (int n = 0; n < names.Length; n++)
            {
                lowerNeedle = names[n].ToLowerInvariant();
                for (int i = 0; i < children.Length; i++)
                {
                    Transform child = children[i];
                    if (child != null && child != transform && child.name.ToLowerInvariant().Contains(lowerNeedle))
                        return child;
                }
            }

            return null;
        }

        private bool HasRadarComponent()
        {
            Component radar = gameObject.GetComponent("MavRadarSystem");
            if (radar != null)
                return true;

            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour mb = behaviours[i];
                if (mb == null)
                    continue;

                string typeName = mb.GetType().Name;
                if (typeName.ToLowerInvariant().Contains("radar"))
                    return true;
            }

            return false;
        }

        private void Warn(string message)
        {
            warningCount++;
            lastWarning = message;
            Debug.LogWarning("MavAircraftMountValidator: " + message, this);
        }
    }
}
