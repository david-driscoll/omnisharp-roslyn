using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("highlight")]
        public async Task<HighlightResponse> Highlight(HighlightRequest request)
        {
            var documents = _workspace.GetDocuments(request.FileName);
            if (request.ProjectNames != null && request.ProjectNames.Length > 0)
            {
                documents = documents.Where(d => request.ProjectNames.Contains(d.Project.Name, StringComparer.Ordinal));
            }

            if (request.Classifications == null || request.Classifications.Length > 0)
            {
                request.Classifications = AllClassifications;
            }

            if (request.ExcludeClassifications != null && request.ExcludeClassifications.Length > 0)
            {
                request.Classifications = request.Classifications.Except(request.ExcludeClassifications).ToArray();
            }

            var results = new List<ClassifiedResult>();

            foreach (var document in documents)
            {
                var project = document.Project.Name;
                var text = await document.GetTextAsync();
                var spans = new List<ClassifiedSpan>();

                if (request.Lines == null || request.Lines.Length == 0)
                {
                    foreach (var span in await Classifier.GetClassifiedSpansAsync(document, new TextSpan(0, text.Length)))
                    {
                        spans.Add(span);
                    }
                }
                else
                {
                    foreach (var line in request.Lines.Where(z => z <= text.Lines.Count))
                    {
                        foreach (var span in await Classifier.GetClassifiedSpansAsync(document, text.Lines[line - 1].Span))
                        {
                            spans.Add(span);
                        }
                    }
                }

                results.AddRange(FilterSpans(request.Classifications, spans)
                    .Select(span => new ClassifiedResult()
                    {
                        Span = span,
                        Lines = text.Lines,
                        Project = project
                    }));
            }

            return new HighlightResponse()
            {
                Highlights = results
                    .GroupBy(result => result.Span.TextSpan.ToString())
                    .Select(grouping => HighlightSpan.FromClassifiedSpan(grouping.First().Span, grouping.First().Lines, grouping.Select(z => z.Project)))
                    .ToArray()
            };
        }

        class ClassifiedResult
        {
            public ClassifiedSpan Span { get; set; }
            public TextLineCollection Lines { get; set; }
            public string Project { get; set; }
        }

        private HighlightClassification[] AllClassifications = Enum.GetValues(typeof(HighlightClassification)).Cast<HighlightClassification>().ToArray();

        private IEnumerable<ClassifiedSpan> FilterSpans(HighlightClassification[] classifications, IEnumerable<ClassifiedSpan> spans)
        {
            foreach (var classification in AllClassifications.Except(classifications))
            {
                if (classification == HighlightClassification.Name)
                    spans = spans.Where(x => !x.ClassificationType.EndsWith(" name"));
                else if (classification == HighlightClassification.Comment)
                    spans = spans.Where(x => x.ClassificationType != "comment" && !x.ClassificationType.StartsWith("xml doc comment "));
                else if (classification == HighlightClassification.String)
                    spans = spans.Where(x => x.ClassificationType != "string" && !x.ClassificationType.StartsWith("string "));
                else if (classification == HighlightClassification.PreprocessorKeyword)
                    spans = spans.Where(x => x.ClassificationType != "preprocessor keyword");
                else if (classification == HighlightClassification.ExcludedCode)
                    spans = spans.Where(x => x.ClassificationType != "excluded code");
                else
                    spans = spans.Where(x => x.ClassificationType != Enum.GetName(typeof(HighlightClassification), classification).ToLower());
            }

            return spans;
        }
    }
}
