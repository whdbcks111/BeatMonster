using _02.Scripts.Manager;
using _02.Scripts.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace _02.Scripts.Input
{
    public class StageSelectSceneInputBindings : MonoBehaviour
    {
        [SerializeField] private StageSelectSceneManager stageSelectSceneManager;
        
        private void OnGoToNextStage(InputValue value)
        {
            stageSelectSceneManager.GoToNextStage();
        }

        private void OnGoToPrevStage(InputValue value)
        {
            stageSelectSceneManager.GoToPrevStage();
        }

        private void OnGoToTitle(InputValue value)
        {
            stageSelectSceneManager.GoToTitleScene();
        }
    }
}