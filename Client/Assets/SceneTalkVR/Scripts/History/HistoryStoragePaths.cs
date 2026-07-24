using System.IO;
using UnityEngine;

namespace SceneTalkVR.History
{
    public static class HistoryStoragePaths
    {
        private const string RelativeRoot = "SceneTalkVR/History";
        public const string DatabaseFileName = "scenetalk_history.sqlite3";

        public static string RootPath => Path.Combine(
            Application.persistentDataPath,
            RelativeRoot.Replace('/', Path.DirectorySeparatorChar));

        public static string DatabasePath => Path.Combine(RootPath, DatabaseFileName);
    }
}
