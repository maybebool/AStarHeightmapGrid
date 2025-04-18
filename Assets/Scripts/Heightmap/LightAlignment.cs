using UnityEngine;

namespace Heightmap {
    public class LightAlignment : MonoBehaviour {
        
        [SerializeField] private Transform anchor;  
        [SerializeField] private Transform mainCamera;    
        [SerializeField] private float lightXAngle = 50f;

        private void Update() {
            var toCamera = mainCamera.position - anchor.position;
            var yAngle = Mathf.Atan2(toCamera.x, toCamera.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(lightXAngle, yAngle, 0);
        }
    }
}
