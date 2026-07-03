using System;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SceneTalkVR.Core
{
    #if UNITY_EDITOR
    [InitializeOnLoad]
    #endif
    public static class DotEnvLoader
    {
        #if UNITY_EDITOR
        static DotEnvLoader()
        {
            LoadEnv();
        }
        #endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void LoadEnv()
        {
            // Path 1: Unity project root (Client/.env)
            string rootPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string envPath = Path.Combine(rootPath, ".env");

            if (!File.Exists(envPath))
            {
                // Path 2: Repository root (SceneTalkVR/.env)
                rootPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
                envPath = Path.Combine(rootPath, ".env");
            }

            if (File.Exists(envPath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(envPath);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                            continue;

                        int idx = line.IndexOf('=');
                        if (idx > 0)
                        {
                            string key = line.Substring(0, idx).Trim();
                            string val = line.Substring(idx + 1).Trim();
                            
                            // Remove wrapping quotes if present
                            if ((val.StartsWith("\"") && val.EndsWith("\"")) || (val.StartsWith("'") && val.EndsWith("'")))
                            {
                                val = val.Substring(1, val.Length - 2);
                            }

                            Environment.SetEnvironmentVariable(key, val);
                        }
                    }
                    Debug.Log($"[DotEnvLoader] Loaded environment variables successfully from: {envPath}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DotEnvLoader] Failed to load .env file: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning("[DotEnvLoader] .env file not found. Bypassing automatic environment registration.");
            }
        }
    }
}
