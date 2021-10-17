using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using SharpDX.XInput;
using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Threading;

namespace AyaGyroAiming
{
    public class XInputController
    {
        private Controller controller;
        private Gamepad gamepad;
        private Settings settings;

        private UdpServer server;

        private IXbox360Controller xcontroller;
        private IDualShock4Controller dcontroller;

        public XInputGirometer gyrometer;
        public XInputAccelerometer accelerometer;

        public Vector3 AngularStick;
        public Vector3 AngularVelocity;
        public Vector3 Acceleration;

        public UserIndex index;
        public bool connected = false;

        public XInputController(UserIndex _idx, Settings _settings, PhysicalAddress PadMacAddress)
        {
            controller = new Controller(_idx);
            settings = _settings;

            index = _idx;

            connected = controller.IsConnected;

            // initialize vectors
            AngularStick = new Vector3();
            AngularVelocity = new Vector3();
            Acceleration = new Vector3();
        }

        public void SetVirtualController(IXbox360Controller _controller)
        {
            xcontroller = _controller;
            // _controller.FeedbackReceived += _controller_FeedbackReceived;

            Thread UpdateThread = new Thread(UpdateXbox);
            UpdateThread.Start();
        }

        private void _controller_FeedbackReceived(object sender, Xbox360FeedbackReceivedEventArgs e)
        {
            // todo : implement me
            throw new NotImplementedException();
        }

        public void SetVirtualController(IDualShock4Controller _controller)
        {
            dcontroller = _controller;
            dcontroller.AutoSubmitReport = false;
            // _controller.FeedbackReceived += _controller_FeedbackReceived;

            Thread UpdateThread = new Thread(UpdateDS4);
            UpdateThread.Start();
        }

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
        }

        public void UpdateSettings(Settings _settings)
        {
            settings = _settings;
            gyrometer.UpdateSettings(_settings);
        }

        private bool HasTriggerPressed()
        {
            if (settings.Trigger == null || settings.Trigger == "")
                return true;

            switch (settings.Trigger)
            {
                case "LeftTrigger":
                    return gamepad.LeftTrigger != 0;
                case "RightTrigger":
                    return gamepad.RightTrigger != 0;
                default:
                    return false;
            }
        }

        private byte NormalizeInput(short input)
        {
            input = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, input));
            float output = (float)input / (float)ushort.MaxValue * (float)byte.MaxValue + (float)(byte.MaxValue / 2.0f);
            return (byte)output;
        }

        private byte[] buffer = new byte[63];
        private void UpdateDS4()
        {
            // Poll events from joystick
            State previousState = controller.GetState();
            ushort tempButtons;
            DualShock4DPadDirection tempDPad;

            while (connected)
            {
                // todo:    allow users to set gyro2stick to either right or left stick
                if (dcontroller == null)
                    continue;

                if (server != null)
                    server.NewReportIncoming(this);

                State state = controller.GetState();

                if (previousState.PacketNumber != state.PacketNumber)
                {
                    gamepad = controller.GetState().Gamepad;

                    tempButtons = 0;
                    tempDPad = DualShock4DPadDirection.None;

                    // redundant, move me !
                    long microseconds = server.sw.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L));

                    buffer[0] = NormalizeInput(gamepad.LeftThumbX); // Left Stick X
                    buffer[1] = (byte)(byte.MaxValue - NormalizeInput(gamepad.LeftThumbY)); // Left Stick Y

                    buffer[2] = NormalizeInput(gamepad.RightThumbX); ; // Right Stick X
                    buffer[3] = (byte)(byte.MaxValue - NormalizeInput(gamepad.RightThumbY)); // Right Stick Y

                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.A))
                        tempButtons |= DualShock4Button.Cross.Value;
                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.B))
                        tempButtons |= DualShock4Button.Circle.Value;
                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.X))
                        tempButtons |= DualShock4Button.Square.Value;
                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.Y))
                        tempButtons |= DualShock4Button.Triangle.Value;

                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.Start))
                        tempButtons |= DualShock4Button.Options.Value;
                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.Back))
                        tempButtons |= DualShock4Button.Share.Value;

                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb))
                        tempButtons |= DualShock4Button.ThumbRight.Value;
                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb))
                        tempButtons |= DualShock4Button.ThumbLeft.Value;

                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder))
                        tempButtons |= DualShock4Button.ShoulderRight.Value;
                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder))
                        tempButtons |= DualShock4Button.ShoulderLeft.Value;

                    if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp) && gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight)) tempDPad = DualShock4DPadDirection.Northeast;
                    else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp) && gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft)) tempDPad = DualShock4DPadDirection.Northwest;
                    else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp)) tempDPad = DualShock4DPadDirection.North;
                    else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight) && gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown)) tempDPad = DualShock4DPadDirection.Southeast;
                    else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight)) tempDPad = DualShock4DPadDirection.East;
                    else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown) && gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft)) tempDPad = DualShock4DPadDirection.Southwest;
                    else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown)) tempDPad = DualShock4DPadDirection.South;
                    else if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft)) tempDPad = DualShock4DPadDirection.West;

                    tempButtons |= tempDPad.Value;
                    buffer[4] = (byte)tempButtons; // dpad
                    buffer[5] = (byte)((short)tempButtons >> 8); // dpad

                    buffer[7] = gamepad.LeftTrigger; // Left Trigger
                    buffer[8] = gamepad.RightTrigger; // Right Trigger
                    buffer[9] = (byte)microseconds;                    // timestamp
                    buffer[10] = (byte)((ushort)microseconds >> 8);    // timestamp
                    buffer[11] = (byte)0xff; // battery

                    // wGyro
                    buffer[12] = (byte)AngularVelocity.X;
                    buffer[13] = (byte)((short)AngularVelocity.X >> 8);
                    buffer[14] = (byte)AngularVelocity.Y;
                    buffer[15] = (byte)((short)AngularVelocity.Y >> 8);
                    buffer[16] = (byte)AngularVelocity.Z;
                    buffer[17] = (byte)((short)AngularVelocity.Z >> 8);

                    // wAccel
                    buffer[18] = (byte)Acceleration.X;
                    buffer[19] = (byte)((short)Acceleration.X >> 8);
                    buffer[20] = (byte)Acceleration.Y;
                    buffer[21] = (byte)((short)Acceleration.Y >> 8);
                    buffer[22] = (byte)Acceleration.Z;
                    buffer[23] = (byte)((short)Acceleration.Z >> 8);

                    buffer[29] = (byte)0xff; // battery
                    dcontroller.SubmitRawReport(buffer);
                }

                Thread.Sleep((int)settings.PullRate);
                previousState = state;
            }
        }

        private void UpdateXbox()
        {
            // Poll events from joystick
            State previousState = controller.GetState();

            while (connected)
            {
                // todo:    allow users to set gyro2stick to either right or left stick
                if (xcontroller == null && dcontroller == null)
                    continue;

                if (server != null)
                    server.NewReportIncoming(this);

                State state = controller.GetState();
                bool HasTrigger = HasTriggerPressed(); // move me !

                if (previousState.PacketNumber != state.PacketNumber)
                {
                    gamepad = controller.GetState().Gamepad;

                    xcontroller.SetAxisValue(Xbox360Axis.LeftThumbX, gamepad.LeftThumbX);
                    xcontroller.SetAxisValue(Xbox360Axis.LeftThumbY, gamepad.LeftThumbY);

                    xcontroller.SetSliderValue(Xbox360Slider.LeftTrigger, gamepad.LeftTrigger);
                    xcontroller.SetSliderValue(Xbox360Slider.RightTrigger, gamepad.RightTrigger);

                    xcontroller.SetButtonState(Xbox360Button.LeftShoulder, gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder));
                    xcontroller.SetButtonState(Xbox360Button.RightShoulder, gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder));

                    xcontroller.SetButtonState(Xbox360Button.LeftThumb, gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb));
                    xcontroller.SetButtonState(Xbox360Button.RightThumb, gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb));

                    xcontroller.SetButtonState(Xbox360Button.A, gamepad.Buttons.HasFlag(GamepadButtonFlags.A));
                    xcontroller.SetButtonState(Xbox360Button.B, gamepad.Buttons.HasFlag(GamepadButtonFlags.B));
                    xcontroller.SetButtonState(Xbox360Button.X, gamepad.Buttons.HasFlag(GamepadButtonFlags.X));
                    xcontroller.SetButtonState(Xbox360Button.Y, gamepad.Buttons.HasFlag(GamepadButtonFlags.Y));

                    xcontroller.SetButtonState(Xbox360Button.Up, gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp));
                    xcontroller.SetButtonState(Xbox360Button.Down, gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown));
                    xcontroller.SetButtonState(Xbox360Button.Left, gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft));
                    xcontroller.SetButtonState(Xbox360Button.Right, gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight));

                    xcontroller.SetButtonState(Xbox360Button.Start, gamepad.Buttons.HasFlag(GamepadButtonFlags.Start));
                    xcontroller.SetButtonState(Xbox360Button.Back, gamepad.Buttons.HasFlag(GamepadButtonFlags.Back));
                }

                short ThumbX = (short)Math.Max(-32767, Math.Min(32767, gamepad.RightThumbX + (settings.GyroAiming && HasTrigger ? AngularStick.X : 0)));
                short ThumbY = (short)Math.Max(-32767, Math.Min(32767, gamepad.RightThumbY + (settings.GyroAiming && HasTrigger ? AngularStick.Y : 0)));
                xcontroller.SetAxisValue(Xbox360Axis.RightThumbX, ThumbX);
                xcontroller.SetAxisValue(Xbox360Axis.RightThumbY, ThumbY);

                Thread.Sleep((int)settings.PullRate);
                previousState = state;
            }
        }
    }
}
