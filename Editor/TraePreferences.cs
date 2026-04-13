//Copyright [2026] Bytedance Ltd. and its affiliates. All rights reserved.
using UnityEditor;

namespace Microsoft.Unity.VisualStudio.Editor
{
	internal static class TraePreferences
	{
		private const string Prefix = "com.bytedance.ide.trae.";
		private const string AutoCopyProjectRulesKey = Prefix + "AutoCopyProjectRules";
		private const string IncludeExternalPackagesKey = Prefix + "IncludeExternalPackages";
		private const string ShowLibraryKey = Prefix + "ShowLibrary";

		internal static bool AutoCopyProjectRules
		{
			get => EditorPrefs.GetBool(AutoCopyProjectRulesKey, true);
			set => EditorPrefs.SetBool(AutoCopyProjectRulesKey, value);
		}

		internal static bool IncludeExternalPackages
		{
			get => EditorPrefs.GetBool(IncludeExternalPackagesKey, true);
			set => EditorPrefs.SetBool(IncludeExternalPackagesKey, value);
		}

		internal static bool ShowLibrary
		{
			get => EditorPrefs.GetBool(ShowLibraryKey, true);
			set => EditorPrefs.SetBool(ShowLibraryKey, value);
		}
	}
}

