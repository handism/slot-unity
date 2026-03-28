using UnityEngine;
using UnityEngine.InputSystem;

namespace SlotGame.Core
{
    /// <summary>
    /// InputSystem のアクションを GameManager のアクションにバインドするクラス。
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class SlotInputHandler : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        private PlayerInput _playerInput;
        private InputAction _spinAction;
        private InputAction _betUpAction;
        private InputAction _betDownAction;
        private InputAction _autoSpinAction;
        private InputAction _skipAction;
        private InputAction _muteAction;
        private InputAction _turboAction;
        private InputAction _paytableAction;

        private void Awake()
        {
            if (gameManager == null)
                gameManager = GetComponent<GameManager>();

            _playerInput = GetComponent<PlayerInput>();

            var actionMap = _playerInput.actions.FindActionMap("Slot");
            if (actionMap == null)
            {
                Debug.LogWarning("[SlotInputHandler] 'Slot' action map not found.");
                return;
            }

            _spinAction = actionMap.FindAction("Spin");
            _betUpAction = actionMap.FindAction("BetUp");
            _betDownAction = actionMap.FindAction("BetDown");
            _autoSpinAction = actionMap.FindAction("AutoSpin");
            _skipAction = actionMap.FindAction("Skip");
            _muteAction = actionMap.FindAction("Mute");
            _turboAction = actionMap.FindAction("Turbo");
            _paytableAction = actionMap.FindAction("Paytable");
        }

        private void OnEnable()
        {
            if (_spinAction != null) _spinAction.performed += OnSpin;
            if (_betUpAction != null) _betUpAction.performed += OnBetUp;
            if (_betDownAction != null) _betDownAction.performed += OnBetDown;
            if (_autoSpinAction != null) _autoSpinAction.performed += OnAutoSpin;
            if (_skipAction != null) _skipAction.performed += OnSkip;
            if (_muteAction != null) _muteAction.performed += OnMute;
            if (_turboAction != null) _turboAction.performed += OnTurbo;
            if (_paytableAction != null) _paytableAction.performed += OnPaytable;
        }

        private void OnDisable()
        {
            if (_spinAction != null) _spinAction.performed -= OnSpin;
            if (_betUpAction != null) _betUpAction.performed -= OnBetUp;
            if (_betDownAction != null) _betDownAction.performed -= OnBetDown;
            if (_autoSpinAction != null) _autoSpinAction.performed -= OnAutoSpin;
            if (_skipAction != null) _skipAction.performed -= OnSkip;
            if (_muteAction != null) _muteAction.performed -= OnMute;
            if (_turboAction != null) _turboAction.performed -= OnTurbo;
            if (_paytableAction != null) _paytableAction.performed -= OnPaytable;
        }

        private void OnSpin(InputAction.CallbackContext context) => gameManager.OnSpinButtonPressed();
        private void OnBetUp(InputAction.CallbackContext context) => gameManager.IncreaseBet();
        private void OnBetDown(InputAction.CallbackContext context) => gameManager.DecreaseBet();
        private void OnAutoSpin(InputAction.CallbackContext context) => gameManager.ToggleAutoSpin();
        private void OnSkip(InputAction.CallbackContext context) => gameManager.RequestSkip();
        private void OnMute(InputAction.CallbackContext context) => gameManager.ToggleMute();
        private void OnTurbo(InputAction.CallbackContext context) => gameManager.ToggleTurbo();
        private void OnPaytable(InputAction.CallbackContext context) => gameManager.TogglePaytable();
    }
}
