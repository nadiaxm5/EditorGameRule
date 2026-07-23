using System.Collections.Generic;

namespace GameRuleEditor.Core
{
    /// <summary>
    /// Value assumed for an action parameter the user leaves empty, by position.
    /// A null entry means the parameter has no sensible default (names, prefabs, properties):
    /// leaving it empty is an incomplete rule, not an omitted zero.
    ///
    /// Single source of truth: the editor fields are initialized from here, and script
    /// generation falls back to the same values for rules saved before a default existed
    /// or for hand-edited JSON.
    /// </summary>
    public static class ActionDefaults
    {
        private static readonly Dictionary<string, string[]> byAction = new Dictionary<string, string[]>
        {
            // Amounts: 0 means "none of it".
            { "Move",       new[] { "0", "0", "0", "0" } },
            { "Rotate",     new[] { "0", "0", "0", "0" } },
            { "Torque",     new[] { "0", "0", "0" } },
            { "Push",       new[] { "0", "0", "0", "0" } },

            // Prefab and spawner must be named; the six offsets are relative, so 0 = at the spawner.
            { "Spawn",      new[] { null, null, "0", "0", "0", "0", "0", "0" } },

            // Destinations: falling back to the actor's own coordinate leaves that axis untouched.
            { "MoveTo",     new[] { "0", "this.x", "this.y", "this.z" } },
            { "NavigateTo", new[] { "0", "this.x", "this.y", "this.z" } },
            { "PushTo",     new[] { "0", "this.x", "this.y", "this.z" } },
            { "RotateTo",   new[] { "0", "this.x", "this.y", "this.z", "this.x", "this.y", "this.z" } },
        };

        /// <summary>Default for one parameter, or null when it has none.</summary>
        public static string Get(string actionName, int parameterIndex)
        {
            if (string.IsNullOrEmpty(actionName)) return null;
            if (!byAction.TryGetValue(actionName, out string[] defaults)) return null;
            if (parameterIndex < 0 || parameterIndex >= defaults.Length) return null;
            return defaults[parameterIndex];
        }

        /// <summary>How many parameters the action takes, or 0 when it has no defaults registered.</summary>
        public static int ParameterCount(string actionName)
        {
            if (!string.IsNullOrEmpty(actionName) && byAction.TryGetValue(actionName, out string[] defaults))
                return defaults.Length;
            return 0;
        }

        /// <summary>Returns value, or the parameter's default when value is blank.</summary>
        public static string Fill(string actionName, int parameterIndex, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value;
            return Get(actionName, parameterIndex) ?? value;
        }
    }
}
