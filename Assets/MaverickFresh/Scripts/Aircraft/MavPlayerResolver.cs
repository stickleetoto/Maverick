using UnityEngine;

namespace MaverickFresh
{
    public static class MavPlayerResolver
    {
        public const string DefaultPlayerName = "Mav_Player";

        public static GameObject FindPlayerObject()
        {
            GameObject go = GameObject.Find(DefaultPlayerName);
            if (go != null)
                return go;

            MavMouseFlightJet jet = Object.FindObjectOfType<MavMouseFlightJet>();
            if (jet != null)
                return jet.gameObject;

            MavAircraftProfileApplier applier = Object.FindObjectOfType<MavAircraftProfileApplier>();
            if (applier != null)
                return applier.gameObject;

            return null;
        }

        public static GameObject EnsurePlayerObject(string preferredName = DefaultPlayerName)
        {
            GameObject go = FindPlayerObject();
            if (go != null)
                return go;

            // This helper is still available for editor tools, but runtime bootstraps should prefer
            // a manually-created Mav_Player in the scene.
            go = new GameObject(string.IsNullOrEmpty(preferredName) ? DefaultPlayerName : preferredName);
            return go;
        }
    }
}
