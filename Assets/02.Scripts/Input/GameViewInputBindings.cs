using _02.Scripts.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _02.Scripts.Input
{
    public class GameViewInputBindings : MonoBehaviour
    {
        [SerializeField] private GameViewGUI _gameViewGUI;
        
        private void OnEscape(InputValue value)
        {
            _gameViewGUI.OpenPauseWindow();
        }
    }
}