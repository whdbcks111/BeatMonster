using _02.Scripts.Manager;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _02.Scripts.Input
{
    public class PlayerInputBindings : MonoBehaviour
    {
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