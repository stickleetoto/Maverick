using System.Collections.Generic;
using UnityEngine;

namespace EaglePhysicalAI.Aircraft
{
    /// <summary>
    /// Helper for imported aircraft assets. Creates stable child anchors and optional simple colliders.
    /// Use it on the aircraft root, not on the visual mesh child.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class F15EAssetRigHelper : MonoBehaviour
    {
        [Header("Generated Anchor Names")]
        public string modelRootName = "Model";
        public string nosePointName = "NosePoint";
        public string cameraFollowPointName = "CameraFollowPoint";
        public string targetingPodMountName = "TargetingPodMount";
        public string strikeOriginName = "WeaponOrStrikeOrigin";
        public string engineLeftName = "EngineLeft";
        public string engineRightName = "EngineRight";

        [Header("Anchor Offsets - Local Space")]
        public Vector3 noseOffset = new Vector3(0f, 0f, 8.5f);
        public Vector3 cameraFollowOffset = new Vector3(0f, 2.2f, -9.0f);
        public Vector3 targetingPodOffset = new Vector3(0.65f, -0.85f, 2.4f);
        public Vector3 strikeOriginOffset = new Vector3(0f, -0.4f, 3.5f);
        public Vector3 engineLeftOffset = new Vector3(-1.15f, 0f, -7.0f);
        public Vector3 engineRightOffset = new Vector3(1.15f, 0f, -7.0f);

        [Header("Collider Setup")]
        public bool createSimpleColliders = true;
        public bool removeOldGeneratedColliders = true;
        public string generatedColliderPrefix = "MaverickGeneratedCollider";
        public Vector3 fuselageColliderSize = new Vector3(2.2f, 2.0f, 12.0f);
        public Vector3 fuselageColliderCenter = new Vector3(0f, 0f, 0.2f);
        public Vector3 wingColliderSize = new Vector3(10.5f, 0.32f, 2.2f);
        public Vector3 wingColliderCenter = new Vector3(0f, -0.05f, -1.6f);
        public Vector3 tailColliderSize = new Vector3(4.5f, 1.0f, 2.5f);
        public Vector3 tailColliderCenter = new Vector3(0f, 0.65f, -5.9f);

        [Header("Resolved Anchors")]
        public Transform nosePoint;
        public Transform cameraFollowPoint;
        public Transform targetingPodMount;
        public Transform strikeOrigin;
        public Transform engineLeft;
        public Transform engineRight;

        private void Reset()
        {
            EnsureRig();
        }

        [ContextMenu("Ensure F-15E Asset Rig")]
        public void EnsureRig()
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            if (rb.mass < 1000f) rb.mass = 14500f;

            nosePoint = EnsureAnchor(nosePointName, noseOffset);
            cameraFollowPoint = EnsureAnchor(cameraFollowPointName, cameraFollowOffset);
            targetingPodMount = EnsureAnchor(targetingPodMountName, targetingPodOffset);
            strikeOrigin = EnsureAnchor(strikeOriginName, strikeOriginOffset);
            engineLeft = EnsureAnchor(engineLeftName, engineLeftOffset);
            engineRight = EnsureAnchor(engineRightName, engineRightOffset);

            if (createSimpleColliders) EnsureSimpleColliders();
        }

        private Transform EnsureAnchor(string anchorName, Vector3 localOffset)
        {
            Transform child = transform.Find(anchorName);
            if (child == null)
            {
                var go = new GameObject(anchorName);
                child = go.transform;
                child.SetParent(transform, false);
            }
            child.localPosition = localOffset;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return child;
        }

        private void EnsureSimpleColliders()
        {
            if (removeOldGeneratedColliders)
            {
                var old = new List<Transform>();
                foreach (Transform child in transform)
                {
                    if (child.name.StartsWith(generatedColliderPrefix)) old.Add(child);
                }
                foreach (Transform child in old)
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }

            CreateBoxCollider(generatedColliderPrefix + "_Fuselage", fuselageColliderCenter, fuselageColliderSize);
            CreateBoxCollider(generatedColliderPrefix + "_Wing", wingColliderCenter, wingColliderSize);
            CreateBoxCollider(generatedColliderPrefix + "_Tail", tailColliderCenter, tailColliderSize);
        }

        private void CreateBoxCollider(string objectName, Vector3 center, Vector3 size)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.center = Vector3.zero;
        }
    }
}
