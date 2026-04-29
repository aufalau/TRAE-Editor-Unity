using UnityEditor;
using UnityEngine;
using System.Runtime.InteropServices;
using System;

namespace ByteDance.IDE.Trae.Editor
{
    /// <summary>
    /// Handles simple analytics collection for the Trae plugin (import and usage tracking).
    /// Uses applogrs native library for telemetry.
    /// </summary>
    [InitializeOnLoad]
    public static class TraeAnalytics
    {
        // Used to record whether the "first import" has been reported, preventing duplicate reports every time the project is opened.
        private const string k_HasTrackedImportPref = "TraeEditor_HasTrackedImport_v1";

        // Native library name
        private const string DllName = "applogrs";

        private const byte k_XorKey = 0xA5;

        private static readonly byte[] k_AppIdEncoded = { 0x92, 0x97, 0x97, 0x91, 0x91, 0x97 };

        private static readonly byte[] k_ChannelEncoded = { 0xF5, 0xEC, 0xE6, 0xEA, 0xFA, 0xF0, 0xCB, 0xCC, 0xD1, 0xDC, 0xF6, 0xE1, 0xEE };

        private static string AppID => XorDecode(k_AppIdEncoded);
        private static string Channel => XorDecode(k_ChannelEncoded);

        private static string XorDecode(byte[] encoded)
        {
            var chars = new char[encoded.Length];
            for (int i = 0; i < encoded.Length; i++)
                chars[i] = (char)(encoded[i] ^ k_XorKey);
            return new string(chars);
        }

        private static bool s_IsInited = false;

        // Static constructor, combined with [InitializeOnLoad], automatically executes when the Unity Editor loads or code recompiles.
        static TraeAnalytics()
        {
            // Delay execution by one frame to ensure the Editor environment is fully initialized.
            EditorApplication.delayCall += TrackUsage;
        }

        private static void TryInitAppLog()
        {
            if (s_IsInited)
            {
                return;
            }

            try
            {
                AppLog_init(AppID, Channel);
                s_IsInited = true;
            }
            catch (Exception e)
            {
                // Silent fail for telemetry init
            }
        }

        private static void TrackUsage()
        {
            // 1. Check if this is the first import.
            if (!EditorPrefs.GetBool(k_HasTrackedImportPref, false))
            {
                ReportEvent("trae_editor_imported", "{\"status\":\"success\"}");
                
                // Mark as reported so the import event is not triggered repeatedly on this computer/project.
                EditorPrefs.SetBool(k_HasTrackedImportPref, true);
            }

            // 2. Check if the plugin is currently being "used".
            // Get the path of the currently configured external script editor in Unity.
            string externalEditor = EditorPrefs.GetString("kScriptsDefaultApp", "");
            
            // If the path contains "Trae", it indicates the developer is using it.
            bool isTraeActive = externalEditor.Contains("Trae") || externalEditor.Contains("trae");
            
            if (isTraeActive)
            {
                ReportEvent("trae_editor_active", "{\"status\":\"active\"}");
            }
        }

        /// <summary>
        /// The actual analytics reporting logic using applogrs.
        /// </summary>
        private static void ReportEvent(string eventName, string jsonParam)
        {
            try
            {
                TryInitAppLog();
                if (s_IsInited)
                {
                    AppLog_onEvent(eventName, jsonParam);
                }
            }
            catch (Exception e)
            {
                // Silent fail for telemetry
            }
        }

        // --- Native DllImports for applogrs ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void AppLog_init(string appid, string channel);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void AppLog_onEvent(string eventName, string param);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern UInt64 AppLog_getDeviceId();
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void AppLog_setEventVerifyEnabled(uint enabled);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void AppLog_setLogEnabled(uint enabled);

        public delegate void DestroyCallback();
        
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void AppLog_destroy(DestroyCallback destory_callback);
    }
}