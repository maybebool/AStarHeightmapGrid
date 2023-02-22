using System;
using UnityEngine;

namespace DefaultNamespace {
    public class CameraMovement : MonoBehaviour {

        [SerializeField] private Transform anchor;
        private void Update() {
            transform.RotateAround (anchor.transform.position, Vector3.up, 30 * Time.deltaTime);
            
        }
    }
}