using System;
using _02.Scripts.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace _02.Scripts.Input
{
    [RequireComponent(typeof(PlayerInput))]
    public class GameViewInputBindings : MonoBehaviour
    {
        [SerializeField] private GameViewGUI gameViewGUI;
        
        private void OnTogglePauseInGame(InputValue value)
        {
            gameViewGUI.TogglePauseWindow();
        }
    }
}