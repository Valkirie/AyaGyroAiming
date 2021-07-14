using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AyaGyroAiming
{
    public class XInputController
    {
        Controller controller;
        Gamepad gamepad;
        int rate;

        public IXbox360Controller vcontroller;
        public Vector3 gyroscope;
        public UserIndex index;
        public bool connected = false;

        public XInputController(UserIndex _idx, int _rate = 10)
        {
            controller = new Controller(_idx);
            index = _idx;
            rate = _rate;

            connected = controller.IsConnected;
            gyroscope = new Vector3();

            Thread UpdateThread = new Thread(Update);
            UpdateThread.Start();
        }

        public void SetVirtualController(IXbox360Controller VirtualController)
        {
            vcontroller = VirtualController;
        }

        public void UpdateGyro(Vector3 gyro)
        {
            // don't ask...
            gyroscope.X = gyro.Y;
            gyroscope.Y = gyro.X;
            gyroscope.Z = gyro.Z;
        }

        void Update()
        {
            while (connected)
            {
                gamepad = controller.GetState().Gamepad;

                // push the values
                if (vcontroller != null)
                {
                    short ThumbX = (short)Math.Max(-32767, Math.Min(32767, gamepad.RightThumbX + gyroscope.X));
                    short ThumbY = (short)Math.Max(-32767, Math.Min(32767, gamepad.RightThumbY + gyroscope.Y));

                    vcontroller.SetAxisValue(Xbox360Axis.LeftThumbX, gamepad.LeftThumbX);
                    vcontroller.SetAxisValue(Xbox360Axis.LeftThumbY, gamepad.LeftThumbY);

                    vcontroller.SetAxisValue(Xbox360Axis.RightThumbX, (short)ThumbX);
                    vcontroller.SetAxisValue(Xbox360Axis.RightThumbY, (short)ThumbY);

                    vcontroller.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)gamepad.LeftTrigger);
                    vcontroller.SetSliderValue(Xbox360Slider.RightTrigger, (byte)gamepad.RightTrigger);

                    vcontroller.SetButtonState(Xbox360Button.LeftShoulder, gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder));
                    vcontroller.SetButtonState(Xbox360Button.RightShoulder, gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder));
                    
                    vcontroller.SetButtonState(Xbox360Button.LeftThumb, gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb));
                    vcontroller.SetButtonState(Xbox360Button.RightThumb, gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb));

                    vcontroller.SetButtonState(Xbox360Button.A, gamepad.Buttons.HasFlag(GamepadButtonFlags.A));
                    vcontroller.SetButtonState(Xbox360Button.B, gamepad.Buttons.HasFlag(GamepadButtonFlags.B));
                    vcontroller.SetButtonState(Xbox360Button.X, gamepad.Buttons.HasFlag(GamepadButtonFlags.X));
                    vcontroller.SetButtonState(Xbox360Button.Y, gamepad.Buttons.HasFlag(GamepadButtonFlags.Y));

                    vcontroller.SetButtonState(Xbox360Button.Up, gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp));
                    vcontroller.SetButtonState(Xbox360Button.Down, gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown));
                    vcontroller.SetButtonState(Xbox360Button.Left, gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft));
                    vcontroller.SetButtonState(Xbox360Button.Right, gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight));

                    vcontroller.SetButtonState(Xbox360Button.Start, gamepad.Buttons.HasFlag(GamepadButtonFlags.Start));
                    vcontroller.SetButtonState(Xbox360Button.Back, gamepad.Buttons.HasFlag(GamepadButtonFlags.Back));
                }

                UpdateGyro(new Vector3(0, 0, 0));

                Thread.Sleep(rate);
            }
        }
    }
}
