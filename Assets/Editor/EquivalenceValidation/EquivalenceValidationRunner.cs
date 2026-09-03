using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using GameRuleEditor.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace GameRuleValidation
{
    /// <summary>
    /// Reproducible validation runner for the GameRule Studio equivalence study.
    /// It can be started from the GameRule menu or through Unity batch mode.
    /// </summary>
    public static class EquivalenceValidationRunner
    {
        private const string ValidationRevision = "c58f222091dbb642797006045ded9c226309fc3f";

        private static readonly string[] CoreConditions =
        {
            "Check", "Collision", "Compare", "Keyboard", "Timer", "Touch"
        };

        private static readonly string[] CoreActions =
        {
            "Animate", "Delete", "Edit", "Move", "MoveTo", "NavigateTo", "PlayParticles",
            "PlaySound", "Push", "PushTo", "Rotate", "RotateTo", "Spawn", "Torque"
        };

        private static readonly IntegrationPair[] IntegrationPairs =
        {
            new IntegrationPair("Tanks", "TanksGameRule.json", "TANKS.json"),
            new IntegrationPair("Survival Shooter", "SurvivalShooterGameRule.json", "SURVIVAL_SHOOTER.json"),
            new IntegrationPair("John Lemon", "JohnLemonGameRule.json", "JHON_LEMON.json")
        };

        [MenuItem("GameRule/Validation/Run equivalence suite", priority = 80)]
        public static void RunFromMenu()
        {
            ValidationReport report = RunSuite();
            EditorUtility.DisplayDialog(
                "GameRule equivalence validation",
                report.overallPass
                    ? "All checks passed. The reports are in Validation/Equivalence/Results."
                    : "At least one check failed. See Validation/Equivalence/Results for details.",
                "OK");
        }

        public static void RunFromCommandLine()
        {
            ValidationReport report = RunSuite();
            Debug.Log($"Equivalence validation completed: {(report.overallPass ? "PASS" : "FAIL")}");
            if (Application.isBatchMode)
                EditorApplication.Exit(report.overallPass ? 0 : 1);
        }

        private static ValidationReport RunSuite()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string casesDirectory = Path.Combine(projectRoot, "Validation", "Equivalence", "Cases");
            string resultsDirectory = Path.Combine(projectRoot, "Validation", "Equivalence", "Results");
            string evidenceDirectory = Path.Combine(resultsDirectory, "Evidence");
            Directory.CreateDirectory(resultsDirectory);
            if (Directory.Exists(evidenceDirectory)) Directory.Delete(evidenceDirectory, true);
            Directory.CreateDirectory(evidenceDirectory);

            var report = new ValidationReport
            {
                generatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                unityVersion = Application.unityVersion,
                expectedUnityVersion = "6000.3.1f1",
                sourceRevision = ValidationRevision
            };

            AssetDatabase.DisallowAutoRefresh();
            try
            {
                foreach (string casePath in Directory.GetFiles(casesDirectory, "E*.json").OrderBy(path => path))
                    report.controlledCases.Add(RunControlledCase(casePath, projectRoot, evidenceDirectory));

                string gamesDirectory = Path.Combine(Application.dataPath, "Resources", "Games");
                foreach (IntegrationPair pair in IntegrationPairs)
                {
                    report.integrationCases.Add(RunIntegrationPair(
                        pair.name,
                        Path.Combine(gamesDirectory, pair.manualFile),
                        Path.Combine(gamesDirectory, pair.studioFile),
                        projectRoot,
                        evidenceDirectory));
                }

                report.coverage = EvaluateCoverage(
                    Directory.GetFiles(casesDirectory, "E*.json").OrderBy(path => path));

                report.runtimeChecks.Add(RunRuntimeCheck("Actor declaration order", ValidateActorOrder));
                report.runtimeChecks.Add(RunRuntimeCheck("Multiple writes and last-write-wins", ValidateMultipleWrites));
                report.runtimeChecks.Add(RunRuntimeCheck("Deferred Spawn and Delete scheduling", ValidateDeferredLifecycle));
            }
            catch (Exception exception)
            {
                report.runnerErrors.Add(exception.ToString());
            }
            finally
            {
                ActorScheduler.Build(Array.Empty<string>());
                AssetDatabase.AllowAutoRefresh();
            }

            report.overallPass =
                report.runnerErrors.Count == 0 &&
                report.controlledCases.Count == 8 &&
                report.controlledCases.All(result => result.AllPassed) &&
                report.integrationCases.Count == 3 &&
                report.integrationCases.All(result => result.AllPassed) &&
                report.runtimeChecks.Count == 3 &&
                report.runtimeChecks.All(result => result.pass) &&
                report.coverage != null && report.coverage.pass &&
                report.unityVersion == report.expectedUnityVersion;

            report.evidence = WriteEvidenceManifest(evidenceDirectory, report);

            File.WriteAllText(
                Path.Combine(resultsDirectory, "equivalence-report.json"),
                NormalizeLineEndings(JsonConvert.SerializeObject(report, Formatting.Indented)),
                new UTF8Encoding(false));

            File.WriteAllText(
                Path.Combine(resultsDirectory, "equivalence-report.md"),
                NormalizeLineEndings(BuildMarkdownReport(report)),
                new UTF8Encoding(false));

            return report;
        }

        private static DescriptorResult RunControlledCase(
            string sourcePath,
            string projectRoot,
            string evidenceRoot)
        {
            string caseName = Path.GetFileNameWithoutExtension(sourcePath);
            var result = new DescriptorResult { name = caseName };
            string evidenceDirectory = Path.Combine(evidenceRoot, "controlled", caseName);
            GameRuleProject sourceProject = null;
            GameRuleProject reconstructedProject = null;

            try
            {
                string sourceJson = File.ReadAllText(sourcePath);
                sourceProject = GameRuleProject.ImportFromJson(sourcePath);
                RequireProject(sourceProject, sourcePath);

                string firstExport = sourceProject.ExportToJson();
                reconstructedProject = ImportTemporary(firstExport);
                RequireProject(reconstructedProject, caseName + " first export");
                string secondExport = reconstructedProject.ExportToJson();

                string canonicalSource = CanonicalizeJson(sourceJson);
                string canonicalFirstExport = CanonicalizeJson(firstExport);
                string canonicalSecondExport = CanonicalizeJson(secondExport);
                string sourceAst = BuildParsedAst(sourceProject);
                string reconstructedAst = BuildParsedAst(reconstructedProject);
                Dictionary<string, string> sourceCSharp = GenerateSourceSnapshot(sourceProject, projectRoot);
                Dictionary<string, string> reconstructedCSharp = GenerateSourceSnapshot(reconstructedProject, projectRoot);

                result.canonicalJson = canonicalSource == canonicalFirstExport;
                result.parsedAst = sourceAst == reconstructedAst;
                result.generatedCSharp = GeneratedSourcesEqual(sourceCSharp, reconstructedCSharp);
                result.roundTrip =
                    canonicalFirstExport == canonicalSecondExport &&
                    SameOrderedTopology(sourceProject, reconstructedProject);
                result.sourceCanonicalSha256 = Sha256(canonicalSource);
                result.outputCanonicalSha256 = Sha256(canonicalFirstExport);
                result.sourceParsedAstSha256 = Sha256(sourceAst);
                result.outputParsedAstSha256 = Sha256(reconstructedAst);
                result.sourceGeneratedCSharpSha256 = SourceSnapshotSha256(sourceCSharp);
                result.outputGeneratedCSharpSha256 = SourceSnapshotSha256(reconstructedCSharp);

                WriteEvidenceText(evidenceDirectory, "canonical-input.json", canonicalSource);
                WriteEvidenceText(evidenceDirectory, "canonical-studio-export.json", canonicalFirstExport);
                WriteEvidenceText(evidenceDirectory, "canonical-round-trip.json", canonicalSecondExport);
                WriteEvidenceText(evidenceDirectory, "parsed-input.ast.txt", sourceAst);
                WriteEvidenceText(evidenceDirectory, "parsed-studio-export.ast.txt", reconstructedAst);
                WriteSourceSnapshot(evidenceDirectory, "csharp-input", sourceCSharp);
                WriteSourceSnapshot(evidenceDirectory, "csharp-studio-export", reconstructedCSharp);
            }
            catch (Exception exception)
            {
                result.errors.Add(exception.ToString());
            }
            finally
            {
                if (sourceProject != null) UnityEngine.Object.DestroyImmediate(sourceProject);
                if (reconstructedProject != null) UnityEngine.Object.DestroyImmediate(reconstructedProject);
            }

            return result;
        }

        private static DescriptorResult RunIntegrationPair(
            string name,
            string manualPath,
            string studioPath,
            string projectRoot,
            string evidenceRoot)
        {
            var result = new DescriptorResult { name = name };
            string evidenceDirectory = Path.Combine(evidenceRoot, "integration", SafePathSegment(name));
            GameRuleProject manualProject = null;
            GameRuleProject studioProject = null;
            GameRuleProject manualRoundTrip = null;
            GameRuleProject studioRoundTrip = null;

            try
            {
                string manualJson = File.ReadAllText(manualPath);
                string studioJson = File.ReadAllText(studioPath);
                manualProject = GameRuleProject.ImportFromJson(manualPath);
                studioProject = GameRuleProject.ImportFromJson(studioPath);
                RequireProject(manualProject, manualPath);
                RequireProject(studioProject, studioPath);

                string canonicalManual = CanonicalizeJson(manualJson);
                string canonicalStudio = CanonicalizeJson(studioJson);
                string manualAst = BuildParsedAst(manualProject);
                string studioAst = BuildParsedAst(studioProject);
                Dictionary<string, string> manualCSharp = GenerateSourceSnapshot(manualProject, projectRoot);
                Dictionary<string, string> studioCSharp = GenerateSourceSnapshot(studioProject, projectRoot);

                result.canonicalJson = canonicalManual == canonicalStudio;
                result.parsedAst = manualAst == studioAst;
                result.generatedCSharp = GeneratedSourcesEqual(manualCSharp, studioCSharp);

                string manualExport = manualProject.ExportToJson();
                string studioExport = studioProject.ExportToJson();
                manualRoundTrip = ImportTemporary(manualExport);
                studioRoundTrip = ImportTemporary(studioExport);
                RequireProject(manualRoundTrip, name + " manual round-trip");
                RequireProject(studioRoundTrip, name + " Studio round-trip");

                string canonicalManualExport = CanonicalizeJson(manualExport);
                string canonicalStudioExport = CanonicalizeJson(studioExport);
                string canonicalManualRoundTrip = CanonicalizeJson(manualRoundTrip.ExportToJson());
                string canonicalStudioRoundTrip = CanonicalizeJson(studioRoundTrip.ExportToJson());

                result.roundTrip =
                    canonicalManualExport == canonicalStudioExport &&
                    canonicalManualExport == canonicalManualRoundTrip &&
                    canonicalStudioExport == canonicalStudioRoundTrip &&
                    SameOrderedTopology(manualProject, studioProject) &&
                    SameOrderedTopology(manualProject, manualRoundTrip) &&
                    SameOrderedTopology(studioProject, studioRoundTrip);

                result.sourceCanonicalSha256 = Sha256(canonicalManual);
                result.outputCanonicalSha256 = Sha256(canonicalStudio);
                result.sourceParsedAstSha256 = Sha256(manualAst);
                result.outputParsedAstSha256 = Sha256(studioAst);
                result.sourceGeneratedCSharpSha256 = SourceSnapshotSha256(manualCSharp);
                result.outputGeneratedCSharpSha256 = SourceSnapshotSha256(studioCSharp);

                WriteEvidenceText(evidenceDirectory, "canonical-manual.json", canonicalManual);
                WriteEvidenceText(evidenceDirectory, "canonical-studio.json", canonicalStudio);
                WriteEvidenceText(evidenceDirectory, "canonical-manual-first-export.json", canonicalManualExport);
                WriteEvidenceText(evidenceDirectory, "canonical-studio-first-export.json", canonicalStudioExport);
                WriteEvidenceText(evidenceDirectory, "canonical-manual-second-export.json", canonicalManualRoundTrip);
                WriteEvidenceText(evidenceDirectory, "canonical-studio-second-export.json", canonicalStudioRoundTrip);
                WriteEvidenceText(evidenceDirectory, "parsed-manual.ast.txt", manualAst);
                WriteEvidenceText(evidenceDirectory, "parsed-studio.ast.txt", studioAst);
                WriteSourceSnapshot(evidenceDirectory, "csharp-manual", manualCSharp);
                WriteSourceSnapshot(evidenceDirectory, "csharp-studio", studioCSharp);
            }
            catch (Exception exception)
            {
                result.errors.Add(exception.ToString());
            }
            finally
            {
                if (manualProject != null) UnityEngine.Object.DestroyImmediate(manualProject);
                if (studioProject != null) UnityEngine.Object.DestroyImmediate(studioProject);
                if (manualRoundTrip != null) UnityEngine.Object.DestroyImmediate(manualRoundTrip);
                if (studioRoundTrip != null) UnityEngine.Object.DestroyImmediate(studioRoundTrip);
            }

            return result;
        }

        private static CoverageResult EvaluateCoverage(IEnumerable<string> casePaths)
        {
            var foundConditions = new HashSet<string>();
            var foundActions = new HashSet<string>();
            var foundOperators = new HashSet<string>();
            bool conditionalRule = false;
            bool unconditionalRule = false;
            bool localReference = false;
            bool globalReference = false;
            bool crossActorReference = false;

            foreach (string path in casePaths)
            {
                GameRuleProject project = GameRuleProject.ImportFromJson(path);
                try
                {
                    RequireProject(project, path);
                    EnsureCollections(project);
                    foreach (ActorJson actor in project.actors)
                    {
                        foreach (SentenceJson rule in actor.Script)
                        {
                            if (rule.When.Count == 0) unconditionalRule = true;
                            else conditionalRule = true;

                            foreach (string expression in rule.When)
                            {
                                CollectReferences(expression, ref localReference, ref globalReference, ref crossActorReference);
                                foreach (string token in GameRuleParser.TokenizeCondition(expression))
                                {
                                    if (token == "AND" || token == "OR" || token == "NOT")
                                    {
                                        foundOperators.Add(token);
                                        continue;
                                    }

                                    (string conditionName, List<string> _) = GameRuleParser.ParseFunction(token);
                                    if (!string.IsNullOrEmpty(conditionName)) foundConditions.Add(conditionName);
                                }
                            }

                            foreach (string action in rule.Do)
                            {
                                CollectReferences(action, ref localReference, ref globalReference, ref crossActorReference);
                                (string actionName, List<string> _) = GameRuleParser.ParseFunction(action);
                                if (!string.IsNullOrEmpty(actionName)) foundActions.Add(actionName);
                            }
                        }
                    }
                }
                finally
                {
                    if (project != null) UnityEngine.Object.DestroyImmediate(project);
                }
            }

            var result = new CoverageResult
            {
                conditions = foundConditions.OrderBy(value => value).ToList(),
                actions = foundActions.OrderBy(value => value).ToList(),
                booleanOperators = foundOperators.OrderBy(value => value).ToList(),
                conditionalAndUnconditionalRules = conditionalRule && unconditionalRule,
                localGlobalAndCrossActorReferences = localReference && globalReference && crossActorReference
            };
            result.missingConditions = CoreConditions.Except(foundConditions).OrderBy(value => value).ToList();
            result.missingActions = CoreActions.Except(foundActions).OrderBy(value => value).ToList();
            result.missingBooleanOperators = new[] { "AND", "OR", "NOT" }
                .Except(foundOperators).OrderBy(value => value).ToList();
            result.pass =
                result.missingConditions.Count == 0 &&
                result.missingActions.Count == 0 &&
                result.missingBooleanOperators.Count == 0 &&
                result.conditionalAndUnconditionalRules &&
                result.localGlobalAndCrossActorReferences;
            return result;
        }

        private static void CollectReferences(
            string value,
            ref bool localReference,
            ref bool globalReference,
            ref bool crossActorReference)
        {
            if (string.IsNullOrEmpty(value)) return;
            localReference |= value.Contains("this.");
            globalReference |= value.Contains("#");
            crossActorReference |= Regex.IsMatch(value, @"(?<![#A-Za-z0-9_])(?:[A-Z][A-Za-z0-9_]*)\.[A-Za-z_]");
        }

        private static RuntimeResult RunRuntimeCheck(string name, Func<string> check)
        {
            var result = new RuntimeResult { name = name };
            try
            {
                result.detail = check();
                result.pass = true;
            }
            catch (Exception exception)
            {
                result.pass = false;
                result.detail = exception.ToString();
            }
            finally
            {
                ActorScheduler.Build(Array.Empty<string>());
            }
            return result;
        }

        private static string ValidateActorOrder()
        {
            var objects = new List<GameObject>();
            var evaluation = new List<string>();
            try
            {
                EquivalenceProbeActor first = CreateProbe("EV_Order_First", objects, () => evaluation.Add("First"));
                EquivalenceProbeActor second = CreateProbe("EV_Order_Second", objects, () => evaluation.Add("Second"));
                EquivalenceProbeActor third = CreateProbe("EV_Order_Third", objects, () => evaluation.Add("Third"));
                Require(first != null && second != null && third != null, "Could not create actor-order probes.");

                ActorScheduler.Build(new[] { "EV_Order_Second", "EV_Order_First", "EV_Order_Third" });
                ActorScheduler.RunFixedUpdate();
                Require(evaluation.SequenceEqual(new[] { "Second", "First", "Third" }),
                    "Observed order: " + string.Join(", ", evaluation));
                return "Observed declaration order: " + string.Join(" -> ", evaluation);
            }
            finally
            {
                DestroyObjects(objects);
            }
        }

        private static string ValidateMultipleWrites()
        {
            var objects = new List<GameObject>();
            try
            {
                var stateObject = new GameObject("EquivalenceStateActor");
                objects.Add(stateObject);
                EquivalenceStateActor state = stateObject.AddComponent<EquivalenceStateActor>();

                var scope = new Dictionary<string, GameObject>
                {
                    { "EquivalenceStateActor.value", stateObject }
                };

                CreateProbe("EV_Writer_A", objects, () =>
                {
                    global::Action.Edit("EquivalenceStateActor.value", "1", scope);
                    global::Action.Edit("EquivalenceStateActor.value", "2", scope);
                });
                CreateProbe("EV_Writer_B", objects, () =>
                    global::Action.Edit("EquivalenceStateActor.value", "3", scope));

                ActorScheduler.Build(new[] { "EV_Writer_A", "EV_Writer_B" });
                ActorScheduler.RunFixedUpdate();
                Require(Mathf.Approximately(state.value, 3f), $"Expected 3, observed {state.value}.");

                state.value = 0f;
                ActorScheduler.Build(new[] { "EV_Writer_B", "EV_Writer_A" });
                ActorScheduler.RunFixedUpdate();
                Require(Mathf.Approximately(state.value, 2f), $"Expected 2 after reversing actor order, observed {state.value}.");

                return "A then B produced 3; B then A produced 2. The final write in the declared order prevailed.";
            }
            finally
            {
                DestroyObjects(objects);
            }
        }

        private static string ValidateDeferredLifecycle()
        {
            var objects = new List<GameObject>();
            try
            {
                var spawnLog = new List<string>();
                EquivalenceProbeActor spawned = CreateProbe("EV_Spawned", objects, () => spawnLog.Add("Spawned"));
                bool registered = false;
                EquivalenceProbeActor spawner = CreateProbe("EV_Spawner", objects, () =>
                {
                    spawnLog.Add("Spawner");
                    if (registered) return;
                    ActorScheduler.RegisterSpawned(spawned);
                    registered = true;
                });
                CreateProbe("EV_Existing", objects, () => spawnLog.Add("Existing"));

                ActorScheduler.Build(new[] { "EV_Spawner", "EV_Existing" });
                ActorScheduler.RunFixedUpdate();
                Require(spawnLog.SequenceEqual(new[] { "Spawner", "Existing" }),
                    "Spawned actor ran during the insertion pass: " + string.Join(", ", spawnLog));
                ActorScheduler.RunFixedUpdate();
                Require(spawnLog.SequenceEqual(new[] { "Spawner", "Existing", "Spawner", "Existing", "Spawned" }),
                    "Spawned actor did not run in the following pass: " + string.Join(", ", spawnLog));

                var deleteLog = new List<string>();
                bool unregistered = false;
                EquivalenceProbeActor removed = CreateProbe("EV_Removed", objects, () => deleteLog.Add("Removed"));
                CreateProbe("EV_Remover", objects, () =>
                {
                    deleteLog.Add("Remover");
                    if (unregistered) return;
                    ActorScheduler.Unregister(removed);
                    unregistered = true;
                });

                ActorScheduler.Build(new[] { "EV_Remover", "EV_Removed" });
                ActorScheduler.RunFixedUpdate();
                Require(deleteLog.SequenceEqual(new[] { "Remover", "Removed" }),
                    "Removed actor was skipped before the pass finished: " + string.Join(", ", deleteLog));
                ActorScheduler.RunFixedUpdate();
                Require(deleteLog.SequenceEqual(new[] { "Remover", "Removed", "Remover" }),
                    "Removed actor remained in the following pass: " + string.Join(", ", deleteLog));

                Require(spawner != null, "Spawner probe was not created.");
                return "Spawned actors began on the next pass; removed actors were excluded from the next pass.";
            }
            finally
            {
                DestroyObjects(objects);
            }
        }

        private static EquivalenceProbeActor CreateProbe(
            string objectName,
            ICollection<GameObject> objects,
            System.Action fixedEvaluation)
        {
            var gameObject = new GameObject(objectName);
            objects.Add(gameObject);
            EquivalenceProbeActor probe = gameObject.AddComponent<EquivalenceProbeActor>();
            probe.OnFixedEvaluation = fixedEvaluation;
            return probe;
        }

        private static void DestroyObjects(IEnumerable<GameObject> objects)
        {
            ActorScheduler.Build(Array.Empty<string>());
            foreach (GameObject gameObject in objects.Where(item => item != null).Reverse())
                UnityEngine.Object.DestroyImmediate(gameObject);
        }

        private static Dictionary<string, string> GenerateSourceSnapshot(GameRuleProject project, string projectRoot)
        {
            EnsureCollections(project);
            string scriptsDirectory = Path.Combine(projectRoot, "Assets", "Resources", "Scripts");
            Directory.CreateDirectory(scriptsDirectory);
            Dictionary<string, byte[]> backup = SnapshotDirectory(scriptsDirectory);

            try
            {
                List<string> declarationOrder = project.actors.Select(actor => actor.ActorName).ToList();
                Scripts.CreateGameManager(project.sceneData, declarationOrder);
                Scripts.Create(project.actors);

                var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
                string managerPath = Path.Combine(scriptsDirectory, "GameManager.cs");
                snapshot["GameManager.cs"] = NormalizeCSharp(File.ReadAllText(managerPath));
                foreach (ActorJson actor in project.actors)
                {
                    string fileName = actor.ActorName + ".cs";
                    string filePath = Path.Combine(scriptsDirectory, fileName);
                    snapshot[fileName] = NormalizeCSharp(File.ReadAllText(filePath));
                }
                return snapshot;
            }
            finally
            {
                RestoreDirectory(scriptsDirectory, backup);
            }
        }

        private static Dictionary<string, byte[]> SnapshotDirectory(string directory)
        {
            if (!Directory.Exists(directory)) return new Dictionary<string, byte[]>();
            return Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .ToDictionary(
                    path => RelativePath(directory, path),
                    File.ReadAllBytes,
                    StringComparer.Ordinal);
        }

        private static void RestoreDirectory(string directory, Dictionary<string, byte[]> backup)
        {
            foreach (string path in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                string relative = RelativePath(directory, path);
                if (!backup.ContainsKey(relative)) File.Delete(path);
            }

            foreach (KeyValuePair<string, byte[]> pair in backup)
            {
                string path = Path.Combine(directory, pair.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? directory);
                File.WriteAllBytes(path, pair.Value);
            }
        }

        private static string RelativePath(string root, string path)
        {
            Uri rootUri = new Uri(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            Uri pathUri = new Uri(path);
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString())
                .Replace('/', Path.DirectorySeparatorChar);
        }

        private static bool GeneratedSourcesEqual(
            Dictionary<string, string> left,
            Dictionary<string, string> right)
        {
            return left.Count == right.Count &&
                   left.All(pair => right.TryGetValue(pair.Key, out string value) && pair.Value == value);
        }

        private static string SourceSnapshotSha256(Dictionary<string, string> snapshot)
        {
            var builder = new StringBuilder();
            foreach (KeyValuePair<string, string> pair in snapshot.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                builder.Append(pair.Key.Length).Append(':').Append(pair.Key)
                    .Append(pair.Value.Length).Append(':').Append(pair.Value);
            }
            return Sha256(builder.ToString());
        }

        private static void WriteSourceSnapshot(
            string evidenceDirectory,
            string label,
            Dictionary<string, string> snapshot)
        {
            foreach (KeyValuePair<string, string> pair in snapshot.OrderBy(item => item.Key, StringComparer.Ordinal))
                WriteEvidenceText(evidenceDirectory, Path.Combine(label, pair.Key), pair.Value);
        }

        private static void WriteEvidenceText(string evidenceDirectory, string relativePath, string content)
        {
            string path = Path.Combine(evidenceDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? evidenceDirectory);
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        private static string SafePathSegment(string value)
        {
            var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
            return new string((value ?? string.Empty)
                    .Select(character => invalid.Contains(character) || char.IsWhiteSpace(character) ? '_' : character)
                    .ToArray())
                .Trim('_');
        }

        private static EvidenceSummary WriteEvidenceManifest(
            string evidenceDirectory,
            ValidationReport report)
        {
            var manifest = new EvidenceManifest
            {
                generatedUtc = report.generatedUtc,
                unityVersion = report.unityVersion,
                expectedUnityVersion = report.expectedUnityVersion,
                sourceRevision = report.sourceRevision,
                overallPass = report.overallPass
            };

            foreach (string path in Directory.GetFiles(evidenceDirectory, "*", SearchOption.AllDirectories)
                         .OrderBy(item => item, StringComparer.Ordinal))
            {
                manifest.artifacts.Add(new EvidenceArtifact
                {
                    path = RelativePath(evidenceDirectory, path).Replace('\\', '/'),
                    bytes = new FileInfo(path).Length,
                    sha256 = Sha256File(path)
                });
            }

            string manifestJson = NormalizeLineEndings(JsonConvert.SerializeObject(manifest, Formatting.Indented));
            WriteEvidenceText(evidenceDirectory, "evidence-manifest.json", manifestJson);
            return new EvidenceSummary
            {
                directory = "Validation/Equivalence/Results/Evidence",
                manifest = "Validation/Equivalence/Results/Evidence/evidence-manifest.json",
                artifactCount = manifest.artifacts.Count,
                manifestSha256 = Sha256(manifestJson)
            };
        }

        private static string NormalizeCSharp(string source)
        {
            return string.Join("\n", NormalizeLineEndings(source)
                .Split('\n').Select(line => line.TrimEnd())).Trim();
        }

        private static string NormalizeLineEndings(string value)
        {
            return (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        }

        private static GameRuleProject ImportTemporary(string json)
        {
            string path = Path.Combine(Path.GetTempPath(), "gamerule-equivalence-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(path, json, new UTF8Encoding(false));
                return GameRuleProject.ImportFromJson(path);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private static void RequireProject(GameRuleProject project, string source)
        {
            if (project == null) throw new InvalidOperationException("Could not import " + source + ".");
            EnsureCollections(project);
        }

        private static void EnsureCollections(GameRuleProject project)
        {
            if (project.actors == null) project.actors = new List<ActorJson>();
            project.sceneData.Cast = project.actors;
            if (project.sceneData.CustomVariables == null)
                project.sceneData.CustomVariables = new List<CustomVariable>();

            foreach (ActorJson actor in project.actors)
            {
                if (actor.Properties == null) actor.Properties = new List<string>();
                if (actor.Script == null) actor.Script = new List<SentenceJson>();
                if (actor.Components == null) actor.Components = new List<ActorComponentMeta>();
                foreach (SentenceJson rule in actor.Script)
                {
                    if (rule.When == null) rule.When = new List<string>();
                    if (rule.Do == null) rule.Do = new List<string>();
                }
            }
        }

        private static bool SameOrderedTopology(GameRuleProject left, GameRuleProject right)
        {
            EnsureCollections(left);
            EnsureCollections(right);
            if (left.actors.Count != right.actors.Count) return false;
            for (int actorIndex = 0; actorIndex < left.actors.Count; actorIndex++)
            {
                ActorJson leftActor = left.actors[actorIndex];
                ActorJson rightActor = right.actors[actorIndex];
                if (leftActor.ActorName != rightActor.ActorName) return false;
                if (!leftActor.Properties.SequenceEqual(rightActor.Properties)) return false;
                if (leftActor.Script.Count != rightActor.Script.Count) return false;
                for (int ruleIndex = 0; ruleIndex < leftActor.Script.Count; ruleIndex++)
                {
                    SentenceJson leftRule = leftActor.Script[ruleIndex];
                    SentenceJson rightRule = rightActor.Script[ruleIndex];
                    if (!leftRule.When.SequenceEqual(rightRule.When)) return false;
                    if (!leftRule.Do.SequenceEqual(rightRule.Do)) return false;
                }
            }
            return true;
        }

        private static string BuildParsedAst(GameRuleProject project)
        {
            EnsureCollections(project);
            var builder = new StringBuilder();
            builder.AppendLine("GAME " + Quote(project.sceneData.GameName));
            foreach (CustomVariable variable in project.sceneData.CustomVariables)
            {
                builder.Append("GLOBAL ").Append(Quote(variable.name)).Append(' ')
                    .Append(Quote(variable.type)).Append(' ')
                    .Append(CanonicalVariableValue(variable)).AppendLine();
            }

            for (int actorIndex = 0; actorIndex < project.actors.Count; actorIndex++)
            {
                ActorJson actor = project.actors[actorIndex];
                builder.Append("ACTOR ").Append(actorIndex).Append(' ')
                    .Append(Quote(actor.ActorName)).Append(' ')
                    .Append(Quote(actor.PrefabName)).Append(' ')
                    .Append(Quote(actor.Tag ?? string.Empty)).Append(' ')
                    .Append(actor.Active ? "1" : "0").AppendLine();

                for (int propertyIndex = 0; propertyIndex < actor.Properties.Count; propertyIndex++)
                    builder.Append("PROPERTY ").Append(propertyIndex).Append(' ')
                        .Append(Quote(actor.Properties[propertyIndex])).AppendLine();

                for (int ruleIndex = 0; ruleIndex < actor.Script.Count; ruleIndex++)
                {
                    SentenceJson rule = actor.Script[ruleIndex];
                    builder.Append("RULE ").Append(ruleIndex).AppendLine();
                    foreach (string expression in rule.When)
                    {
                        builder.AppendLine("WHEN");
                        foreach (string token in GameRuleParser.TokenizeCondition(expression))
                        {
                            if (token == "AND" || token == "OR" || token == "NOT")
                                builder.Append("OP ").Append(token).AppendLine();
                            else
                                AppendFunctionNode(builder, "CONDITION", token);
                        }
                    }
                    for (int actionIndex = 0; actionIndex < rule.Do.Count; actionIndex++)
                    {
                        builder.Append("ACTION_INDEX ").Append(actionIndex).AppendLine();
                        AppendFunctionNode(builder, "ACTION", rule.Do[actionIndex]);
                    }
                }
            }
            return NormalizeLineEndings(builder.ToString());
        }

        private static void AppendFunctionNode(StringBuilder builder, string kind, string source)
        {
            (string name, List<string> parameters) = GameRuleParser.ParseFunction(source);
            builder.Append(kind).Append(' ').Append(Quote(name ?? string.Empty));
            foreach (string parameter in parameters ?? new List<string>())
                builder.Append(' ').Append(Quote(parameter));
            builder.AppendLine();
        }

        private static string CanonicalVariableValue(CustomVariable variable)
        {
            switch ((variable.type ?? string.Empty).ToLowerInvariant())
            {
                case "int": return variable.intValue.ToString(CultureInfo.InvariantCulture);
                case "float": return Math.Round(variable.floatValue, 6).ToString(CultureInfo.InvariantCulture);
                case "bool": return variable.boolValue ? "true" : "false";
                case "vector2":
                case "vector3":
                    return "[" + string.Join(",", (variable.arrayValue ?? Array.Empty<float>())
                        .Select(value => Math.Round(value, 6).ToString(CultureInfo.InvariantCulture))) + "]";
                default: return string.Empty;
            }
        }

        private static string Quote(string value)
        {
            return JsonConvert.ToString(value ?? string.Empty);
        }

        private static string CanonicalizeJson(string json)
        {
            JToken token = JToken.Parse(json);
            NormalizeNumbers(token);
            if (!(token is JObject root)) throw new InvalidDataException("The descriptor root must be an object.");

            RemoveDefault(root, "ScreenResolution", new JArray(1920, 1080));
            RemoveDefault(root, "CameraPosition", new JArray(0, 1, -10));
            RemoveDefault(root, "CameraRotation", new JArray(0, 0, 0));
            RemoveDefault(root, "SunPosition", new JArray(0, 3, 0));
            RemoveDefault(root, "SunRotation", new JArray(50, -30, 0));
            RemoveDefault(root, "SunColor", new JArray(255, 255, 255));
            RemoveDefault(root, "SunAmbientColor", new JArray(128, 128, 128));
            RemoveDefault(root, "BackgroundColor", new JArray(0, 0, 0));
            RemoveDefault(root, "Gravity", new JArray(0, -9.81, 0));
            RemoveDefault(root, "CustomVariables", new JArray());

            if (root["Cast"] is JArray cast)
            {
                foreach (JObject actor in cast.OfType<JObject>())
                {
                    RemoveDefault(actor, "Active", new JValue(true));
                    RemoveDefault(actor, "Tag", new JValue(string.Empty));
                    RemoveDefault(actor, "IconColorHex", new JValue(string.Empty));
                    RemoveDefault(actor, "Properties", new JArray());
                    RemoveDefault(actor, "Components", new JArray());
                    RemoveDefault(actor, "Script", new JArray());

                    if (actor["Script"] is JArray script)
                    {
                        foreach (JObject rule in script.OfType<JObject>())
                        {
                            RemoveDefault(rule, "Name", new JValue(string.Empty));
                            RemoveDefault(rule, "groupId", new JValue(string.Empty));
                            RemoveDefault(rule, "When", new JArray());
                            RemoveDefault(rule, "Do", new JArray());
                        }
                    }
                }
            }

            return SortJson(token).ToString(Formatting.None);
        }

        private static void RemoveDefault(JObject owner, string propertyName, JToken defaultValue)
        {
            JProperty property = owner.Property(propertyName);
            if (property != null && JToken.DeepEquals(property.Value, defaultValue)) property.Remove();
        }

        private static void NormalizeNumbers(JToken token)
        {
            if (token is JValue value)
            {
                if (value.Type != JTokenType.Float && value.Type != JTokenType.Integer) return;
                double number = value.Value<double>();
                double rounded = Math.Round(number, 6, MidpointRounding.AwayFromZero);
                if (Math.Abs(rounded - Math.Round(rounded)) < 0.0000001)
                    value.Replace(new JValue(Convert.ToInt64(Math.Round(rounded))));
                else
                    value.Replace(new JValue(rounded));
                return;
            }

            foreach (JToken child in token.Children().ToList()) NormalizeNumbers(child);
        }

        private static JToken SortJson(JToken token)
        {
            if (token is JObject obj)
            {
                var sorted = new JObject();
                foreach (JProperty property in obj.Properties().OrderBy(property => property.Name, StringComparer.Ordinal))
                    sorted.Add(property.Name, SortJson(property.Value));
                return sorted;
            }

            if (token is JArray array)
                return new JArray(array.Select(SortJson));

            return token.DeepClone();
        }

        private static string Sha256(string value)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value));
                return string.Concat(hash.Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        private static string Sha256File(string path)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = algorithm.ComputeHash(stream);
                return string.Concat(hash.Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static string BuildMarkdownReport(ValidationReport report)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# GameRule Studio equivalence validation");
            builder.AppendLine();
            builder.AppendLine("- Result: **" + (report.overallPass ? "PASS" : "FAIL") + "**");
            builder.AppendLine("- Unity: `" + report.unityVersion + "` (expected `" + report.expectedUnityVersion + "`)");
            builder.AppendLine("- Source revision: `" + report.sourceRevision + "`");
            builder.AppendLine("- Generated: `" + report.generatedUtc + "`");
            builder.AppendLine();
            builder.AppendLine("## Controlled cases");
            builder.AppendLine();
            AppendDescriptorTable(builder, report.controlledCases);
            builder.AppendLine();
            builder.AppendLine("## Full-game integration pairs");
            builder.AppendLine();
            AppendDescriptorTable(builder, report.integrationCases);
            builder.AppendLine();
            builder.AppendLine("## Runtime checks");
            builder.AppendLine();
            builder.AppendLine("| Check | Result | Detail |");
            builder.AppendLine("|---|---|---|");
            foreach (RuntimeResult runtime in report.runtimeChecks)
                builder.Append("| ").Append(runtime.name).Append(" | ")
                    .Append(runtime.pass ? "Pass" : "Fail").Append(" | ")
                    .Append(EscapeMarkdown(runtime.detail)).AppendLine(" |");
            builder.AppendLine();
            builder.AppendLine("## Coverage");
            builder.AppendLine();
            if (report.coverage != null)
            {
                builder.AppendLine("- Six formal conditions: " + JoinOrNone(report.coverage.conditions));
                builder.AppendLine("- Fourteen formal actions: " + JoinOrNone(report.coverage.actions));
                builder.AppendLine("- Boolean operators: " + JoinOrNone(report.coverage.booleanOperators));
                builder.AppendLine("- Conditional and unconditional rules: " + PassFail(report.coverage.conditionalAndUnconditionalRules));
                builder.AppendLine("- Local, global, and cross-actor references: " + PassFail(report.coverage.localGlobalAndCrossActorReferences));
                builder.AppendLine("- Coverage result: **" + PassFail(report.coverage.pass) + "**");
            }
            if (report.evidence != null)
            {
                builder.AppendLine();
                builder.AppendLine("## Inspectable evidence");
                builder.AppendLine();
                builder.AppendLine("- Directory: `" + report.evidence.directory + "`");
                builder.AppendLine("- Manifest: `" + report.evidence.manifest + "`");
                builder.AppendLine("- Indexed artifacts: " + report.evidence.artifactCount);
                builder.AppendLine("- Manifest SHA-256: `" + report.evidence.manifestSha256 + "`");
            }
            if (report.runnerErrors.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Runner errors");
                builder.AppendLine();
                foreach (string error in report.runnerErrors) builder.AppendLine("- " + EscapeMarkdown(error));
            }
            return builder.ToString();
        }

        private static void AppendDescriptorTable(StringBuilder builder, IEnumerable<DescriptorResult> results)
        {
            builder.AppendLine("| Case | Canonical JSON | Parsed AST | Generated C# | Round-trip |");
            builder.AppendLine("|---|---:|---:|---:|---:|");
            foreach (DescriptorResult result in results)
            {
                builder.Append("| ").Append(result.name).Append(" | ")
                    .Append(PassFail(result.canonicalJson)).Append(" | ")
                    .Append(PassFail(result.parsedAst)).Append(" | ")
                    .Append(PassFail(result.generatedCSharp)).Append(" | ")
                    .Append(PassFail(result.roundTrip)).AppendLine(" |");
            }
        }

        private static string PassFail(bool pass)
        {
            return pass ? "Pass" : "Fail";
        }

        private static string JoinOrNone(IEnumerable<string> values)
        {
            string joined = string.Join(", ", values ?? Enumerable.Empty<string>());
            return string.IsNullOrEmpty(joined) ? "None" : joined;
        }

        private static string EscapeMarkdown(string value)
        {
            return (value ?? string.Empty).Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }

        private sealed class IntegrationPair
        {
            public readonly string name;
            public readonly string manualFile;
            public readonly string studioFile;

            public IntegrationPair(string name, string manualFile, string studioFile)
            {
                this.name = name;
                this.manualFile = manualFile;
                this.studioFile = studioFile;
            }
        }

        [Serializable]
        private sealed class ValidationReport
        {
            public string generatedUtc;
            public string unityVersion;
            public string expectedUnityVersion;
            public string sourceRevision;
            public bool overallPass;
            public List<DescriptorResult> controlledCases = new List<DescriptorResult>();
            public List<DescriptorResult> integrationCases = new List<DescriptorResult>();
            public CoverageResult coverage;
            public List<RuntimeResult> runtimeChecks = new List<RuntimeResult>();
            public EvidenceSummary evidence;
            public List<string> runnerErrors = new List<string>();
        }

        [Serializable]
        private sealed class DescriptorResult
        {
            public string name;
            public bool canonicalJson;
            public bool parsedAst;
            public bool generatedCSharp;
            public bool roundTrip;
            public string sourceCanonicalSha256;
            public string outputCanonicalSha256;
            public string sourceParsedAstSha256;
            public string outputParsedAstSha256;
            public string sourceGeneratedCSharpSha256;
            public string outputGeneratedCSharpSha256;
            public List<string> errors = new List<string>();
            [JsonIgnore] public bool AllPassed =>
                canonicalJson && parsedAst && generatedCSharp && roundTrip && errors.Count == 0;
        }

        [Serializable]
        private sealed class CoverageResult
        {
            public bool pass;
            public List<string> conditions = new List<string>();
            public List<string> actions = new List<string>();
            public List<string> booleanOperators = new List<string>();
            public List<string> missingConditions = new List<string>();
            public List<string> missingActions = new List<string>();
            public List<string> missingBooleanOperators = new List<string>();
            public bool conditionalAndUnconditionalRules;
            public bool localGlobalAndCrossActorReferences;
        }

        [Serializable]
        private sealed class RuntimeResult
        {
            public string name;
            public bool pass;
            public string detail;
        }

        [Serializable]
        private sealed class EvidenceSummary
        {
            public string directory;
            public string manifest;
            public int artifactCount;
            public string manifestSha256;
        }

        [Serializable]
        private sealed class EvidenceManifest
        {
            public string generatedUtc;
            public string unityVersion;
            public string expectedUnityVersion;
            public string sourceRevision;
            public bool overallPass;
            public List<EvidenceArtifact> artifacts = new List<EvidenceArtifact>();
        }

        [Serializable]
        private sealed class EvidenceArtifact
        {
            public string path;
            public long bytes;
            public string sha256;
        }
    }
}
