using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using SharpDX.XInput;
using System;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Threading;

namespace AyaGyroAiming
{
    public class XInputController
    {
        private Controller controller;
        private Gamepad gamepad;
        private int rate;

        private UdpStatus MotionStatus;
        private IXbox360Controller vcontroller;
        private DualShockPadMeta meta;

        public XInputGirometer gyrometer;
        public XInputAccelerometer accelerometer;

        public Vector3 AngularStick;
        public Vector3 AngularVelocity;
        public Vector3 Acceleration;

        [Flags]
        public enum UdpStatus
        {
            None = 0,
            HasGyroscope = 1,
            HasAccelerometer = 2
        }

        public UserIndex index;
        public bool connected = false;

        private byte[][] udpOutBuffers = new byte[UdpServer.NUMBER_SLOTS][]
        {
            new byte[100], new byte[100],
            new byte[100], new byte[100],
        };

        public XInputController(UserIndex _idx, int _rate, PhysicalAddress PadMacAddress)
        {
            controller = new Controller(_idx);
            index = _idx;
            rate = _rate;

            // fake data for initialization
            meta = new DualShockPadMeta()
            {
                BatteryStatus = DsBattery.Full,
                ConnectionType = DsConnection.Bluetooth,
                IsActive = true,
                PadId = (byte)0, //_idx
                PadMacAddress = PadMacAddress,
                Model = DsModel.DS4,
                PadState = DsState.Connected
            };

            connected = controller.IsConnected;

            // initialize vectors
            AngularStick = new Vector3();
            AngularVelocity = new Vector3();
            Acceleration = new Vector3();

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
            // used for udp server
            Acceleration.X = e.AccelerationX;
            Acceleration.Y = e.AccelerationY;
            Acceleration.Z = e.AccelerationZ;
            FlagsHelper.Set(ref MotionStatus, UdpStatus.HasAccelerometer);
        }

        private void Girometer_ReadingChanged(object sender, XInputGirometerReadingChangedEventArgs e)
        {
            // used for gyro2stick
            AngularStick.X = e.AngularStickX;
            AngularStick.Y = e.AngularStickY;
            AngularStick.Z = e.AngularStickZ;

            // used for udp server
            AngularVelocity.X = e.AngularVelocityX;
            AngularVelocity.Y = e.AngularVelocityY;
            AngularVelocity.Z = e.AngularVelocityZ;
            FlagsHelper.Set(ref MotionStatus, UdpStatus.HasGyroscope);
        }

        // temp
        private string TriggerString = "LeftTrigger";
        private bool TriggerPressed = false;
        private Settings settings;

        public void UpdateSettings(Settings _settings)
        {
            settings = _settings;
            gyrometer.UpdateSettings(_settings);
        }

        private bool HasKey()
        {
            return TriggerString != "";
        }

        private void UpdateTrigger()
        {
            if (HasKey())
            {
                switch (TriggerString)
                {
                    case "LeftTrigger":
                        TriggerPressed = gamepad.LeftTrigger != 0;
                        break;
                    case "RightTrigger":
                        TriggerPressed = gamepad.RightTrigger != 0;
                        break;
                }
            }
        }

        private void Update()
        {
            while (connected)
            {
                gamepad = controller.GetState().Gamepad;

                if (server != null && MotionStatus.HasFlag(UdpStatus.HasGyroscope) && MotionStatus.HasFlag(UdpStatus.HasAccelerometer))
                {
                    server.NewReportIncoming(ref meta, this, udpOutBuffers[0]);
                    FlagsHelper.Unset(ref MotionStatus, UdpStatus.HasAccelerometer);
                    FlagsHelper.Unset(ref MotionStatus, UdpStatus.HasGyroscope);
                }

                // todo:    allow users to set gyro2stick to either right or left stick
                //          allow users to set triggers to enable gyro2stick (while aiming in games, etc..)

                if (vcontroller == null || gyrometer == null)
                    continue;

                UpdateTrigger();

                short ThumbX = (short)(gamepad.RightThumbX + (settings.EnableGyroAiming && TriggerPressed ? AngularStick.X : 0));
                short ThumbY = (short)(gamepad.RightThumbY + (settings.EnableGyroAiming && TriggerPressed ? AngularStick.Y : 0));

                vcontroller.SetAxisValue(Xbox360Axis.LeftThumbX, gamepad.LeftThumbX);
                vcontroller.SetAxisValue(Xbox360Axis.LeftThumbY, gamepad.LeftThumbY);

                vcontroller.SetAxisValue(Xbox360Axis.RightThumbX, Math.Max((short)-32767, Math.Min((short)32767, ThumbX)));
                vcontroller.SetAxisValue(Xbox360Axis.RightThumbY, Math.Max((short)-32767, Math.Min((short)32767, ThumbY)));

                vcontroller.SetSliderValue(Xbox360Slider.LeftTrigger, gamepad.LeftTrigger);
                vcontroller.SetSliderValue(Xbox360Slider.RightTrigger, gamepad.RightTrigger);

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

                Thread.Sleep(rate);
            }
        }
    }
}
