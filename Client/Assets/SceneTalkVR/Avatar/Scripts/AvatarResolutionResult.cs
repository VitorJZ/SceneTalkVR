using System;

namespace SceneTalkVR.AvatarSystem
{
    [Serializable]
    public sealed class AvatarResolutionResult
    {
        public string avatarKey;
        public string fallbackLevel;
        public string fallbackReason;
        public int score;
        public AvatarPresetEntry preset;

        public bool HasPreset => preset != null;

        public static AvatarResolutionResult Empty(string reason)
        {
            return new AvatarResolutionResult
            {
                avatarKey = string.Empty,
                fallbackLevel = "none",
                fallbackReason = reason,
                score = 0,
                preset = null
            };
        }
    }
}
