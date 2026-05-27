using UnityEngine;

namespace EaglePhysicalAI.Battlefield
{
    public class CasTargetDesignator : MonoBehaviour
    {
        public GroundUnit selectedTarget;
        public LayerMask targetMask = ~0;
        public float maxSelectDistance = 10000f;
        public Camera sourceCamera;

        private void Awake()
        {
            if (sourceCamera == null) sourceCamera = Camera.main;
        }

        private void Update()
        {
            if (MaverickInput.GetMouseButtonDown(0))
            {
                TrySelectFromMouse();
            }
        }

        public bool TrySelectFromMouse()
        {
            if (sourceCamera == null) return false;
            Ray ray = sourceCamera.ScreenPointToRay(MaverickInput.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, maxSelectDistance, targetMask))
            {
                GroundUnit unit = hit.collider.GetComponentInParent<GroundUnit>();
                if (unit != null)
                {
                    selectedTarget = unit;
                    return true;
                }
            }
            return false;
        }
    }
}
