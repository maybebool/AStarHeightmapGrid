using UnityEngine;

namespace Utilities {
    public class CameraMovement : MonoBehaviour {

        [SerializeField] private Transform anchor;
        public float multiplier = 0;
        private float _time;
    
        private void Update() {
            transform.RotateAround (anchor.transform.position, Vector3.up, 30 * Time.deltaTime * multiplier);
        }
    
        public void SliderMultiplier(float adjustingSpeed) {
            multiplier = adjustingSpeed;
        }
    }
}