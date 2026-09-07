// Observational runner. Production import/export and UI are deliberately unmodified.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GameRuleEditor.Controllers;
using GameRuleEditor.Core;
using GameRuleEditor.CustomControls;
using GameRuleEditor.Panels;
using GameRuleEditor.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace GameRuleEvaluation
{
    public static class RobustnessScaleEvaluation
    {
        const string Root = "Evaluation/RobustnessScale";
        const double LayoutTimeout = 15;
        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        static EditorContext context;
        static ProjectController controller;
        static GameRuleHierarchyWindow hierarchy;
        static GameRuleRulesWindow rules;
        static JObject row;
        static JArray logs;
        static string runDir, stage, input, kind;
        static HashSet<string> existingAssets;
        static readonly HashSet<string> createdAssets = new HashSet<string>();
        static double importBoundary;
        static bool timing, exitWhenDone, abort;
        static Stopwatch clock;
        static string stageException;
        static bool sampleSaved;

        [MenuItem("GameRule/Evaluation/Run robustness and scale")]
        public static void RunFromMenu() { Start(false); }

        // Launch with graphics enabled. Do not supply -quit: update callbacks drive the suite.
        public static void RunCommandLine() { Start(true); }

        static void Start(bool exit)
        {
            if (stack.Count != 0) throw new InvalidOperationException("Evaluation already running");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Evaluation requires Edit mode");
            if (!File.Exists(Root + "/inputs.json"))
                throw new FileNotFoundException("Run scale/generator/generate.py first");
            exitWhenDone = exit;
            abort = false;
            string id = Environment.GetEnvironmentVariable("GR_EVAL_RUN_ID") ??
                DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            if (id.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
                throw new ArgumentException("Unsafe run id");
            runDir = Root + "/runs/" + id;
            Directory.CreateDirectory(runDir);
            if (File.Exists(runDir + "/environment.json")) throw new IOException("Run already exists");
            Write(runDir + "/environment.json", EnvironmentRecord());
            File.Copy(Root + "/inputs.json", runDir + "/inputs.json", false);
            EditorApplication.update += Tick;
            stack.Push(Run());
        }

        static JObject EnvironmentRecord()
        {
            var packages = JObject.Parse(File.ReadAllText("Packages/packages-lock.json"))["dependencies"];
            return new JObject {
                ["utc"] = DateTime.UtcNow.ToString("O"), ["os"] = SystemInfo.operatingSystem,
                ["cpu"] = SystemInfo.processorType, ["logical_processors"] = SystemInfo.processorCount,
                ["ram_mb"] = SystemInfo.systemMemorySize, ["gpu"] = SystemInfo.graphicsDeviceName,
                ["gpu_driver"] = SystemInfo.graphicsDeviceVersion, ["unity"] = Application.unityVersion,
                ["packages"] = packages, ["git_commit"] = Git("rev-parse HEAD"),
                ["git_branch"] = Git("branch --show-current"),
                ["git_dirty"] = !string.IsNullOrWhiteSpace(Git("status --porcelain --untracked-files=no")),
                ["production_sha256"] = new JObject(Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories)
                    .Where(p => !p.Replace('\\', '/').Contains("/Evaluation/"))
                    .OrderBy(p => p, StringComparer.Ordinal)
                    .Select(p => new JProperty(p.Replace('\\', '/'), Hash(File.ReadAllText(p))))),
                ["mode"] = "Edit mode", ["batch_mode"] = Application.isBatchMode,
                ["graphics"] = SystemInfo.graphicsDeviceType.ToString(),
                ["build_target"] = EditorUserBuildSettings.activeBuildTarget.ToString(),
                ["development_build_setting"] = EditorUserBuildSettings.development,
                ["profiler_enabled"] = UnityEngine.Profiling.Profiler.enabled,
                ["stopwatch_frequency"] = Stopwatch.Frequency, ["high_resolution"] = Stopwatch.IsHighResolution,
                ["culture"] = CultureInfo.CurrentCulture.Name,
                ["pixels_per_point"] = EditorGUIUtility.pixelsPerPoint,
                ["layout_timeout_seconds"] = LayoutTimeout, ["sample_timeout_seconds"] = 600,
                ["window_rects"] = "Hierarchy 360x720; Rules 1000x720 points",
                ["cache_policy"] = "One warmup per configuration; fresh context/windows/assets each sample; OS/Unity caches retained; no forced GC",
                ["render_completion"] = "Attached panel, finite nonzero geometry, scheduled callback, >=3 editor updates with stable geometry; not GPU present completion",
                ["scene_generation"] = "not invoked; separate scene-changing pipeline"
            };
        }

        static string Git(string args)
        {
            try {
                using (var p = Process.Start(new ProcessStartInfo("git", args) {
                    UseShellExecute = false, RedirectStandardOutput = true,
                    RedirectStandardError = true, CreateNoWindow = true })) {
                    string s = p.StandardOutput.ReadToEnd();
                    return p.WaitForExit(5000) && p.ExitCode == 0 ? s.Trim() : "unavailable";
                }
            } catch { return "unavailable"; }
        }

        static void Tick()
        {
            try {
                if (stack.Count == 0) { Finish(); return; }
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); return; }
                if (top.Current is IEnumerator child) stack.Push(child);
            } catch (Exception ex) {
                // Harness catches exceptions escaping production, preserves the exact message and continues.
                if (row != null && !sampleSaved) {
                    row["status"] = ex is TimeoutException ? "timeout" : "harness_or_setup_error";
                    row["failure_stage"] = stage;
                    row["failure"] = Safe(ex.ToString());
                    SaveRow();
                }
                Cleanup();
                if (stack.Count > 1) {
                    while (stack.Count > 1) stack.Pop();
                    return; // Retain this timeout/error and continue the next independent sample.
                }
                stack.Clear();
                Write(runDir + "/aborted.json", new JObject { ["stage"] = stage, ["exception"] = Safe(ex.ToString()) });
                abort = true;
                Finish();
            }
        }

        static IEnumerator Run()
        {
            var manifest = JObject.Parse(File.ReadAllText(Root + "/inputs.json"));
            // Baseline is a gate. Invalid-input observations are meaningless if it cannot load fully.
            yield return Sample("baseline", Root + "/robustness/baseline/baseline.json", -1, null);
            if ((string)row["status"] != "completed" || !(bool)row["correctness"]["all"])
                throw new InvalidOperationException("Baseline gate failed; remaining observations pending");
            foreach (JObject spec in manifest["cases"])
                yield return Sample("robustness", Root + "/" + (string)spec["file"], 1, spec);
            foreach (JObject spec in manifest["scale"])
                for (int repetition = 0; repetition <= 5; repetition++)
                    yield return Sample("scale", Root + "/" + (string)spec["file"], repetition, spec);
            Write(runDir + "/completed.json", new JObject { ["utc"] = DateTime.UtcNow.ToString("O") });
        }

        static IEnumerator Sample(string category, string path, int repetition, JObject spec)
        {
            kind = category; input = path; stageException = null; sampleSaved = false;
            row = new JObject {
                ["case_id"] = spec?["case_id"] ?? "baseline", ["kind"] = kind,
                ["repetition"] = repetition, ["warmup"] = kind == "scale" && repetition == 0,
                ["input"] = path, ["input_sha256"] = Hash(File.ReadAllText(path)),
                ["utc"] = DateTime.UtcNow.ToString("O"), ["status"] = "running",
                ["manual_panel_check"] = "pending", ["spec"] = spec?.DeepClone()
            };
            logs = new JArray();
            SetStage("setup");
            Application.logMessageReceived += CaptureLog;
            Setup();
            JObject before = null;
            GameRuleProject prior = null;
            if (kind == "robustness") {
                SetStage("baseline setup");
                controller.ImportJsonAsProject(Root + "/robustness/baseline/baseline.json");
                context.SelectActor(0);
                rules = NewRules();
                yield return Settle(hierarchy.rootVisualElement);
                yield return Settle(rules.rootVisualElement);
                before = Snapshot(context.currentProject);
                prior = context.currentProject;
                row["before"] = before;
                logs.Clear();
            }
            clock = Stopwatch.StartNew();
            importBoundary = -1;
            timing = true;
            SetStage("import: read, deserialize, defaults, asset persistence");
            Exception escaped = Attempt(() => controller.ImportJsonAsProject(input));
            timing = false;
            double importCall = clock.Elapsed.TotalMilliseconds;
            row["import_call_ms"] = importCall;
            row["read_process_persist_ms"] = importBoundary < 0 ? JValue.CreateNull() : new JValue(importBoundary);
            row["hierarchy_sync_ms"] = importBoundary < 0 ? JValue.CreateNull() : new JValue(importCall - importBoundary);
            if (escaped == null && context.currentProject != null) {
                SetStage("visual reconstruction and layout");
                yield return Settle(hierarchy.rootVisualElement);
                row["hierarchy_visual_order"] = new JArray(HierarchyNames());
                var visual = new JArray();
                // Production is lazy per actor. Visit every actor serially through real selection and Rules windows.
                for (int i = 0; i < context.currentProject.actors.Count; i++) {
                    if (clock.Elapsed.TotalSeconds > 600) throw new TimeoutException("Sample exceeded 600 seconds");
                    int actorIndex = i;
                    Exception uiError = Attempt(() => {
                        context.SelectActor(actorIndex);
                        if (rules == null) rules = NewRules();
                    });
                    if (uiError != null) { escaped = uiError; break; }
                    yield return Settle(rules.rootVisualElement);
                    var panel = rules.rootVisualElement.Q<ScriptEditorPanel>();
                    var folds = panel.Query<Foldout>().ToList();
                    var actions = panel.Query<ActionElement>().ToList().Select(a => a.GetActionString()).ToArray();
                    visual.Add(new JObject {
                        ["actor"] = context.SelectedActor.ActorName,
                        ["selected_index"] = context.selectedActorIndex,
                        ["rules"] = new JArray(folds.Select(RuleTitle)),
                        ["actions"] = new JArray(actions),
                        ["conditions"] = new JArray(panel.Query<ConditionElement>().ToList().Select(c => c.GetString())),
                        ["attached"] = panel.panel != null
                    });
                }
                row["visual"] = visual;
            }
            row["visual_rebuild_ms"] = importBoundary < 0 ? JValue.CreateNull() : new JValue(clock.Elapsed.TotalMilliseconds - importBoundary);
            string importFailureStage = escaped == null ? null : Classify(escaped);
            SetStage("export");
            string exportPath = runDir + "/" + RowName() + "-export.json";
            var exportClock = Stopwatch.StartNew();
            Exception exportError = Attempt(() => controller.SaveProjectToJson(exportPath));
            exportClock.Stop();
            row["export_ms"] = exportClock.Elapsed.TotalMilliseconds;
            clock.Stop();
            row["total_ms"] = clock.Elapsed.TotalMilliseconds;
            row["import_exception"] = escaped == null ? JValue.CreateNull() : new JValue(Safe(escaped.ToString()));
            row["export_exception"] = exportError == null ? JValue.CreateNull() : new JValue(Safe(exportError.ToString()));
            row["export_file"] = File.Exists(exportPath) ? Path.GetFileName(exportPath) : null;
            SetStage("observation and explicit post-import Validate");
            row["after"] = Snapshot(context.currentProject);
            var validation = context.currentProject == null ? new List<string>() : context.currentProject.Validate();
            row["explicit_validation_messages"] = new JArray(validation);
            row["correctness"] = CheckModelAndRoundTrip(exportPath);
            if (kind == "robustness") {
                bool replaced = prior != context.currentProject;
                bool same = Canonical(before) == Canonical(row["after"]);
                bool uiMismatch = row["visual"] != null && !VisualMatches();
                var importLogs = logs.OfType<JObject>().Where(l => ((string)l["stage"]).StartsWith("import") ||
                    ((string)l["stage"]).StartsWith("visual")).Where(l => (string)l["type"] != "Log").ToList();
                bool detected = escaped != null || importLogs.Count > 0;
                row["detected"] = detected;
                row["detection_stage"] = escaped != null ? importFailureStage : importLogs.FirstOrDefault()?["stage"] ?? "not detected during import/reconstruction";
                row["message"] = escaped != null ? Safe(escaped.Message) : string.Join("\n", importLogs.Select(l => (string)l["message"]));
                row["user_visible_diagnostic"] = importLogs.Count > 0 ? "Console log" : escaped != null ? "exception escapes UI caller; harness captured it, natural Console presentation not exercised" : "none";
                row["import_rejected"] = !replaced;
                row["accepted_silently"] = replaced && !detected;
                row["acceptance"] = !replaced ? "not installed" : escaped != null || uiMismatch ? "installed; visual failure or discrepancy" : "installed and visually reconstructed";
                row["unhandled_exception"] = escaped != null;
                row["previous_state_preserved"] = !replaced && same;
                row["previous_asset_content_preserved"] = Canonical(before) == Canonical(Snapshot(prior));
                row["partial_or_inconsistent_state"] = uiMismatch || (replaced && escaped != null) || !(bool)row["previous_asset_content_preserved"];
                row["notes"] = "Import detection excludes explicit Validate, oracle discrepancies and export diagnostics. Previous state means the active project AND its content; original asset checked separately. Runtime/generation not executed.";
            }
            if (kind == "scale" && (int)spec["actors"] == 1 && (int)spec["rules_per_actor"] == 100 && rules != null)
                yield return LongPanel();
            row["status"] = escaped != null || exportError != null ? "production_exception" : "completed";
            SaveRow();
            Cleanup();
            yield return null;
        }

        static void Setup()
        {
            existingAssets = new HashSet<string>(AssetDatabase.FindAssets("t:GameRuleProject"));
            context = ScriptableObject.CreateInstance<EditorContext>();
            context.hideFlags = HideFlags.DontSave;
            controller = new ProjectController(context);
            // First subscriber timestamps the actual data/UI boundary without instrumenting production.
            context.OnProjectLoaded += () => {
                string assetPath = AssetDatabase.GetAssetPath(context.currentProject);
                if (!string.IsNullOrEmpty(assetPath) && !existingAssets.Contains(AssetDatabase.AssetPathToGUID(assetPath)))
                    createdAssets.Add(assetPath);
                if (timing) { importBoundary = clock.Elapsed.TotalMilliseconds; SetStage("visual reconstruction: OnProjectLoaded"); }
            };
            hierarchy = ScriptableObject.CreateInstance<GameRuleHierarchyWindow>();
            hierarchy.Init(context, controller);
            hierarchy.position = new Rect(30, 50, 360, 720);
            hierarchy.Show();
        }

        static GameRuleRulesWindow NewRules()
        {
            var w = ScriptableObject.CreateInstance<GameRuleRulesWindow>();
            w.Init(context, controller);
            w.position = new Rect(400, 50, 1000, 720);
            w.Show();
            return w;
        }

        static IEnumerator Settle(VisualElement root)
        {
            var watch = Stopwatch.StartNew();
            bool scheduled = false;
            root.schedule.Execute(() => scheduled = true);
            Rect previous = default;
            int stable = 0;
            while (stable < 3) {
                hierarchy?.Repaint(); rules?.Repaint();
                yield return null;
                Rect current = root.worldBound;
                var scroll = root.Q<ScrollView>();
                if (scroll != null) current.height += scroll.contentContainer.worldBound.height;
                bool valid = root.panel != null && current.width > 0 && current.height > 0 &&
                    !float.IsNaN(current.height) && !float.IsInfinity(current.height);
                stable = scheduled && valid && current == previous ? stable + 1 : 0;
                previous = current;
                if (watch.Elapsed.TotalSeconds > LayoutTimeout)
                    throw new TimeoutException("UI Toolkit layout/scheduled callback did not settle within 15 seconds");
            }
        }

        static IEnumerator LongPanel()
        {
            SetStage("long panel automation (outside timing)");
            var panel = rules.rootVisualElement.Q<ScriptEditorPanel>();
            var folds = panel.Query<Foldout>().ToList();
            var scroll = panel.Q<ScrollView>();
            if (folds.Count != 100) {
                row["long_panel"] = new JObject { ["rule_count"] = folds.Count, ["last_visible"] = false };
                yield break;
            }
            var last = folds.Last();
            bool changed = false;
            last.RegisterValueChangedCallback(e => changed = true);
            last.value = true;
            yield return Settle(rules.rootVisualElement);
            scroll.ScrollTo(last);
            yield return Settle(rules.rootVisualElement);
            row["long_panel"] = new JObject {
                ["actor_selected"] = context.selectedActorIndex == 0,
                ["rule_count"] = folds.Count, ["last_rule"] = RuleTitle(last),
                ["foldout_callback"] = changed, ["enabled"] = last.enabledInHierarchy,
                ["last_visible"] = scroll.contentViewport.worldBound.Overlaps(last.worldBound),
                ["scroll_y"] = scroll.scrollOffset.y,
                ["manual_pointer_keyboard_visual_quality"] = "pending; programmatic callbacks do not prove human interaction"
            };
        }

        static string[] HierarchyNames()
        {
            // Read-only reflection over the real hierarchy preserves exact visible order.
            var field = typeof(GameRuleHierarchyWindow).GetField("actorItems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return ((List<VisualElement>)field.GetValue(hierarchy)).Select(v => v.Q<Label>()?.text).ToArray();
        }

        // Production clears Foldout.toggle.text and inserts the editable rule title into that toggle.
        static string RuleTitle(Foldout f) => f.Q<Toggle>()?.Q<TextField>()?.value;

        static bool VisualMatches()
        {
            if (row["visual"] is not JArray visual || context.currentProject == null) return false;
            if (visual.Count != context.currentProject.actors.Count) return false;
            for (int i = 0; i < visual.Count; i++) {
                var actor = context.currentProject.actors[i];
                if ((string)visual[i]["actor"] != actor.ActorName) return false;
                if (!visual[i]["rules"].Values<string>().SequenceEqual(actor.Script.Select(r => r.Name))) return false;
                if (!visual[i]["actions"].Values<string>().Select(Compact).SequenceEqual(actor.Script.SelectMany(r => r.Do).Select(Compact))) return false;
                if (!visual[i]["conditions"].Values<string>().Select(Compact).SequenceEqual(actor.Script.SelectMany(r => r.When).Select(Compact))) return false;
            }
            return true;
        }

        static JObject CheckModelAndRoundTrip(string exported)
        {
            var result = new JObject { ["all"] = false, ["exportable"] = File.Exists(exported) };
            if (context.currentProject == null) return result;
            var p = context.currentProject;
            result["actor_count"] = p.actors.Count;
            result["total_rules"] = p.actors.Sum(a => a.Script?.Count ?? 0);
            result["rules_per_actor"] = new JArray(p.actors.Select(a => a.Script?.Count ?? 0));
            result["actor_order"] = new JArray(p.actors.Select(a => a.ActorName));
            result["unique_actor_identifiers"] = p.actors.Select(a => a.ActorName).Distinct().Count() == p.actors.Count;
            result["unique_rule_names_per_actor"] = p.actors.All(a => a.Script.Select(r => r.Name).Distinct().Count() == a.Script.Count);
            result["visual_matches_model"] = VisualMatches();
            result["hierarchy_matches_model"] = HierarchyNames().SequenceEqual(p.actors.Select(a => a.ActorName));
            try {
                var source = JObject.Parse(File.ReadAllText(input));
                var actual = JObject.Parse(File.ReadAllText(exported));
                result["input_content_preserved"] = Contains(actual, source);
                var again = GameRuleProject.ImportFromJson(exported);
                try { result["canonical_round_trip"] = Canonical(actual) == Canonical(JObject.Parse(again.ExportToJson())); }
                finally { Object.DestroyImmediate(again); }
            } catch (Exception ex) { result["check_exception"] = Safe(ex.ToString()); }
            result["all"] = new[] { "exportable", "unique_actor_identifiers", "unique_rule_names_per_actor", "visual_matches_model", "hierarchy_matches_model", "input_content_preserved", "canonical_round_trip" }
                .All(k => result[k]?.Type == JTokenType.Boolean && (bool)result[k]);
            return result;
        }

        // Order-sensitive arrays; input keys must survive, exporter-added defaults are permitted.
        static bool Contains(JToken actual, JToken expected)
        {
            if (actual == null) return expected is JArray ar && ar.Count == 0;
            if (expected is JObject obj) return obj.Properties().All(p => Contains(actual[p.Name], p.Value));
            if (expected is JArray array) return actual is JArray aa && aa.Count == array.Count && aa.Zip(array, Contains).All(v => v);
            return Canonical(actual) == Canonical(expected);
        }

        static JObject Snapshot(GameRuleProject p)
        {
            if (p == null) return new JObject { ["project"] = JValue.CreateNull() };
            var snapshot = new JObject {
                ["project_name"] = p.projectName,
                ["actors_before_export"] = new JArray(p.actors.Select(a => JObject.Parse(JsonUtility.ToJson(a)))),
                ["actor_order"] = new JArray(p.actors.Select(a => a.ActorName)),
                ["rules_per_actor"] = new JArray(p.actors.Select(a => a.Script?.Count ?? 0))
            };
            try { snapshot["export"] = JObject.Parse(p.ExportToJson()); }
            catch (Exception ex) { snapshot["export_exception"] = Safe(ex.ToString()); }
            snapshot["actors_after_export"] = new JArray(p.actors.Select(a => JObject.Parse(JsonUtility.ToJson(a))));
            return snapshot;
        }

        static string Canonical(JToken token)
        {
            if (token == null) return "null";
            if (token is JObject o) return "{" + string.Join(",", o.Properties().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => JsonConvert.SerializeObject(p.Name) + ":" + Canonical(p.Value))) + "}";
            if (token is JArray a) return "[" + string.Join(",", a.Select(Canonical)) + "]";
            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
                return Convert.ToDouble(((JValue)token).Value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture);
            return token.ToString(Formatting.None);
        }

        static string Compact(string s) => string.Concat((s ?? "").Where(c => !char.IsWhiteSpace(c)));
        static Exception Attempt(System.Action action)
        {
            try { action(); return null; }
            catch (Exception ex) { stageException = stage; return ex; }
        }
        static string Classify(Exception ex) => ex.StackTrace != null && ex.StackTrace.Contains("FromJson") ?
            "JSON parsing/deserialization (single JsonUtility call)" : stageException ?? "unknown";
        static void CaptureLog(string message, string trace, LogType type)
        {
            logs.Add(new JObject { ["stage"] = stage, ["type"] = type.ToString(), ["message"] = Safe(message), ["stack"] = Safe(trace) });
        }
        static string Safe(string s)
        {
            if (s == null) return null;
            return s.Replace(Directory.GetCurrentDirectory(), "<PROJECT>").Replace(Directory.GetCurrentDirectory().Replace('\\', '/'), "<PROJECT>")
                .Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "<USER>");
        }
        static string Hash(string s)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-", "").ToLowerInvariant();
        }
        static string RowName() => (string)row["kind"] + "-" + (string)row["case_id"] + "-" + row["repetition"];
        static void SetStage(string s)
        {
            stage = s;
            Write(runDir + "/progress.json", new JObject { ["utc"] = DateTime.UtcNow.ToString("O"), ["stage"] = s, ["sample"] = row?.DeepClone() });
        }
        static void SaveRow()
        {
            row["logs"] = logs.DeepClone();
            row["finished_utc"] = DateTime.UtcNow.ToString("O");
            string dir = Root + "/" + (kind == "scale" ? "scale" : "robustness") + "/raw-results/" + Path.GetFileName(runDir);
            Directory.CreateDirectory(dir);
            string path = dir + "/" + RowName() + ".json";
            if (File.Exists(path)) throw new IOException("Refusing to overwrite raw result");
            Write(path, row);
            sampleSaved = true;
        }
        static void Write(string path, JToken value) => File.WriteAllText(path, value.ToString(Formatting.Indented) + "\n", new UTF8Encoding(false));

        static void Cleanup()
        {
            timing = false;
            Application.logMessageReceived -= CaptureLog;
            if (rules != null) { rules.Close(); rules = null; }
            if (hierarchy != null) { hierarchy.Close(); hierarchy = null; }
            if (context != null) { context.Clear(); Object.DestroyImmediate(context); context = null; }
            // Only assets installed in this isolated context by production import, never unrelated new assets.
            foreach (var path in createdAssets) AssetDatabase.DeleteAsset(path);
            createdAssets.Clear();
            existingAssets = null;
        }
        static void Finish()
        {
            EditorApplication.update -= Tick;
            Cleanup();
            if (exitWhenDone) EditorApplication.Exit(abort ? 2 : 0);
            else UnityEngine.Debug.Log("GameRule evaluation artifacts: " + runDir);
        }
    }
}
