using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Abstractions;
using OmniSharp.Extensions.LanguageServer.Capabilities.Client;
using OmniSharp.Extensions.LanguageServer.Capabilities.Server;
using OmniSharp.Extensions.LanguageServer.Models;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.FileClose;
using OmniSharp.Models.FileOpen;
using OmniSharp.Models.UpdateBuffer;
using OmniSharp.Roslyn;

namespace OmniSharp.LanguageServerProtocol.Handlers
{
    [Shared, Export(typeof(TextDocumentSyncHandler))]
    class TextDocumentSyncHandler : ITextDocumentSyncHandler, IWillSaveTextDocumentHandler, IWillSaveWaitUntilTextDocumentHandler
    {
        public static IEnumerable<IJsonRpcHandler> Enumerate(
            RequestHandlers handlers,
            ILoggerFactory loggerFactory,
            OmniSharpWorkspace workspace)
        {
            foreach (var (selector, openHandler, closeHandler, bufferHandler) in handlers
                .OfType<
                    Mef.IRequestHandler<FileOpenRequest, FileOpenResponse>,
                    Mef.IRequestHandler<FileCloseRequest, FileCloseResponse>,
                    Mef.IRequestHandler<UpdateBufferRequest, object>>())
            {
                var logger = loggerFactory.CreateLogger<TextDocumentSyncHandler>();
                logger.LogDebug("openHandler: {openHandler}", openHandler);
                logger.LogDebug("closeHandler: {closeHandler}", closeHandler);
                logger.LogDebug("bufferHandler: {bufferHandler}", bufferHandler);
                logger.LogDebug("selector: {selector}", (string)selector);
                // TODO: Fix once cake has working support for incremental
                var documentSyncKind = openHandler == null || closeHandler == null ? TextDocumentSyncKind.Full : TextDocumentSyncKind.Incremental;
                // if (selector.ToString().IndexOf(".cake") > -1) documentSyncKind = TextDocumentSyncKind.Full;
                yield return new TextDocumentSyncHandler(openHandler, closeHandler, bufferHandler, selector, documentSyncKind, workspace, logger);
            }
        }

        // TODO Make this configurable?
        private readonly DocumentSelector _documentSelector;
        private SynchronizationCapability _capability;
        private readonly Mef.IRequestHandler<FileOpenRequest, FileOpenResponse> _openHandler;
        private readonly Mef.IRequestHandler<FileCloseRequest, FileCloseResponse> _closeHandler;
        private readonly Mef.IRequestHandler<UpdateBufferRequest, object> _bufferHandler;
        private readonly OmniSharpWorkspace _workspace;
        private readonly ILogger<TextDocumentSyncHandler> _logger;

        [ImportingConstructor]
        public TextDocumentSyncHandler(
            Mef.IRequestHandler<FileOpenRequest, FileOpenResponse> openHandler,
            Mef.IRequestHandler<FileCloseRequest, FileCloseResponse> closeHandler,
            Mef.IRequestHandler<UpdateBufferRequest, object> bufferHandler,
            DocumentSelector documentSelector,
            TextDocumentSyncKind documentSyncKind,
            OmniSharpWorkspace workspace,
            ILogger<TextDocumentSyncHandler> logger)
        {
            _openHandler = openHandler;
            _closeHandler = closeHandler;
            _bufferHandler = bufferHandler;
            _workspace = workspace;
            _logger = logger;
            _documentSelector = documentSelector;
            Options.Change = documentSyncKind;
        }

        public TextDocumentSyncOptions Options { get; } = new TextDocumentSyncOptions()
        {
            Change = TextDocumentSyncKind.Incremental,
            OpenClose = true,
            WillSave = true, // Do we need to configure this?
            WillSaveWaitUntil = true,  // Do we need to configure this?
            Save = new SaveOptions()
            {
                IncludeText = true
            }
        };

        public TextDocumentAttributes GetTextDocumentAttributes(Uri uri)
        {
            var document = _workspace.GetDocument(Helpers.FromUri(uri));
            if (document == null) return new TextDocumentAttributes(uri, "");
            return new TextDocumentAttributes(uri, "");
        }

        public Task Handle(DidChangeTextDocumentParams notification)
        {
            if (notification.ContentChanges.Count() == 1 && notification.ContentChanges.First().Range == null)
            {
                _logger.LogDebug("_bufferHandler: {_bufferHandler} - _documentSelector: {_documentSelector}", _bufferHandler, (string)_documentSelector);
                _logger.LogDebug("Received {Mode} {Request}", TextDocumentSyncKind.Full, notification.ContentChanges);
                var change = notification.ContentChanges.First();
                return _bufferHandler.Handle(new UpdateBufferRequest()
                {
                    FileName = Helpers.FromUri(notification.TextDocument.Uri),
                    Buffer = change.Text
                });
            }

            _logger.LogDebug("Received {Mode} {Request}", TextDocumentSyncKind.Incremental, notification.ContentChanges);
            var changes = notification.ContentChanges
                .Select(change => new LinePositionSpanTextChange()
                {
                    NewText = change.Text,
                    StartColumn = Convert.ToInt32(change.Range.Start.Character),
                    StartLine = Convert.ToInt32(change.Range.Start.Line),
                    EndColumn = Convert.ToInt32(change.Range.End.Character),
                    EndLine = Convert.ToInt32(change.Range.End.Line),
                })
                .ToArray();

            return _bufferHandler.Handle(new UpdateBufferRequest()
            {
                FileName = Helpers.FromUri(notification.TextDocument.Uri),
                Changes = changes
            });
        }

        public Task Handle(DidOpenTextDocumentParams notification)
        {
            return _openHandler?.Handle(new FileOpenRequest()
            {
                Buffer = notification.TextDocument.Text,
                FileName = Helpers.FromUri(notification.TextDocument.Uri)
            }) ?? Task.CompletedTask;
        }

        public Task Handle(DidCloseTextDocumentParams notification)
        {
            return _closeHandler?.Handle(new FileCloseRequest()
            {
                FileName = Helpers.FromUri(notification.TextDocument.Uri)
            }) ?? Task.CompletedTask;
        }

        public Task Handle(DidSaveTextDocumentParams notification)
        {
            if (_capability?.DidSave == true)
            {
                return _bufferHandler.Handle(new UpdateBufferRequest()
                {
                    FileName = Helpers.FromUri(notification.TextDocument.Uri),
                    Buffer = notification.Text
                });
            }
            return Task.CompletedTask;
        }

        public Task Handle(WillSaveTextDocumentParams notification)
        {
            if (_capability?.WillSave == true)
            {

            }
            return Task.CompletedTask;
        }

        public Task Handle(WillSaveTextDocumentParams request, CancellationToken token)
        {
            if (_capability?.WillSaveWaitUntil == true)
            {

            }
            return Task.CompletedTask;
        }

        public void SetCapability(SynchronizationCapability capability)
        {
            _capability = capability;
        }

        TextDocumentChangeRegistrationOptions IRegistration<TextDocumentChangeRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentChangeRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                SyncKind = Options.Change
            };
        }

        TextDocumentRegistrationOptions IRegistration<TextDocumentRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
            };
        }

        TextDocumentSaveRegistrationOptions IRegistration<TextDocumentSaveRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentSaveRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                IncludeText = true
            };
        }
    }
}
