﻿//------------------------------------------------------------------------------
// <auto-generated>
//     Ce code a été généré par un outil.
//     Version du runtime :4.0.30319.42000
//
//     Les modifications apportées à ce fichier peuvent provoquer un comportement incorrect et seront perdues si
//     le code est régénéré.
// </auto-generated>
//------------------------------------------------------------------------------

namespace AyaGyroAiming.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "16.10.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool EnableGyroAiming {
            get {
                return ((bool)(this["EnableGyroAiming"]));
            }
            set {
                this["EnableGyroAiming"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("1")]
        public uint GyroMaxSample {
            get {
                return ((uint)(this["GyroMaxSample"]));
            }
            set {
                this["GyroMaxSample"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("10000")]
        public float GyroStickRange {
            get {
                return ((float)(this["GyroStickRange"]));
            }
            set {
                this["GyroStickRange"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool GyroStickInvertAxisX {
            get {
                return ((bool)(this["GyroStickInvertAxisX"]));
            }
            set {
                this["GyroStickInvertAxisX"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool GyroStickInvertAxisY {
            get {
                return ((bool)(this["GyroStickInvertAxisY"]));
            }
            set {
                this["GyroStickInvertAxisY"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool GyroStickInvertAxisZ {
            get {
                return ((bool)(this["GyroStickInvertAxisZ"]));
            }
            set {
                this["GyroStickInvertAxisZ"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("10")]
        public uint GyroPullRate {
            get {
                return ((uint)(this["GyroPullRate"]));
            }
            set {
                this["GyroPullRate"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("0.5")]
        public float GyroStickAggressivity {
            get {
                return ((float)(this["GyroStickAggressivity"]));
            }
            set {
                this["GyroStickAggressivity"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("26760")]
        public int UdpPort {
            get {
                return ((int)(this["UdpPort"]));
            }
            set {
                this["UdpPort"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("<?xml version=\"1.0\" encoding=\"utf-16\"?>\r\n<ArrayOfString xmlns:xsi=\"http://www.w3." +
            "org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">\r\n  <s" +
            "tring>Controller (XBOX 360 For Windows)</string>\r\n</ArrayOfString>")]
        public global::System.Collections.Specialized.StringCollection HidHideDevices {
            get {
                return ((global::System.Collections.Specialized.StringCollection)(this["HidHideDevices"]));
            }
            set {
                this["HidHideDevices"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string HidHideDeviceID {
            get {
                return ((string)(this["HidHideDeviceID"]));
            }
            set {
                this["HidHideDeviceID"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string TriggerString {
            get {
                return ((string)(this["TriggerString"]));
            }
            set {
                this["TriggerString"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool EnableScreenRatio {
            get {
                return ((bool)(this["EnableScreenRatio"]));
            }
            set {
                this["EnableScreenRatio"] = value;
            }
        }
    }
}
