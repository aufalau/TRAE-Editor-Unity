//Copyright [2026] Bytedance Ltd. and its affiliates. All rights reserved.
using System.Linq;
using Microsoft.Unity.VisualStudio.Editor;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ByteDance.IDE.Trae.Editor
{
	/// <summary>
	/// Shows a one-time welcome dialog after the Trae Editor package is imported,
	/// so users can opt to set Trae as the default External Script Editor without
	/// digging into Preferences manually.
	/// The prompt is keyed by project + package version, so upgrading or moving to
	/// another project will surface it again, but repeated domain reloads will not.
	/// </summary>
	[InitializeOnLoad]
	internal static class TraeFirstRunWelcome
	{
		private const string PackageName = "com.bytedance.ide.trae";
		private const string PromptShownKeyPrefix = PackageName + ".FirstRunPromptShown.";
		private const string ExternalEditorKey = "kScriptsDefaultApp";

		static TraeFirstRunWelcome()
		{
			if (!UnityInstallation.IsMainUnityEditorProcess)
				return;

			// Defer until the editor is fully initialized so dialogs don't collide with load-time work.
			EditorApplication.delayCall += TryShowWelcome;
		}

		private static string GetPromptShownKey()
		{
			// Scope by project path + package version so that:
			//   - Same project + same package version → only prompt once.
			//   - New project or upgraded package → prompt again.
			var version = GetPackageVersion();
			var projectHash = Application.dataPath.GetHashCode().ToString("X");
			return PromptShownKeyPrefix + projectHash + "." + version;
		}

		private static string GetPackageVersion()
		{
			try
			{
				var info = PackageInfo.FindForAssembly(typeof(TraeFirstRunWelcome).Assembly);
				if (info != null && !string.IsNullOrEmpty(info.version))
					return info.version;
			}
			catch
			{
				// PackageInfo may not be available in all contexts; fall back silently.
			}
			return "unknown";
		}

		private static void TryShowWelcome()
		{
			var promptKey = GetPromptShownKey();
			if (EditorPrefs.GetBool(promptKey, false))
				return;

			// If the user already uses Trae, silently mark as shown and move on.
			var current = EditorPrefs.GetString(ExternalEditorKey, string.Empty);
			if (IsTraePath(current))
			{
				EditorPrefs.SetBool(promptKey, true);
				return;
			}

			var installation = FindFirstTraeInstallation();
			if (installation == null)
			{
				// No Trae found locally. Don't nag the user — mark as shown to avoid future popups.
				EditorPrefs.SetBool(promptKey, true);
				return;
			}

			var codeEditorInstallation = installation.ToCodeEditorInstallation();
			var message =
				$"Detected: {codeEditorInstallation.Name}\n{installation.Path}\n\n" +
				"Would you like to set Trae as your default external script editor? " +
				"You can change this later in Edit > Preferences > External Tools.";

			var result = EditorUtility.DisplayDialogComplex(
				"Welcome to Trae Editor",
				message,
				"Set Trae as Default",
				"Don't Ask Again",
				"Open Preferences");

			switch (result)
			{
				case 0: // Set as default
					CodeEditor.SetExternalScriptEditor(installation.Path);
					EditorPrefs.SetBool(promptKey, true);
					break;
				case 1: // Don't ask again
					EditorPrefs.SetBool(promptKey, true);
					break;
				case 2: // Open Preferences
					SettingsService.OpenUserPreferences("Preferences/External Tools");
					EditorPrefs.SetBool(promptKey, true);
					break;
			}
		}

		private static IVisualStudioInstallation FindFirstTraeInstallation()
		{
			try
			{
				return Discovery
					.GetVisualStudioInstallations()
					.OrderByDescending(i => IsTraePath(i.Path))
					.FirstOrDefault();
			}
			catch
			{
				return null;
			}
		}

		private static bool IsTraePath(string path)
		{
			return !string.IsNullOrEmpty(path)
				&& path.IndexOf("trae", System.StringComparison.OrdinalIgnoreCase) >= 0;
		}
	}
}
