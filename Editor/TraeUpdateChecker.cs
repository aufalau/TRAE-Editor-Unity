//Copyright [2026] Bytedance Ltd. and its affiliates. All rights reserved.
using System.Text.RegularExpressions;
using Microsoft.Unity.VisualStudio.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ByteDance.IDE.Trae.Editor
{
	/// <summary>
	/// Checks for newer versions of the Trae Editor package on every Unity startup
	/// by fetching the remote package.json from GitLab and comparing version strings.
	/// Uses a session-scoped flag to ensure only one check per editor session.
	/// </summary>
	[InitializeOnLoad]
	internal static class TraeUpdateChecker
	{
		private const string PackageName = "com.bytedance.ide.trae";
		private const string RemotePackageJsonUrl =
			"https://code.byted.org/pico-xr-sdk/TraeEditor_Unity/raw/master/package.json";
		private const string SkipVersionKey = PackageName + ".SkipVersion";

		// Session-scoped flag: resets every time the editor process starts.
		private static bool s_checkedThisSession;

		static TraeUpdateChecker()
		{
			if (!UnityInstallation.IsMainUnityEditorProcess)
				return;

			EditorApplication.delayCall += CheckOnce;
		}

		private static void CheckOnce()
		{
			if (s_checkedThisSession)
				return;
			s_checkedThisSession = true;

			var localVersion = GetInstalledVersion();
			if (string.IsNullOrEmpty(localVersion))
				return;

			FetchRemoteVersion(remoteVersion =>
			{
				if (string.IsNullOrEmpty(remoteVersion))
					return;

				// If the user chose to skip this exact version, don't prompt again.
				var skipped = EditorPrefs.GetString(SkipVersionKey, string.Empty);
				if (skipped == remoteVersion)
					return;

				if (!IsNewer(remoteVersion, localVersion))
					return;

				var result = EditorUtility.DisplayDialogComplex(
					"Trae Editor Update Available",
					$"A new version of Trae Editor is available.\n\n" +
					$"Installed: {localVersion}\n" +
					$"Available: {remoteVersion}\n\n" +
					"You can update via Window > Package Manager.",
					"Open Package Manager",
					"Skip This Version",
					"Remind Me Later");

				switch (result)
				{
					case 0: // Open Package Manager
						UnityEditor.PackageManager.UI.Window.Open(PackageName);
						break;
					case 1: // Skip This Version
						EditorPrefs.SetString(SkipVersionKey, remoteVersion);
						break;
					// case 2: Remind Me Later — do nothing, will check again next session.
				}
			});
		}

		private static string GetInstalledVersion()
		{
			try
			{
				var info = PackageInfo.FindForAssembly(typeof(TraeUpdateChecker).Assembly);
				if (info != null && !string.IsNullOrEmpty(info.version))
					return info.version;
			}
			catch
			{
				// Silently fall back if PackageInfo is unavailable.
			}
			return null;
		}

		private static void FetchRemoteVersion(System.Action<string> callback)
		{
			var request = UnityWebRequest.Get(RemotePackageJsonUrl);
			request.timeout = 10; // 10 seconds timeout for internal network
			var operation = request.SendWebRequest();
			operation.completed += _ =>
			{
				string version = null;
				if (request.result == UnityWebRequest.Result.Success)
				{
					// Parse "version": "x.y.z" from the JSON without a full JSON parser.
					var match = Regex.Match(
						request.downloadHandler.text,
						@"""version""\s*:\s*""([^""]+)""");
					if (match.Success)
						version = match.Groups[1].Value;
				}
				request.Dispose();
				callback(version);
			};
		}

		/// <summary>
		/// Returns true if <paramref name="remote"/> is a higher version than <paramref name="local"/>.
		/// </summary>
		private static bool IsNewer(string remote, string local)
		{
			if (TryParseVersion(remote, out var r) && TryParseVersion(local, out var l))
				return r > l;
			// Fallback: lexicographic comparison.
			return string.Compare(remote, local, System.StringComparison.Ordinal) > 0;
		}

		private static bool TryParseVersion(string input, out System.Version version)
		{
			version = null;
			if (string.IsNullOrEmpty(input))
				return false;
			// Strip leading 'v' if present (e.g. "v1.0.3" → "1.0.3").
			if (input.Length > 0 && (input[0] == 'v' || input[0] == 'V'))
				input = input.Substring(1);
			return System.Version.TryParse(input, out version);
		}
	}
}
