using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
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
        public DualShockPadMeta meta;

        public XInputGirometer gyrometer;
        public Vector3 AngularStick;
        public Vector3 AngularVelocity;

        public XInputAccelerometer accelerometer;
        public float[] Acceleration;

        public UserIndex index;
        public PhysicalAddress PadMacAddress = new PhysicalAddress(new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10 });
        public bool connected = false;

        byte[][] udpOutBuffers = new byte[UdpServer.NUMBER_SLOTS][]
        {
                    new byte[100], new byte[100],
                    new byte[100], new byte[100],
        };

        public XInputController(UserIndex _idx, int _rate = 10)
        {
            controller = new Controller(_idx);
            index = _idx;
            rate = _rate;

            meta = new DualShockPadMeta()
            {
                BatteryStatus = DsBattery.Full,
                ConnectionType = DsConnection.Bluetooth,
                IsActive = true,
                PadId = (byte)0, //_idx
                PadMacAddress = new PhysicalAddress(new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10 }),
                Model = DsModel.DS4,
                PadState = DsState.Connected
            };

            connected = controller.IsConnected;
            AngularStick = new Vector3();
            AngularVelocity = new Vector3();
            Acceleration = new float[3];

            Thread UpdateThread = new Thread(Update);
            UpdateThread.Start();
        }

        public void SetVirtualController(IXbox360Controller _vcontroller)
        {
            vcontroller = _vcontroller;
        }

        UdpServer server;
        public void SetUdpServer(UdpServer _server)
        {
            server = _server;
        }

        public void SetGyroscope(XInputGirometer _gyrometer)
        {
            gyrometer = _gyrometer;
            gyrometer.ReadingChanged += Girometer_ReadingChanged;
        }

        public void SetAccelerometer(XInputAccelerometer _accelerometer)
        {
            accelerometer = _accelerometer;
            accelerometer.ReadingChanged += Accelerometer_ReadingChanged;
        }

        private void Accelerometer_ReadingChanged(object sender, XInputAccelerometerReadingChangedEventArgs e)
        {
            Acceleration[0] = e.AccelerationX;
            Acceleration[1] = e.AccelerationY;
            Acceleration[2] = e.AccelerationZ;

            if (server != null)
                server.NewReportIncoming(ref meta, this, udpOutBuffers[0]);
        }

        private void Girometer_ReadingChanged(object sender, XInputGirometerReadingChangedEventArgs e)
        {
            AngularStick = new Vector3()
            {
                X = e.AngularStickX,
                Y = e.AngularStickY,
                Z = e.AngularStickZ,
            };

            AngularVelocity = new Vector3()
            {
                X = e.AngularVelocityX,
                Y = e.AngularVelocityY,
                Z = e.AngularVelocityZ
            };
        }

        void Update()
        {
            while (connected)
            {
                gamepad = controller.GetState().Gamepad;

                // push the values
                if (vcontroller != null)
                {
                    short ThumbX = (short)Math.Max(-32767, Math.Min(32767, gamepad.RightThumbX + (gyrometer.EnableGyroAiming ? AngularStick.X : 0)));
                    short ThumbY = (short)Math.Max(-32767, Math.Min(32767, gamepad.RightThumbY + (gyrometer.EnableGyroAiming ? AngularStick.Y : 0)));

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

                Thread.Sleep(rate);
            }
        }
    }
}
