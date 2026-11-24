using Oculus.Interaction;
using UnityEngine;

namespace RoboticsLab.Story
{
    /// <summary>
    /// References to the Meta Interaction SDK "UI Set" (Meta's Horizon OS UI kit): theme + button/slider prefabs.
    /// Lives at Resources/MetaUIKitLibrary so runtime-built panels (pendants, ECHO captions, hub, assessment)
    /// can be assembled from official Meta UI components. Created by Robotics Lab/Build Lab Scene.
    /// </summary>
    [CreateAssetMenu(menuName = "Robotics Lab/Meta UI Kit Library")]
    public class MetaUIKitLibrary : ScriptableObject
    {
        public UITheme theme;
        public GameObject primaryButton;
        public GameObject secondaryButton;
        public GameObject destructiveButton;
        public GameObject borderlessButton;
        public GameObject slider;

        static MetaUIKitLibrary _instance;
        static bool _looked;

        public static void ClearCache() { _looked = false; _instance = null; }

        public static MetaUIKitLibrary Instance
        {
            get
            {
                if (!_looked) { _instance = Resources.Load<MetaUIKitLibrary>("MetaUIKitLibrary"); _looked = true; }
                return _instance;
            }
        }
    }
}
