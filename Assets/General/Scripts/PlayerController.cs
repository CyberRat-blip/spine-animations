using Gameplay.Aim;
using Gameplay.Animation;
using Gameplay.Input;
using Gameplay.Movement;
using UnityEngine;

namespace Gameplay
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private MousePlayerInput input;
        [SerializeField] private InertialHorizontalMover mover;
        [SerializeField] private DeterministicLocomotionAnimator locomotion;
        [SerializeField] private SpineAimController aim;
        [SerializeField] private WorldCursorCrosshair cursorCrosshair;

        private void Reset()
        {
            input = GetComponent<MousePlayerInput>();
            mover = GetComponent<InertialHorizontalMover>();
            locomotion = GetComponent<DeterministicLocomotionAnimator>();
            aim = GetComponent<SpineAimController>();
            cursorCrosshair = GetComponentInChildren<WorldCursorCrosshair>();
        }

        private void Update()
        {
            TickGameplay(Time.deltaTime);
        }

        private void TickGameplay(float deltaTime)
        {
            input.Tick();

            mover.SetTargetX(input.CursorWorldPosition.x);
            mover.Tick(deltaTime);

            locomotion.Tick(mover.CurrentX, mover.CurrentSpeed, mover.Range);
            aim.Tick(input.IsAiming, input.CursorWorldPosition);

            cursorCrosshair?.Tick(input.IsAiming, input.CursorWorldPosition);
        }
    }
}
