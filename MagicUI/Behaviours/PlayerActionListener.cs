using System;
using UnityEngine;

namespace MagicUI.Behaviours
{
    internal class PlayerActionListener : MonoBehaviour
    {
        public KeyCode? playerAction;
        public Func<bool>? enableCondition;
        public Action? execute;
        private bool previousPressedState = false;

        private bool CurrentPressedState() {
            return playerAction == null ? false : Input.GetKeyDown(playerAction.Value);
        }

        private void Update()
        {
            var shouldExecute = CurrentPressedState() && !previousPressedState;
            previousPressedState = CurrentPressedState();

            bool enable = enableCondition?.Invoke() ?? true;
            if (enable && shouldExecute)
            {
                execute?.Invoke();
            }
        }
    }
}
