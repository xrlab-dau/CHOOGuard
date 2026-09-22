#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.Compilation;
using UnityEngine;

namespace ChooGuard.Tests.EditMode.Stories
{
    public class CSBOOT0201Tests
    {
        private const string ProductRoot = "Assets/ChooGuard";
        private const string ContractPath = "docs/CHOOGuard_Story_Plan_v4/basis/v3/contracts/assembly-layout.json";
        private const string EditModeName = "ChooGuard.EditModeTests";
        private const string PlayModeName = "ChooGuard.PlayModeTests";
        private static readonly string[] UiReferences = { "UnityEngine.UI", "Unity.TextMeshPro", "Unity.InputSystem" };
        private static readonly string[] RenderReferences = { "Unity.RenderPipelines.Core.Runtime", "Unity.RenderPipelines.Universal.Runtime" };
        private static string ProjectRoot => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));

        [Test]
        public void SubmittedAssemblies_MatchThirteenModulesAndTwoTestBoundaries()
        {
            var snapshot = Capture();
            Assert.That(snapshot.Modules.Length, Is.EqualTo(13));
            Assert.That(snapshot.Modules.Count(module => !module.engineReferences), Is.EqualTo(9));
            Assert.That(Validate(snapshot), Is.Empty);
        }

        [Test]
        public void EverySubmittedSource_CompilesIntoItsDeepestDeclaredAssembly()
        {
            var snapshot = Capture();
            Assert.That(snapshot.Sources, Is.Not.Empty);
            foreach (var path in snapshot.Sources.Keys)
            {
                var owner = FindOwner(path, snapshot.Definitions.Values);
                Assert.That(owner, Is.Not.Null, "Assembly-CSharp fallback: " + path);
                Assert.That(CompilationPipeline.GetAssemblyNameFromScriptPath(path),
                    Is.EqualTo(owner.name + ".dll"), path);
            }
        }

        [Test]
        public void CompiledPureModules_WithSourcesHaveNoUnityOrDefaultAssemblyDependencies()
        {
            var snapshot = Capture();
            var compiled = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            foreach (var module in snapshot.Modules.Where(module => !module.engineReferences))
            {
                // Empty modules intentionally have no dummy C# and need not produce a DLL.
                if (!snapshot.Sources.Keys.Any(path => IsWithin(path, module.root))) continue;
                var assembly = compiled.SingleOrDefault(candidate => candidate.name == module.name);
                Assert.That(assembly, Is.Not.Null, module.name);
                Assert.That(assembly.assemblyReferences.Select(reference => reference.name)
                    .Where(IsForbiddenPureReference), Is.Empty, module.name + " declared assembly dependencies");
                // compiledAssemblyReferences lists compiler inputs, not emitted AssemblyRef dependencies.
                // The installed macOS support module supplies Xcode to user-script compilation even with
                // noEngineReferences/overrideReferences enabled. Observe inputs; never allowlist emitted refs.
                TestContext.WriteLine(module.name + " available compiler references: " +
                    string.Join(", ", assembly.compiledAssemblyReferences));
                var outputPath = Path.GetFullPath(Path.Combine(ProjectRoot, assembly.outputPath));
                Assert.That(File.Exists(outputPath), Is.True, module.name + " emitted DLL is required");
                var emitted = System.Reflection.Assembly.ReflectionOnlyLoad(File.ReadAllBytes(outputPath));
                Assert.That(emitted.GetName().Name, Is.EqualTo(module.name), outputPath);
                var references = emitted.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
                TestContext.WriteLine(module.name + " emitted AssemblyRef: " + string.Join(", ", references));
                Assert.That(references.Where(IsForbiddenPureReference), Is.Empty, module.name + " emitted AssemblyRef");
            }
        }

        [TestCase("UnityEngine")]
        [TestCase("UnityEngine.CoreModule")]
        [TestCase("UnityEditor")]
        [TestCase("UnityEditor.CoreModule")]
        [TestCase("UnityEditor.iOS.Extensions.Xcode")]
        public void PureAssembly_RejectsExplicitUnityReference(string reference)
        {
            var snapshot = Capture();
            var domain = snapshot.Definitions["ChooGuard.Domain"];
            domain.references = domain.references.Concat(new[] { reference }).ToArray();
            Assert.That(Validate(snapshot), Does.Contain("pure-reference:ChooGuard.Domain:" + reference));
        }

        [TestCase("Contracts")]
        [TestCase("Domain")]
        [TestCase("Application")]
        [TestCase("Content")]
        [TestCase("Persistence")]
        [TestCase("Experiments")]
        [TestCase("Scenarios")]
        [TestCase("Simulation")]
        [TestCase("Authoring")]
        public void PureAssembly_RejectsEnabledEngineReferencesAndExplicitUnityReference(string module)
        {
            var name = "ChooGuard." + module;
            var snapshot = Capture();
            snapshot.Definitions[name].noEngineReferences = false;
            Assert.That(Validate(snapshot), Does.Contain("engine-boundary:" + name));
            snapshot = Capture();
            snapshot.Definitions[name].references = new[] { "UnityEngine.CoreModule" };
            Assert.That(Validate(snapshot), Does.Contain("pure-reference:" + name + ":UnityEngine.CoreModule"));
        }

        [Test]
        public void PureAssembly_RejectsImplicitOrExplicitPluginReferences()
        {
            var snapshot = Capture();
            snapshot.Definitions["ChooGuard.Domain"].overrideReferences = false;
            Assert.That(Validate(snapshot), Does.Contain("plugin-boundary:ChooGuard.Domain"));
            snapshot.Definitions["ChooGuard.Domain"].overrideReferences = true;
            snapshot.Definitions["ChooGuard.Domain"].precompiledReferences = new[] { "UnityEngine.CoreModule.dll" };
            Assert.That(Validate(snapshot), Does.Contain("plugin-boundary:ChooGuard.Domain"));
        }

        [TestCase("Assembly-CSharp")]
        [TestCase("Assembly-CSharp.dll")]
        [TestCase("Assembly-CSharp-firstpass")]
        [TestCase("Assembly-CSharp-Editor")]
        public void CustomAssembly_RejectsPredefinedAssemblyReference(string reference)
        {
            var snapshot = Capture();
            snapshot.Definitions["ChooGuard.Domain"].references = new[] { "ChooGuard.Contracts", reference };
            Assert.That(Validate(snapshot), Does.Contain("default-reference:ChooGuard.Domain:" + reference));
        }

        [TestCase("using V = UnityEngine.Vector3; public class Invalid { V value; }")]
        [TestCase("using UnityEngine; public class Invalid { Vector3 value; }")]
        [TestCase("using E = global::UnityEngine; public class Invalid { E.Vector3 value; }")]
        [TestCase("public class Invalid { global::UnityEngine.Vector3 value; }")]
        [TestCase("using UnityEditor; public class Invalid { SerializedObject value; }")]
        public void PureSource_RejectsUnityNamespaceIncludingAliasAndQualifiedTypes(string source)
        {
            var snapshot = Capture();
            const string path = ProductRoot + "/Domain/Invalid.cs";
            snapshot.Sources[path] = source;
            Assert.That(Validate(snapshot), Does.Contain("pure-source:" + path));
        }

        [Test]
        public void SourceInspection_DoesNotMistakeCommentsOrLiteralsForUnityDependencies()
        {
            var snapshot = Capture();
            snapshot.Sources[ProductRoot + "/Domain/Documentation.cs"] =
                "// UnityEngine.Vector3\n/* UnityEditor */\npublic class Documentation { " +
                "const string A = \"UnityEngine\"; const string B = @\"UnityEditor\"; }";
            Assert.That(Validate(snapshot), Is.Empty);
        }

        [Test]
        public void RuntimeSource_RejectsEditorCodeEvenInsideEditorConditional()
        {
            var snapshot = Capture();
            const string path = ProductRoot + "/Presentation/Invalid.cs";
            snapshot.Sources[path] = "#if UNITY_EDITOR\nusing UnityEditor;\n#endif\nclass Invalid {}";
            Assert.That(Validate(snapshot), Does.Contain("editor-source:" + path));
        }

        [Test]
        public void Graph_RejectsCycleThroughTwoModules()
        {
            var snapshot = Capture();
            snapshot.Definitions["ChooGuard.Contracts"].references = new[] { "ChooGuard.Domain" };
            Assert.That(Validate(snapshot), Does.Contain("cycle:ChooGuard.Contracts"));
        }

        [Test]
        public void Graph_RejectsSelfReference()
        {
            var snapshot = Capture();
            snapshot.Definitions["ChooGuard.Contracts"].references = new[] { "ChooGuard.Contracts" };
            Assert.That(Validate(snapshot), Does.Contain("cycle:ChooGuard.Contracts"));
        }

        [Test]
        public void SourceWithoutDeclaredRoot_RejectsDefaultAssemblyFallback()
        {
            var snapshot = Capture();
            const string path = ProductRoot + "/Loose.cs";
            snapshot.Sources[path] = "public class Loose {}";
            Assert.That(Validate(snapshot), Does.Contain("unowned-source:" + path));
        }

        [Test]
        public void MissingModuleDefinition_IsRejectedEvenWhenTheModuleIsEmpty()
        {
            var snapshot = Capture();
            snapshot.Definitions.Remove("ChooGuard.Domain");
            Assert.That(Validate(snapshot), Does.Contain("missing-assembly:ChooGuard.Domain"));
        }

        [Test]
        public void OwnerSelection_UsesDeepestDirectoryAndNotStringPrefix()
        {
            var outer = new Definition { name = "Outer", Root = ProductRoot };
            var inner = new Definition { name = "Inner", Root = ProductRoot + "/Domain" };
            Assert.That(FindOwner(ProductRoot + "/Domain/Nested/Type.cs", new[] { outer, inner }), Is.SameAs(inner));
            Assert.That(FindOwner(ProductRoot + "/DomainExtra/Type.cs", new[] { inner }), Is.Null);
        }

        [Test]
        public void UndeclaredNestedAssemblyAndAssemblyReferenceAsset_AreRejected()
        {
            var snapshot = Capture();
            snapshot.Definitions.Add("Hidden", new Definition { name = "Hidden", Root = ProductRoot + "/Domain/Hidden" });
            Assert.That(Validate(snapshot), Does.Contain("unexpected-assembly:Hidden"));
            snapshot.AssemblyReferencePaths = new[] { ProductRoot + "/Domain/Bypass.asmref" };
            Assert.That(Validate(snapshot), Does.Contain("assembly-reference-asset:" + ProductRoot + "/Domain/Bypass.asmref"));
        }

        [Test]
        public void RuntimeAssembly_RejectsEditorDependencyAndEditorOnlyPlatform()
        {
            var snapshot = Capture();
            var presentation = snapshot.Definitions["ChooGuard.Presentation"];
            presentation.references = presentation.references.Concat(new[] { "ChooGuard.Editor" }).ToArray();
            presentation.includePlatforms = new[] { "Editor" };
            var errors = Validate(snapshot);
            Assert.That(errors, Does.Contain("editor-reference:ChooGuard.Presentation:ChooGuard.Editor"));
            Assert.That(errors, Does.Contain("platform-boundary:ChooGuard.Presentation"));
        }

        [TestCase("ChooGuard.Presentation", "Assembly-CSharp.dll", "default-reference")]
        [TestCase("ChooGuard.Presentation", "UnityEditor.CoreModule.dll", "editor-reference")]
        [TestCase(PlayModeName, "ChooGuard.Editor.dll", "editor-reference")]
        public void PrecompiledReference_RejectsDefaultAndEditorBoundaryBypass(string name, string reference, string error)
        {
            var snapshot = Capture();
            snapshot.Definitions[name].precompiledReferences = new[] { reference };
            Assert.That(Validate(snapshot), Does.Contain(error + ":" + name + ":" + reference));
        }

        [TestCase("ChooGuard.Editor")]
        [TestCase(EditModeName)]
        public void PlayModeAssembly_RejectsDirectEditorAssemblyReference(string reference)
        {
            var snapshot = Capture();
            var definition = snapshot.Definitions[PlayModeName];
            definition.references = definition.references.Concat(new[] { reference }).ToArray();
            Assert.That(Validate(snapshot), Does.Contain("editor-reference:" + PlayModeName + ":" + reference));
        }

        [TestCase("ChooGuard.Content")]
        [TestCase("ChooGuard.Application")]
        [TestCase("ChooGuard.Domain")]
        [TestCase("ChooGuard.World")]
        [TestCase("ChooGuard.Persistence")]
        [TestCase(null)]
        public void EditModeAssembly_RejectsMissingOrUnexpectedProductionReference(string missingReference)
        {
            var snapshot = Capture();
            var definition = snapshot.Definitions[EditModeName];
            // 제출 파일을 바꾸지 않고 필수 참조 누락과 미허용 Simulation 참조를 검사한다.
            definition.references = new[] { "ChooGuard.Contracts", "ChooGuard.Presentation", "ChooGuard.Editor", "ChooGuard.Content", "ChooGuard.Application", "ChooGuard.Domain", "ChooGuard.World", "ChooGuard.Persistence" }
                .Concat(UiReferences).Concat(RenderReferences).ToArray();
            Assert.That(Validate(snapshot), Does.Not.Contain("references:" + EditModeName));
            definition.references = missingReference != null
                ? definition.references.Where(reference => reference != missingReference).ToArray()
                : definition.references.Concat(new[] { "ChooGuard.Simulation" }).ToArray();
            Assert.That(Validate(snapshot), Does.Contain("references:" + EditModeName));
        }

        [Test]
        public void EditorAndTestPlatforms_RejectLostBoundaries()
        {
            var snapshot = Capture();
            snapshot.Definitions["ChooGuard.Editor"].includePlatforms = Array.Empty<string>();
            snapshot.Definitions[EditModeName].includePlatforms = Array.Empty<string>();
            snapshot.Definitions[PlayModeName].includePlatforms = new[] { "Editor" };
            var errors = Validate(snapshot);
            Assert.That(errors, Does.Contain("platform-boundary:ChooGuard.Editor"));
            Assert.That(errors, Does.Contain("platform-boundary:" + EditModeName));
            Assert.That(errors, Does.Contain("platform-boundary:" + PlayModeName));
        }

        private static Snapshot Capture()
        {
            var contract = JsonUtility.FromJson<Layout>(File.ReadAllText(Path.Combine(ProjectRoot, ContractPath)));
            var snapshot = new Snapshot { Modules = contract.modules };
            foreach (var path in Directory.GetFiles(Path.Combine(ProjectRoot, ProductRoot), "*.asmdef", SearchOption.AllDirectories))
            {
                var definition = JsonUtility.FromJson<Definition>(File.ReadAllText(path));
                definition.Path = Relative(path);
                definition.Root = Relative(Path.GetDirectoryName(path));
                snapshot.Definitions.Add(definition.name, definition);
            }
            foreach (var path in Directory.GetFiles(Path.Combine(ProjectRoot, ProductRoot), "*.cs", SearchOption.AllDirectories))
                snapshot.Sources.Add(Relative(path), File.ReadAllText(path));
            snapshot.AssemblyReferencePaths = Directory.GetFiles(Path.Combine(ProjectRoot, ProductRoot), "*.asmref", SearchOption.AllDirectories)
                .Select(Relative).ToArray();
            return snapshot;
        }

        // This same inspection handles the submitted tree and isolated in-memory mutations.
        // It never writes invalid C# or assets into the project to trigger reimports.
        private static List<string> Validate(Snapshot snapshot)
        {
            var errors = new List<string>();
            var expectedNames = new HashSet<string>(snapshot.Modules.Select(module => module.name)) { EditModeName, PlayModeName };
            foreach (var name in snapshot.Definitions.Keys.Where(name => !expectedNames.Contains(name)))
                errors.Add("unexpected-assembly:" + name);
            foreach (var module in snapshot.Modules)
            {
                if (!snapshot.Definitions.TryGetValue(module.name, out var definition))
                {
                    errors.Add("missing-assembly:" + module.name);
                    continue;
                }
                if (definition.Path != module.path || definition.Root != module.root) errors.Add("assembly-location:" + module.name);
                if (definition.noEngineReferences == module.engineReferences) errors.Add("engine-boundary:" + module.name);
                if (!module.engineReferences && (!definition.overrideReferences || Values(definition.precompiledReferences).Length != 0))
                    errors.Add("plugin-boundary:" + module.name);
                var external = module.name == "ChooGuard.Presentation" ? UiReferences :
                    module.editorOnly ? UiReferences.Concat(RenderReferences).ToArray() : Array.Empty<string>();
                CheckReferences(definition, module.references.Concat(external), errors);
                CheckPlatforms(definition, module.editorOnly, errors);
                if (Values(definition.defineConstraints).Length != 0 || Values(definition.optionalUnityReferences).Length != 0)
                    errors.Add("production-constraints:" + module.name);
                if (!module.engineReferences)
                    foreach (var reference in Values(definition.references).Where(IsForbiddenPureReference))
                        errors.Add("pure-reference:" + module.name + ":" + reference);
            }
            foreach (var name in new[] { EditModeName, PlayModeName })
            {
                if (!snapshot.Definitions.TryGetValue(name, out var definition))
                {
                    errors.Add("missing-assembly:" + name);
                    continue;
                }
                var editMode = name == EditModeName;
                var expectedRoot = ProductRoot + "/Tests/" + (editMode ? "EditMode" : "PlayMode");
                if (definition.Root != expectedRoot || definition.Path != expectedRoot + "/" + name + ".asmdef")
                    errors.Add("assembly-location:" + name);
                var references = new[] { "ChooGuard.Contracts", "ChooGuard.Presentation" }.Concat(UiReferences);
                if (editMode) references = references.Concat(new[] { "ChooGuard.Editor", "ChooGuard.Content", "ChooGuard.Application", "ChooGuard.Domain", "ChooGuard.World", "ChooGuard.Persistence" }).Concat(RenderReferences);
                CheckReferences(definition, references, errors);
                CheckPlatforms(definition, editMode, errors);
                if (definition.autoReferenced || definition.noEngineReferences ||
                    !Values(definition.defineConstraints).SequenceEqual(new[] { "UNITY_INCLUDE_TESTS" }) ||
                    !Values(definition.optionalUnityReferences).SequenceEqual(new[] { "TestAssemblies" }))
                    errors.Add("test-boundary:" + name);
            }
            var editorNames = new HashSet<string>(snapshot.Modules.Where(module => module.editorOnly).Select(module => module.name)) { EditModeName };
            foreach (var definition in snapshot.Definitions.Values)
            {
                foreach (var reference in Values(definition.references).Concat(Values(definition.precompiledReferences)))
                {
                    if (reference.StartsWith("Assembly-CSharp", StringComparison.Ordinal))
                        errors.Add("default-reference:" + definition.name + ":" + reference);
                    if (!editorNames.Contains(definition.name) &&
                        (reference.StartsWith("UnityEditor", StringComparison.Ordinal) || editorNames.Contains(reference) || reference == "ChooGuard.Editor.dll"))
                        errors.Add("editor-reference:" + definition.name + ":" + reference);
                }
            }
            foreach (var path in snapshot.AssemblyReferencePaths) errors.Add("assembly-reference-asset:" + path);
            foreach (var source in snapshot.Sources)
            {
                var owner = FindOwner(source.Key, snapshot.Definitions.Values);
                if (owner == null) { errors.Add("unowned-source:" + source.Key); continue; }
                if (owner.name == EditModeName || owner.name == PlayModeName) continue;
                var code = Regex.Replace(source.Value, @"//[^\r\n]*|/\*[\s\S]*?\*/|@""(?:""""|[^""])*""|""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'", " ");
                if (owner.noEngineReferences && Regex.IsMatch(code, @"\b(?:UnityEngine|UnityEditor)\b"))
                    errors.Add("pure-source:" + source.Key);
                if (!Values(owner.includePlatforms).SequenceEqual(new[] { "Editor" }) && Regex.IsMatch(code, @"\bUnityEditor\b"))
                    errors.Add("editor-source:" + source.Key);
            }
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();
            foreach (var name in snapshot.Definitions.Keys.OrderBy(name => name, StringComparer.Ordinal))
                Visit(name, snapshot.Definitions, visited, visiting, errors);
            return errors;
        }

        private static void CheckReferences(Definition definition, IEnumerable<string> expected, List<string> errors)
        {
            if (!Values(definition.references).OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(expected.OrderBy(value => value, StringComparer.Ordinal)))
                errors.Add("references:" + definition.name);
        }

        private static void CheckPlatforms(Definition definition, bool editorOnly, List<string> errors)
        {
            if (!Values(definition.includePlatforms).SequenceEqual(editorOnly ? new[] { "Editor" } : Array.Empty<string>()) ||
                Values(definition.excludePlatforms).Length != 0)
                errors.Add("platform-boundary:" + definition.name);
        }

        private static void Visit(string name, Dictionary<string, Definition> definitions, HashSet<string> visited,
            HashSet<string> visiting, List<string> errors)
        {
            if (visiting.Contains(name)) { errors.Add("cycle:" + name); return; }
            if (!visited.Add(name)) return;
            visiting.Add(name);
            foreach (var reference in Values(definitions[name].references).Where(definitions.ContainsKey))
                Visit(reference, definitions, visited, visiting, errors);
            visiting.Remove(name);
        }

        private static Definition FindOwner(string path, IEnumerable<Definition> definitions) =>
            definitions.Where(definition => IsWithin(path, definition.Root)).OrderByDescending(definition => definition.Root.Length).FirstOrDefault();
        private static bool IsWithin(string path, string root) => path.StartsWith(root + "/", StringComparison.Ordinal);
        private static string Relative(string path) => path.Substring(ProjectRoot.Length + 1).Replace('\\', '/');
        private static string[] Values(string[] values) => values ?? Array.Empty<string>();
        private static bool IsForbiddenPureReference(string reference) =>
            reference.StartsWith("UnityEngine", StringComparison.Ordinal) || reference.StartsWith("UnityEditor", StringComparison.Ordinal) ||
            reference.StartsWith("Assembly-CSharp", StringComparison.Ordinal);

        [Serializable]
        private sealed class Layout { public Module[] modules; }
        [Serializable]
        private sealed class Module
        {
            public string name;
            public string root;
            public string path;
            public string[] references;
            public bool engineReferences;
            public bool editorOnly;
        }
        [Serializable]
        private sealed class Definition
        {
            public string name;
            public string[] references;
            public string[] includePlatforms;
            public string[] excludePlatforms;
            public string[] precompiledReferences;
            public string[] defineConstraints;
            public string[] optionalUnityReferences;
            public bool noEngineReferences;
            public bool overrideReferences;
            public bool autoReferenced;
            [NonSerialized] public string Path;
            [NonSerialized] public string Root;
        }
        private sealed class Snapshot
        {
            public Module[] Modules;
            public readonly Dictionary<string, Definition> Definitions = new Dictionary<string, Definition>(StringComparer.Ordinal);
            public readonly Dictionary<string, string> Sources = new Dictionary<string, string>(StringComparer.Ordinal);
            public string[] AssemblyReferencePaths = Array.Empty<string>();
        }
    }
}
#endif
