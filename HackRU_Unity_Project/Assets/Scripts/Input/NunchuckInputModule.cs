using System;
using WiimoteApi;

namespace UnityEngine.EventSystems
{
    [AddComponentMenu("Event/Nunchuck Input Module")]
    public class NunchuckInputModule : PointerInputModule
    {
        private float m_NextAction;
        private bool hitStickDirection = false;
        private bool hitSubmit = false;

        protected NunchuckInputModule()
        { }

        public Wiimote wiimote;
        public int deadZone = 10;

        [SerializeField]
        private float m_InputActionsPerSecond = 10;

        [SerializeField]
        private bool m_AllowActivationOnMobileDevice;

        public bool allowActivationOnMobileDevice
        {
            get { return m_AllowActivationOnMobileDevice; }
            set { m_AllowActivationOnMobileDevice = value; }
        }

        public float inputActionsPerSecond
        {
            get { return m_InputActionsPerSecond; }
            set { m_InputActionsPerSecond = value; }
        }

        public override void UpdateModule()
        {
            
        }

        public override bool IsModuleSupported()
        {
            return true;
        }

        public override bool ShouldActivateModule()
        {
            if (!base.ShouldActivateModule() || wiimote == null)
                return false;

            var shouldActivate = wiimote.b && !hitSubmit;
            if (wiimote.current_ext != ExtensionController.NUNCHUCK)
                return shouldActivate;
            NunchuckData data = new NunchuckData();
            data.InterpretExtensionData(wiimote.extension);

            shouldActivate |= data.z;
            shouldActivate |= Mathf.Abs((int)data.stick[0] - 128) > deadZone;
            shouldActivate |= Mathf.Abs((int)data.stick[1] - 128) > deadZone;
            return shouldActivate;
        }

        public override void ActivateModule()
        {
            base.ActivateModule();

            var toSelect = eventSystem.currentSelectedGameObject;
            if (toSelect == null)
                toSelect = eventSystem.lastSelectedGameObject;
            if (toSelect == null)
                toSelect = eventSystem.firstSelectedGameObject;

            eventSystem.SetSelectedGameObject(null, GetBaseEventData());
            eventSystem.SetSelectedGameObject(toSelect, GetBaseEventData());
        }

        public override void DeactivateModule()
        {
            base.DeactivateModule();
            ClearSelection();
        }

        public override void Process()
        {
            bool usedEvent = SendUpdateEventToSelectedObject();

            if (eventSystem.sendNavigationEvents)
            {
                if (!usedEvent)
                    usedEvent |= SendMoveEventToSelectedObject();

                if (!usedEvent)
                    SendSubmitEventToSelectedObject();
            }
        }

        /// <summary>
        /// Process submit keys.
        /// </summary>
        private bool SendSubmitEventToSelectedObject()
        {
            if (eventSystem.currentSelectedGameObject == null || wiimote == null)
                return false;

            var data = GetBaseEventData();
            if (wiimote.b && !hitSubmit)
            {
                hitSubmit = true;
                ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, data, ExecuteEvents.submitHandler);
            }
            else if (!wiimote.b)
                hitSubmit = false;

            if (wiimote.current_ext != ExtensionController.NUNCHUCK)
                return data.used;
            NunchuckData nd = new NunchuckData();
            nd.InterpretExtensionData(wiimote.extension);

            if (nd.z)
                ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, data, ExecuteEvents.cancelHandler);
            return data.used;
        }

        private bool AllowMoveEventProcessing(float time)
        {
            if (wiimote == null || wiimote.current_ext != ExtensionController.NUNCHUCK)
                return false;
            NunchuckData data = new NunchuckData();
            data.InterpretExtensionData(wiimote.extension);

            bool allow =    Mathf.Abs((int)data.stick[0] - 128) > deadZone;
            allow |=        Mathf.Abs((int)data.stick[1] - 128) > deadZone;
            allow |= (time > m_NextAction);
            return allow;
        }

        private Vector2 GetRawMoveVector()
        {
            if (wiimote == null || wiimote.current_ext != ExtensionController.NUNCHUCK)
                return Vector2.zero;
            NunchuckData data = new NunchuckData();
            data.InterpretExtensionData(wiimote.extension);

            Vector2 move = Vector2.zero;
            move.x = data.stick[0];
            move.y = data.stick[1];
            move.x -= 128;
            move.y -= 128;

            if (Mathf.Abs((int)data.stick[0] - 128) > deadZone)
            {
                if (move.x < 0)
                    move.x = -1f;
                if (move.x > 0)
                    move.x = 1f;
            }
            else move.x = 0;
            if (Mathf.Abs((int)data.stick[1] - 128) > deadZone)
            {
                if (move.y < 0)
                    move.y = -1f;
                if (move.y > 0)
                    move.y = 1f;
            }
            else move.y = 0;
            return move;
        }

        /// <summary>
        /// Process keyboard events.
        /// </summary>
        private bool SendMoveEventToSelectedObject()
        {
            float time = Time.unscaledTime;

            if (!AllowMoveEventProcessing(time))
                return false;

            Vector2 movement = GetRawMoveVector();
            // Debug.Log(m_ProcessingEvent.rawType + " axis:" + m_AllowAxisEvents + " value:" + "(" + x + "," + y + ")");
            var axisEventData = GetAxisEventData(movement.x, movement.y, 0.6f);
            if ((!Mathf.Approximately(axisEventData.moveVector.x, 0f)
                || !Mathf.Approximately(axisEventData.moveVector.y, 0f)))
            {
                if (!hitStickDirection)
                {
                    hitStickDirection = true;
                    ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, axisEventData, ExecuteEvents.moveHandler);
                }
            }
            else
                hitStickDirection = false;
            m_NextAction = time + 1f / m_InputActionsPerSecond;
            return axisEventData.used;
        }

        private bool SendUpdateEventToSelectedObject()
        {
            if (eventSystem.currentSelectedGameObject == null)
                return false;

            var data = GetBaseEventData();
            ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, data, ExecuteEvents.updateSelectedHandler);
            return data.used;
        }
    }
}