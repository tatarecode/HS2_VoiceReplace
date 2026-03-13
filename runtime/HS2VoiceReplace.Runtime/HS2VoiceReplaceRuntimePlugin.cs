using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace HS2VoiceReplace.Runtime
{
    [BepInPlugin(Guid, Name, Version)]
    public sealed class HS2VoiceReplaceRuntimePlugin : BaseUnityPlugin
    {
        public const string Guid = "com.hs2voicereplace.runtime";
        public const string Name = "HS2 Voice Replace Runtime";
        public const string Version = "1.0.0";

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<int> _targetPersonalityId;
        private ConfigEntry<string> _packName;
        private ConfigEntry<bool> _verboseLog;

        private void Awake()
        {
            _enabled = Config.Bind("General", "Enabled", true, "Enable this plugin.");
            _targetPersonalityId = Config.Bind("General", "TargetPersonalityId", 13, "Personality ID to replace (existing ID only).");
            _packName = Config.Bind("General", "PackName", "VoiceReplacePack", "Display name for the voice replacement pack.");
            _verboseLog = Config.Bind("General", "VerboseLog", false, "Enable detailed startup logs.");

            if (!_enabled.Value)
            {
                Logger.LogInfo($"{Name} {Version} disabled by config.");
                return;
            }

            Logger.LogInfo($"{Name} {Version} loaded. target={_targetPersonalityId.Value}, pack={_packName.Value}");
            Logger.LogInfo("Mode: zipmod-driven voice replacement for existing personality IDs.");

            if (_verboseLog.Value)
            {
                Logger.LogInfo("No runtime personality injection is active in this version.");
                Logger.LogInfo("Use a zipmod that overrides abdata/sound/data/pcm/cXX assets for the target personality.");
            }
        }
    }
}


