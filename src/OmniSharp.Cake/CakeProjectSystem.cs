using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Framework.ConfigurationModel;
using Microsoft.Framework.Logging;
using OmniSharp.Models.v1;
using OmniSharp.Services;
using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Scripting;
using Path = System.IO.Path;

namespace OmniSharp.Cake
{
    [Export(typeof(IProjectSystem))]
    public class CakeProjectSystem : IProjectSystem
    {
        private static readonly string BaseAssemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location);
        private readonly OmnisharpWorkspace _workspace;
        private readonly IOmnisharpEnvironment _env;
        private readonly CakeContext _cakeContext;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public CakeProjectSystem(OmnisharpWorkspace workspace, IOmnisharpEnvironment env, ILoggerFactory loggerFactory, CakeContext cakeContext)
        {
            _workspace = workspace;
            _env = env;
            _cakeContext = cakeContext;
            _logger = loggerFactory.CreateLogger<CakeProjectSystem>();
        }

        public string Key { get { return "Cake"; } }
        public string Language { get { return LanguageNames.CSharp; } }
        public IEnumerable<string> Extensions { get; } = new[] { ".cake" };

        public void Initalize(IConfiguration configuration)
        {
            _logger.LogInformation($"Detecting cake files in '{_env.Path}'.");

            var allCakeFiles = Directory.GetFiles(_env.Path, "*.cake", SearchOption.TopDirectoryOnly);

            if (allCakeFiles.Length == 0)
            {
                _logger.LogInformation("Could not find any cake files");
                return;
            }

            var buildFile = allCakeFiles.First();

            _cakeContext.Path = _env.Path;
            _logger.LogInformation($"Found {allCakeFiles.Length} cake files.");

            var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp6, DocumentationMode.Parse, SourceCodeKind.Script);

            var references = GetDefaultAssemblies();
            var namespaces = GetDefaultNamespaces();

            _cakeContext.CakeFiles.Add(buildFile);
            _cakeContext.Usings.UnionWith(namespaces);

            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, usings: namespaces.Distinct());

            var fileName = Path.GetFileName(buildFile);

            var projectId = ProjectId.CreateNewId(Guid.NewGuid().ToString());
            var project = ProjectInfo.Create(projectId, VersionStamp.Create(), fileName, $"{fileName}.dll", LanguageNames.CSharp, null, null,
                                                    compilationOptions, parseOptions, null, null, references, null, null, true, typeof(IScriptHost));

            _workspace.AddProject(project);
            AddFile(buildFile, projectId);

            _logger.LogInformation($"Succesfully processed {buildFile}.");
        }


        Task<object> IProjectSystem.GetProjectModel(string path)
        {
            return Task.FromResult<object>(null);
        }

        Task<object> IProjectSystem.GetInformationModel(WorkspaceInformationRequest request)
        {
            return Task.FromResult<object>(_cakeContext);
        }

        private void AddFile(string filePath, ProjectId projectId)
        {
            using (var stream = File.OpenRead(filePath))
            using (var reader = new StreamReader(stream))
            {
                var fileName = Path.GetFileName(filePath);
                var cakeFile = reader.ReadToEnd();

                var documentId = DocumentId.CreateNewId(projectId, fileName);
                var documentInfo = DocumentInfo.Create(documentId, fileName, null, SourceCodeKind.Script, null, filePath)
                    .WithSourceCodeKind(SourceCodeKind.Script)
                    .WithTextLoader(TextLoader.From(TextAndVersion.Create(SourceText.From(cakeFile), VersionStamp.Create())));
                _workspace.AddDocument(documentInfo);
            }
        }

        private static IEnumerable<MetadataReference> GetDefaultAssemblies()
        {
            var defaultAssemblies = new HashSet<Assembly>
            {
                typeof(Action).Assembly, // mscorlib
                typeof(Uri).Assembly, // System
                typeof(IQueryable).Assembly, // System.Core
                typeof(System.Xml.XmlReader).Assembly, // System.Xml
                typeof(System.Xml.Linq.XDocument).Assembly, // System.Xml.Linq
                typeof(ICakeContext).Assembly, // Cake.Core
                
            };
            return defaultAssemblies.Select(assembly => MetadataReference.CreateFromAssembly(assembly));
        }

        private static IEnumerable<string> GetDefaultNamespaces()
        {
            var defaultNamespaces = new HashSet<string>
            {
                "System", "System.Collections.Generic", "System.Linq",
                "System.Text", "System.Threading.Tasks", "System.IO",
                "Cake.Core", "Cake.Core.IO",
                "Cake.Core.Scripting", "Cake.Core.Diagnostics"
            };
            return defaultNamespaces;
        }
    }
}
