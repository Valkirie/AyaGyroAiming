﻿using Nefarius.ViGEm.Client.Targets;
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
        private Settings settings;

        private UdpServer server;
        private IXbox360Controller vcontroller;

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

            Thread UpdateThread = new Thread(Update);
            UpdateThread.Start();
        }

        public void SetVirtualController(IXbox360Controller _vcontroller)
        {
            vcontroller = _vcontroller;
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

        private void Update()
        {
            // Poll events from joystick
            State previousState = controller.GetState();

            while (connected)
            {
                // todo:    allow users to set gyro2stick to either right or left stick
                if (vcontroller == null)
                    continue;

                if (server != null)
                    server.NewReportIncoming(this);

                State state = controller.GetState();
                if (previousState.PacketNumber != state.PacketNumber)
                {
                    gamepad = controller.GetState().Gamepad;

                    vcontroller.SetAxisValue(Xbox360Axis.LeftThumbX, gamepad.LeftThumbX);
                    vcontroller.SetAxisValue(Xbox360Axis.LeftThumbY, gamepad.LeftThumbY);

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
                }

                bool HasTrigger = HasTriggerPressed();
                short ThumbX = (short)Math.Max(-32767, Math.Min(32767, gamepad.RightThumbX + (settings.GyroAiming && HasTrigger ? AngularStick.X : 0)));
                short ThumbY = (short)Math.Max(-32767, Math.Min(32767, gamepad.RightThumbY + (settings.GyroAiming && HasTrigger ? AngularStick.Y : 0)));

                vcontroller.SetAxisValue(Xbox360Axis.RightThumbX, ThumbX);
                vcontroller.SetAxisValue(Xbox360Axis.RightThumbY, ThumbY);

                Thread.Sleep((int)settings.PullRate);
                previousState = state;
            }
        }
    }
}
