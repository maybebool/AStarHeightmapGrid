using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Heightmap {
    public class InputEventManager : MonoBehaviour
    {
        [SerializeField] private PathFinding pathFinding;
    
        private InputSystem_Actions _keyAction;

        private void Awake() {
            _keyAction = new InputSystem_Actions();
        }


        private void OnEnable() {
            _keyAction.Enable();
            _keyAction.Player.Attack.performed+= OnAKeyPerformed;
            _keyAction.Player.Jump.performed += OnSpaceKeyPerformed;
        }

        private void OnDisable() {
            _keyAction.Disable();
        }
    
        private void OnAKeyPerformed(InputAction.CallbackContext context) {
            if (pathFinding != null)
                pathFinding.HandleRightMouseAction(); 
        }
    
        private void OnSpaceKeyPerformed(InputAction.CallbackContext context) {
            if (pathFinding != null)
                pathFinding.HandleLeftMouseAction();
        }
    }
}
