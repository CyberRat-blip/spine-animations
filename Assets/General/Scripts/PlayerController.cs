using Gameplay.Aim;
using Gameplay.Animation;
using Gameplay.Input;
using Gameplay.Movement;
using UnityEngine;

namespace Gameplay
{
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private MousePlayerInput input;
        [SerializeField] private InertialHorizontalMover mover;
        [SerializeField] private DeterministicLocomotionAnimator locomotion;
        [SerializeField] private SpineAimController aim;
        [SerializeField] private WorldCursorCrosshair cursorCrosshair;

        private IPlayerInput _input;
        private IHorizontalMover _mover;
        private ILocomotionAnimator _locomotion;
        private IAimController _aim;

        private void Reset()
        {
            input = GetComponent<MousePlayerInput>();
            mover = GetComponent<InertialHorizontalMover>();
            locomotion = GetComponent<DeterministicLocomotionAnimator>();
            aim = GetComponent<SpineAimController>();
            cursorCrosshair = GetComponentInChildren<WorldCursorCrosshair>();
        }

        private void Awake()
        {
            _input = input;
            _mover = mover;
            _locomotion = locomotion;
            _aim = aim;
        }

        private void Update()
        {
            _input.Tick();

            _mover.SetTargetX(_input.CursorWorldPosition.x);
            _mover.Tick(Time.deltaTime);

            _locomotion.Tick(_mover.CurrentX, _mover.CurrentSpeed, _mover.Range);
            _aim.Tick(_input.IsAiming, _input.CursorWorldPosition, transform.position);

            if (cursorCrosshair != null)
            {
                cursorCrosshair.Tick(_input.IsAiming, _input.CursorWorldPosition);
            }
        }
    }
}
