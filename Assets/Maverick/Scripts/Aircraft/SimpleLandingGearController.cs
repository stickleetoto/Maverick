using System.Collections.Generic;
using UnityEngine;

namespace EaglePhysicalAI.Aircraft
{
    /// <summary>
    /// Lightweight landing gear visibility controller for imported aircraft models.
    /// It does not simulate landing gear physics; it only hides/shows assigned objects.
    /// </summary>
    public class SimpleLandingGearController : MonoBehaviour
    {
        public bool gearDown = true;
        public KeyCode toggleKey = KeyCode.G;
        public bool autoFindByNameOnAwake = true;
        public string[] gearNameHints = { "gear", "wheel", "landing" };
        public List<GameObject> gearObjects = new List<GameObject>();
        public string lastNote = "none";

        private void Awake()
        {
            if (autoFindByNameOnAwake && gearObjects.Count == 0) FindGearObjectsByName();
            ApplyState();
        }

        private void Update()
        {
            if (MaverickInput.GetKeyDown(toggleKey)) ToggleGear();
        }

        [ContextMenu("Find Gear Objects By Name")]
        public void FindGearObjectsByName()
        {
            gearObjects.Clear();
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child == transform) continue;
                string lower = child.name.ToLowerInvariant();
                foreach (string hint in gearNameHints)
                {
                    if (!string.IsNullOrEmpty(hint) && lower.Contains(hint.ToLowerInvariant()))
                    {
                        if (!gearObjects.Contains(child.gameObject)) gearObjects.Add(child.gameObject);
                        break;
                    }
                }
            }
            lastNote = "found_gear_objects_" + gearObjects.Count;
        }

        public void ToggleGear()
        {
            gearDown = !gearDown;
            ApplyState();
        }

        public void SetGearDown(bool down)
        {
            gearDown = down;
            ApplyState();
        }

        public void ApplyState()
        {
            foreach (var obj in gearObjects)
            {
                if (obj != null) obj.SetActive(gearDown);
            }
            lastNote = gearDown ? "gear_down" : "gear_up_visual_only";
        }
    }
}
