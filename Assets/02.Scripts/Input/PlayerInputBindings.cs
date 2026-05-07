using System;
using _02.Scripts.Manager;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace _02.Scripts.Input
{
    public class PlayerInputBindings : MonoBehaviour
    {
        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
        }

        private void Update()
        {
            foreach (Touch t in Touch.activeTouches)
            {
                if (t.phase == TouchPhase.Began)
                {
                    var pos = t.screenPosition;
                    var mid = Screen.width / 2f;
                    
                    if(pos.x < mid) LevelManager.instance.player.Defend();
                    else LevelManager.instance.player.Attack();
                }
            }
        }

        private void OnAttack(InputValue value)
        {
            if(value.isPressed) LevelManager.instance.player.Attack();
        }

        private void OnDefend(InputValue value)
        {
            if(value.isPressed) LevelManager.instance.player.Defend();
        }
        
        
    }
}