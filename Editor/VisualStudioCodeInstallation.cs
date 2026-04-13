//Copyright [2026] Bytedance Ltd. and its affiliates. All rights reserved.
/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using SimpleJSON;
using IOPath = System.IO.Path;

namespace Microsoft.Unity.VisualStudio.Editor
{
	internal class VisualStudioCodeInstallation : VisualStudioInstallation
	{
		private static readonly IGenerator _generator = GeneratorFactory.GetInstance(GeneratorStyle.SDK);

		public override bool SupportsAnalyzers
		{
			get
			{
				return true;
			}
		}

		public override Version LatestLanguageVersionSupported
		{
			get
			{
				return new Version(13, 0);
			}
		}

		private string GetExtensionPath()
		{
			var vscode = IsPrerelease ? ".vscode-insiders" : ".vscode";
			var extensionsPath = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), vscode, "extensions");
			if (!Directory.Exists(extensionsPath))
				return null;

			return Directory
				.EnumerateDirectories(extensionsPath, $"{MicrosoftUnityExtensionId}*") // publisherid.extensionid
				.OrderByDescending(n => n)
				.FirstOrDefault();
		}

		public override string[] GetAnalyzers()
		{
			var vstuPath = GetExtensionPath();
			if (string.IsNullOrEmpty(vstuPath))
				return Array.Empty<string>();

			return GetAnalyzers(vstuPath); }

		public override IGenerator ProjectGenerator
		{
			get
			{
				return _generator;
			}
		}

		private static bool IsCandidateForDiscovery(string path)
		{
#if UNITY_EDITOR_OSX
			return Directory.Exists(path) && Regex.IsMatch(path, ".*Trae.*.app$", RegexOptions.IgnoreCase);
#elif UNITY_EDITOR_WIN
			return File.Exists(path) && Regex.IsMatch(path, ".*Trae.*.exe$", RegexOptions.IgnoreCase);
#else
			return File.Exists(path) && path.EndsWith("trae", StringComparison.OrdinalIgnoreCase);
#endif
		}

		[Serializable]
		internal class VisualStudioCodeManifest
		{
			public string name;
			public string version;
		}

		public static bool TryDiscoverInstallation(string editorPath, out IVisualStudioInstallation installation)
		{
			installation = null;

			if (string.IsNullOrEmpty(editorPath))
				return false;

			if (!IsCandidateForDiscovery(editorPath))
				return false;

			Version version = null;
			var isPrerelease = false;

			try
			{
				var manifestBase = GetRealPath(editorPath);

#if UNITY_EDITOR_WIN
				// on Windows, editorPath is a file, resources as subdirectory
				manifestBase = IOPath.GetDirectoryName(manifestBase);
#elif UNITY_EDITOR_OSX
				// on Mac, editorPath is a directory
				manifestBase = IOPath.Combine(manifestBase, "Contents");
#else
				// on Linux, editorPath is a file, in a bin sub-directory
				var parent = Directory.GetParent(manifestBase);
				// but we can link to [vscode]/code or [vscode]/bin/code
				manifestBase = parent?.Name == "bin" ? parent.Parent?.FullName : parent?.FullName;
#endif

				if (manifestBase == null)
					return false;

				var manifestFullPath = IOPath.Combine(manifestBase, "resources", "app", "package.json");
				if (File.Exists(manifestFullPath))
				{
					var manifest = JsonUtility.FromJson<VisualStudioCodeManifest>(File.ReadAllText(manifestFullPath));
					Version.TryParse(manifest.version.Split('-').First(), out version);
					isPrerelease = manifest.version.ToLower().Contains("insider");
				}
			}
			catch (Exception)
			{
				// do not fail if we are not able to retrieve the exact version number
			}

			isPrerelease = isPrerelease || editorPath.ToLower().Contains("insider");
			var isTraeCN = editorPath.IndexOf("Trae CN", StringComparison.OrdinalIgnoreCase) != -1;

			installation = new VisualStudioCodeInstallation()
			{
				IsPrerelease = isPrerelease,
				Name = (isTraeCN ? "Trae CN" : "Trae") + (isPrerelease ? " - Insider" : string.Empty) + (version != null ? $" [{version.ToString(3)}]" : string.Empty),
				Path = editorPath,
				Version = version ?? new Version()
			};

			return true;
		}

		public static IEnumerable<IVisualStudioInstallation> GetVisualStudioInstallations()
		{
			var candidates = new List<string>();

			var envPath = Environment.GetEnvironmentVariable("TRAE_PATH");
			if (!string.IsNullOrEmpty(envPath))
				candidates.Add(envPath);

			var envExe = Environment.GetEnvironmentVariable("TRAE_EXE");
			if (!string.IsNullOrEmpty(envExe))
				candidates.Add(envExe);

#if UNITY_EDITOR_WIN
			var localAppPath = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");
			var programFiles = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));

			foreach (var basePath in new[] {localAppPath, programFiles})
			{
				candidates.Add(IOPath.Combine(basePath, "Trae", "Trae.exe"));
				candidates.Add(IOPath.Combine(basePath, "Trae Insiders", "Trae - Insiders.exe"));
				candidates.Add(IOPath.Combine(basePath, "Trae CN", "Trae CN.exe"));
			}
#elif UNITY_EDITOR_OSX
			var appPath = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
			candidates.AddRange(Directory.EnumerateDirectories(appPath, "Trae*.app"));
#elif UNITY_EDITOR_LINUX
			// Well known locations
			candidates.Add("/usr/bin/trae");
			candidates.Add("/bin/trae");
			candidates.Add("/usr/local/bin/trae");

			// Preference ordered base directories relative to which desktop files should be searched
			candidates.AddRange(GetXdgCandidates());
#endif

			foreach (var candidate in candidates.Distinct())
			{
				if (TryDiscoverInstallation(candidate, out var installation))
					yield return installation;
			}
		}

#if UNITY_EDITOR_LINUX
		private static readonly Regex DesktopFileExecEntry = new Regex(@"Exec=(\S+)", RegexOptions.Singleline | RegexOptions.Compiled);

		private static IEnumerable<string> GetXdgCandidates()
		{
			var envdirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
			if (string.IsNullOrEmpty(envdirs))
				yield break;

			var dirs = envdirs.Split(':');
			foreach(var dir in dirs)
			{
				Match match = null;

				try
				{
					var desktopFile = IOPath.Combine(dir, "applications/trae.desktop");
					if (!File.Exists(desktopFile))
						continue;
				
					var content = File.ReadAllText(desktopFile);
					match = DesktopFileExecEntry.Match(content);
				}
				catch
				{
					// do not fail if we cannot read desktop file
				}

				if (match == null || !match.Success)
					continue;

				yield return match.Groups[1].Value;
				break;
			}
		}

		[System.Runtime.InteropServices.DllImport ("libc")]
		private static extern int readlink(string path, byte[] buffer, int buflen);

		internal static string GetRealPath(string path)
		{
			byte[] buf = new byte[512];
			int ret = readlink(path, buf, buf.Length);
			if (ret == -1) return path;
			char[] cbuf = new char[512];
			int chars = System.Text.Encoding.Default.GetChars(buf, 0, ret, cbuf, 0);
			return new String(cbuf, 0, chars);
		}
#else
		internal static string GetRealPath(string path)
		{
			return path;
		}
#endif

		public override void CreateExtraFiles(string projectDirectory)
		{
			try
			{
				var vscodeDirectory = IOPath.Combine(projectDirectory.NormalizePathSeparators(), ".vscode");
				Directory.CreateDirectory(vscodeDirectory);

				var enablePatch = !File.Exists(IOPath.Combine(vscodeDirectory, ".vstupatchdisable"));

				CreateRecommendedExtensionsFile(vscodeDirectory, enablePatch);
				CreateSettingsFile(vscodeDirectory, enablePatch);
				CreateLaunchFile(vscodeDirectory, enablePatch);
			}
			catch (IOException)
			{
			}			
		}

		private const string DefaultLaunchFileContent = @"{
    ""version"": ""0.2.0"",
    ""configurations"": [
        {
            ""name"": ""Attach to Unity"",
            ""type"": ""vstuc"",
            ""request"": ""attach""
        }
     ]
}";

		private static void CreateLaunchFile(string vscodeDirectory, bool enablePatch)
		{
			var launchFile = IOPath.Combine(vscodeDirectory, "launch.json");
			if (File.Exists(launchFile))
			{
				if (enablePatch)
					PatchLaunchFile(launchFile);

				return;
			}

			File.WriteAllText(launchFile, DefaultLaunchFileContent);
		}

		private static void PatchLaunchFile(string launchFile)
		{
			try
			{
				const string configurationsKey = "configurations";
				const string typeKey = "type";

				var content = File.ReadAllText(launchFile);
				var launch = JSONNode.Parse(content);

				var configurations = launch[configurationsKey] as JSONArray;
				if (configurations == null)
				{
					configurations = new JSONArray();
					launch.Add(configurationsKey, configurations);
				}

				if (configurations.Linq.Any(entry => entry.Value[typeKey].Value == "vstuc"))
					return;

				var defaultContent = JSONNode.Parse(DefaultLaunchFileContent);
				configurations.Add(defaultContent[configurationsKey][0]);

				WriteAllTextFromJObject(launchFile, launch);
			}
			catch (Exception)
			{
				// do not fail if we cannot patch the launch.json file
			}
		}

		private void CreateSettingsFile(string vscodeDirectory, bool enablePatch)
		{
			var settingsFile = IOPath.Combine(vscodeDirectory, "settings.json");
			if (File.Exists(settingsFile))
			{
				if (enablePatch)
					PatchSettingsFile(settingsFile);

				return;
			}

			var libraryExcludes = TraePreferences.ShowLibrary ? string.Empty : @"        ""Library/"": true,
        ""library/"": true,
";

			var excludes = @"    ""files.exclude"": {
        ""**/.DS_Store"": true,
        ""**/.git"": true,
        ""**/.vs"": true,
        ""**/.gitmodules"": true,
        ""**/.vsconfig"": true,
        ""**/*.booproj"": true,
        ""**/*.pidb"": true,
        ""**/*.suo"": true,
        ""**/*.user"": true,
        ""**/*.userprefs"": true,
        ""**/*.unityproj"": true,
        ""**/*.dll"": true,
        ""**/*.exe"": true,
        ""**/*.pdf"": true,
        ""**/*.mid"": true,
        ""**/*.midi"": true,
        ""**/*.wav"": true,
        ""**/*.gif"": true,
        ""**/*.ico"": true,
        ""**/*.jpg"": true,
        ""**/*.jpeg"": true,
        ""**/*.png"": true,
        ""**/*.psd"": true,
        ""**/*.tga"": true,
        ""**/*.tif"": true,
        ""**/*.tiff"": true,
        ""**/*.3ds"": true,
        ""**/*.3DS"": true,
        ""**/*.fbx"": true,
        ""**/*.FBX"": true,
        ""**/*.lxo"": true,
        ""**/*.LXO"": true,
        ""**/*.ma"": true,
        ""**/*.MA"": true,
        ""**/*.obj"": true,
        ""**/*.OBJ"": true,
        ""**/*.asset"": true,
        ""**/*.cubemap"": true,
        ""**/*.flare"": true,
        ""**/*.mat"": true,
        ""**/*.meta"": true,
        ""**/*.prefab"": true,
        ""**/*.unity"": true,
        ""build/"": true,
        ""Build/"": true,
" + libraryExcludes + @"
        ""obj/"": true,
        ""Obj/"": true,
        ""Logs/"": true,
        ""logs/"": true,
        ""ProjectSettings/"": true,
        ""UserSettings/"": true,
        ""temp/"": true,
        ""Temp/"": true
    }";

			var content = @"{
" + excludes + @",
    ""files.associations"": {
        ""*.asset"": ""yaml"",
        ""*.meta"": ""yaml"",
        ""*.prefab"": ""yaml"",
        ""*.unity"": ""yaml"",
    },
    ""explorer.fileNesting.enabled"": true,
    ""explorer.fileNesting.patterns"": {
        ""*.sln"": ""*.csproj"",
        ""*.slnx"": ""*.csproj""
    },
    ""dotnet.defaultSolution"": """ + IOPath.GetFileName(ProjectGenerator.SolutionFile()) + @"""
}";

			File.WriteAllText(settingsFile, content);
		}

		private void PatchSettingsFile(string settingsFile)
		{
			try
			{
				const string excludesKey = "files.exclude";
				const string solutionKey = "dotnet.defaultSolution";

				var content = File.ReadAllText(settingsFile);
				var settings = JSONNode.Parse(content);

				var excludes = settings[excludesKey] as JSONObject;
				if (excludes == null)
					return;

				var patchList = new List<string>();
				var patched = false;

				// Remove files.exclude for solution+project files in the project root
				foreach (var exclude in excludes)
				{
					if (!bool.TryParse(exclude.Value, out var exc) || !exc)
						continue;

					var key = exclude.Key;

					if (!key.EndsWith(".sln") && !key.EndsWith(".csproj"))
						continue;

					if (!Regex.IsMatch(key, "^(\\*\\*[\\\\\\/])?\\*\\.(sln|csproj)$"))
						continue;

					patchList.Add(key);
					patched = true;
				}

				// Check default solution
				var defaultSolution = settings[solutionKey];
				var solutionFile = IOPath.GetFileName(ProjectGenerator.SolutionFile());
				if (defaultSolution == null || defaultSolution.Value != solutionFile)
				{
					settings[solutionKey] = solutionFile;
					patched = true;
				}

				if (TraePreferences.ShowLibrary)
				{
					if (excludes["Library/"] != null)
					{
						excludes.Remove("Library/");
						patched = true;
					}

					if (excludes["library/"] != null)
					{
						excludes.Remove("library/");
						patched = true;
					}
				}
				else
				{
					if (excludes["Library/"] == null || excludes["Library/"].Value != "true")
					{
						excludes["Library/"] = true;
						patched = true;
					}

					if (excludes["library/"] == null || excludes["library/"].Value != "true")
					{
						excludes["library/"] = true;
						patched = true;
					}
				}

				if (!patched)
					return;

				foreach (var patch in patchList)
					excludes.Remove(patch);

				WriteAllTextFromJObject(settingsFile, settings);
			}
			catch (Exception)
			{
				// do not fail if we cannot patch the settings.json file
			}
		}

		private const string MicrosoftUnityExtensionId = "visualstudiotoolsforunity.vstuc";
		private const string DefaultRecommendedExtensionsContent = @"{
    ""recommendations"": [
      """+ MicrosoftUnityExtensionId + @"""
    ]
}
";

		private static void CreateRecommendedExtensionsFile(string vscodeDirectory, bool enablePatch)
		{
			// see https://tattoocoder.com/recommending-vscode-extensions-within-your-open-source-projects/
			var extensionFile = IOPath.Combine(vscodeDirectory, "extensions.json");
			if (File.Exists(extensionFile))
			{
				if (enablePatch)
					PatchRecommendedExtensionsFile(extensionFile);

				return;
			}

			File.WriteAllText(extensionFile, DefaultRecommendedExtensionsContent);
		}

		private static void PatchRecommendedExtensionsFile(string extensionFile)
		{
			try
			{
				const string recommendationsKey = "recommendations";

				var content = File.ReadAllText(extensionFile);
				var extensions = JSONNode.Parse(content);

				var recommendations = extensions[recommendationsKey] as JSONArray;
				if (recommendations == null)
				{
					recommendations = new JSONArray();
					extensions.Add(recommendationsKey, recommendations);
				}

				if (recommendations.Linq.Any(entry => entry.Value.Value == MicrosoftUnityExtensionId))
					return;

				recommendations.Add(MicrosoftUnityExtensionId);
				WriteAllTextFromJObject(extensionFile, extensions);
			}
			catch (Exception)
			{
				// do not fail if we cannot patch the extensions.json file
			}
		}

		private static void WriteAllTextFromJObject(string file, JSONNode node)
		{
			using (var fs = File.Open(file, FileMode.Create))
			using (var sw = new StreamWriter(fs))
			{
				// Keep formatting/indent in sync with default contents
				sw.Write(node.ToString(aIndent: 4));
			}
		}

		public override bool Open(string path, int line, int column, string solution)
		{
			var application = Path;

			line = Math.Max(1, line);
			column = Math.Max(0, column);

			string solutionDirectory = null;
			if (!string.IsNullOrEmpty(solution))
				solutionDirectory = IOPath.GetDirectoryName(solution);

			var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;

			if (string.IsNullOrEmpty(solutionDirectory))
				solutionDirectory = projectRoot;

			var target = solutionDirectory;

			var absolutePath = string.IsNullOrEmpty(path) ? null : FileUtility.GetAbsolutePath(path);

			if (!string.IsNullOrEmpty(projectRoot))
			{
				if (TraePreferences.AutoCopyProjectRules)
				{
					EnsureProjectRules(projectRoot);
				}
			}

			if (TraePreferences.IncludeExternalPackages && !string.IsNullOrEmpty(absolutePath) && !string.IsNullOrEmpty(projectRoot) && !IsPathUnderDirectory(absolutePath, projectRoot))
			{
				var externalPackageRoot = TryFindUnityPackageRoot(absolutePath);
				if (!string.IsNullOrEmpty(externalPackageRoot))
				{
					var solutionFileName = string.IsNullOrEmpty(solution) ? string.Empty : IOPath.GetFileName(solution);
					var projectName = IOPath.GetFileName(projectRoot.TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar));
					var workspaceFile = EnsureTraeWorkspace(projectRoot, projectName, externalPackageRoot, solutionFileName);
					if (!string.IsNullOrEmpty(workspaceFile))
						target = workspaceFile;
				}
			}

			ProcessRunner.Start(string.IsNullOrEmpty(path)
				? ProcessStartInfoFor(application, $"\"{target}\"")
				: ProcessStartInfoFor(application, $"\"{target}\" -g \"{path}\":{line}:{column}"));

			return true;
		}

		private static bool IsPathUnderDirectory(string path, string directory)
		{
			if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(directory))
				return false;

			try
			{
				var normalizedPath = IOPath.GetFullPath(path.TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar));
				var normalizedDirectory = IOPath.GetFullPath(directory.TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar))
					+ IOPath.DirectorySeparatorChar;

				return normalizedPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
			}
			catch
			{
				return false;
			}
		}

		private static void EnsureProjectRules(string projectRoot)
		{
			try
			{
				var rulesDirectory = IOPath.Combine(projectRoot, ".trae", "rules");
				if (!Directory.Exists(rulesDirectory))
				{
					Directory.CreateDirectory(rulesDirectory);
				}

				var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(VisualStudioCodeInstallation).Assembly);
				if (package == null) return;

				var instructionsDir = IOPath.Combine(package.resolvedPath, "unity-code-style-guide", "UnitySpecificInstructions");

				var styleGuideFile = IOPath.Combine(instructionsDir, "UnityCodeStyleInstructions.md");
				var performanceFile = IOPath.Combine(instructionsDir, "UnityPerformanceOptimizationInstructions.md");

				var targetStyleGuideFile = IOPath.Combine(rulesDirectory, "UnityCodeStyleInstructions.md");
				var targetPerformanceFile = IOPath.Combine(rulesDirectory, "UnityPerformanceOptimizationInstructions.md");

				if (File.Exists(styleGuideFile) && !File.Exists(targetStyleGuideFile))
				{
					File.Copy(styleGuideFile, targetStyleGuideFile);
				}

				if (File.Exists(performanceFile) && !File.Exists(targetPerformanceFile))
				{
					File.Copy(performanceFile, targetPerformanceFile);
				}
			}
			catch
			{
				// do not fail if we cannot copy the rules
			}
		}

		private static string TryFindUnityPackageRoot(string filePath)
		{
			var currentDirectory = IOPath.GetDirectoryName(filePath);
			if (string.IsNullOrEmpty(currentDirectory) || !Directory.Exists(currentDirectory))
				return null;

			for (var i = 0; i < 16; i++)
			{
				var packageJson = IOPath.Combine(currentDirectory, "package.json");
				if (File.Exists(packageJson))
					return currentDirectory;

				var parent = Directory.GetParent(currentDirectory);
				if (parent == null)
					return null;

				currentDirectory = parent.FullName;
			}

			return null;
		}

		private static string EnsureTraeWorkspace(string projectRoot, string projectName, string externalPackageRoot, string solutionFileName)
		{
			if (string.IsNullOrEmpty(projectRoot) || string.IsNullOrEmpty(externalPackageRoot))
				return null;

			try
			{
				var safeProjectName = string.IsNullOrEmpty(projectName) ? "Unity" : MakeSafeFileName(projectName);
				var workspaceFile = IOPath.Combine(projectRoot, $"{safeProjectName}.trae.code-workspace");
				var workspace = File.Exists(workspaceFile) ? JSONNode.Parse(File.ReadAllText(workspaceFile)) : new JSONObject();

				var folders = workspace["folders"] as JSONArray;
				if (folders == null)
				{
					folders = new JSONArray();
					workspace["folders"] = folders;
				}

				if (!folders.Linq.Any(f => string.Equals(f.Value?["path"]?.Value, ".", StringComparison.OrdinalIgnoreCase)))
				{
					var rootFolder = new JSONObject();
					rootFolder["name"] = safeProjectName;
					rootFolder["path"] = ".";
					folders.Add(rootFolder);
				}

				var externalPackageName = TryGetUnityPackageName(externalPackageRoot);
				var externalFolderName = string.IsNullOrEmpty(externalPackageName)
					? IOPath.GetFileName(externalPackageRoot.TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar))
					: externalPackageName;
				var externalPackagePath = FileUtility.GetAbsolutePath(externalPackageRoot);
				if (!string.IsNullOrEmpty(externalPackageName))
				{
					for (var i = folders.Count - 1; i >= 0; i--)
					{
						var existingFolder = folders[i];
						var existingName = existingFolder?["name"]?.Value;
						if (!string.Equals(existingName, externalPackageName, StringComparison.OrdinalIgnoreCase))
							continue;

						var existingPath = existingFolder?["path"]?.Value;
						if (!string.Equals(existingPath, ".", StringComparison.OrdinalIgnoreCase))
							folders.Remove(i);
					}
				}
				var existingExternalFolder = folders.Linq.FirstOrDefault(f =>
					string.Equals(FileUtility.GetAbsolutePath(f.Value?["path"]?.Value ?? string.Empty), externalPackagePath, StringComparison.OrdinalIgnoreCase));

				if (existingExternalFolder.Value != null)
				{
					if (!string.IsNullOrEmpty(externalFolderName))
						existingExternalFolder.Value["name"] = externalFolderName;
				}
				else if (string.IsNullOrEmpty(externalPackageName) || !folders.Linq.Any(f =>
					string.Equals(f.Value?["name"]?.Value, externalPackageName, StringComparison.OrdinalIgnoreCase)))
				{
					var externalFolder = new JSONObject();
					externalFolder["name"] = externalFolderName;
					externalFolder["path"] = externalPackagePath;
					folders.Add(externalFolder);
				}

				if (!string.IsNullOrEmpty(solutionFileName))
				{
					var settings = workspace["settings"] as JSONObject;
					if (settings == null)
					{
						settings = new JSONObject();
						workspace["settings"] = settings;
					}

					settings["dotnet.defaultSolution"] = solutionFileName;
				}

				WriteAllTextFromJObject(workspaceFile, workspace);
				return workspaceFile;
			}
			catch
			{
				return null;
			}
		}

		private static string MakeSafeFileName(string name)
		{
			if (string.IsNullOrEmpty(name))
				return string.Empty;

			var invalid = IOPath.GetInvalidFileNameChars();
			var chars = name.ToCharArray();
			for (var i = 0; i < chars.Length; i++)
			{
				if (Array.IndexOf(invalid, chars[i]) >= 0)
					chars[i] = '_';
			}

			return new string(chars).Trim();
		}

		private static string TryGetUnityPackageName(string packageRoot)
		{
			if (string.IsNullOrEmpty(packageRoot))
				return null;

			try
			{
				var packageJsonPath = IOPath.Combine(packageRoot, "package.json");
				if (!File.Exists(packageJsonPath))
					return null;

				var packageJson = JSONNode.Parse(File.ReadAllText(packageJsonPath));
				var packageName = packageJson?["name"]?.Value;
				return string.IsNullOrWhiteSpace(packageName) ? null : packageName.Trim();
			}
			catch
			{
				return null;
			}
		}

		private static ProcessStartInfo ProcessStartInfoFor(string application, string arguments)
		{
#if UNITY_EDITOR_OSX
			// wrap with built-in OSX open feature
			arguments = $"-n \"{application}\" --args {arguments}";
			application = "open";
			return ProcessRunner.ProcessStartInfoFor(application, arguments, redirect:false, shell: true);
#else
			return ProcessRunner.ProcessStartInfoFor(application, arguments, redirect: false);
#endif
		}

		public static void Initialize()
		{
		}
	}
}
