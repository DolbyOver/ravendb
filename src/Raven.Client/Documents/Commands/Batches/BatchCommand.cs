﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Raven.Client.Documents.Conventions;
using Raven.Client.Http;
using Raven.Client.Json;
using Raven.Client.Json.Converters;
using Sparrow.Json;

namespace Raven.Client.Documents.Commands.Batches
{
    public class BatchCommand : RavenCommand<BlittableArrayResult>, IDisposable
    {
        private readonly JsonOperationContext _context;
        private readonly BlittableJsonReaderObject[] _commands;
        private readonly BatchOptions _options;

        public BatchCommand(DocumentConventions conventions, JsonOperationContext context, List<ICommandData> commands, BatchOptions options = null)
        {
            if (conventions == null)
                throw new ArgumentNullException(nameof(conventions));
            if (commands == null)
                throw new ArgumentNullException(nameof(commands));

            _context = context ?? throw new ArgumentNullException(nameof(context));

            _commands = new BlittableJsonReaderObject[commands.Count];
            for (var i = 0; i < commands.Count; i++)
            {
                var command = commands[i];
                _commands[i] = _context.ReadObject(command.ToJson(conventions, context), "command");
            }

            _options = options;

            Timeout = options?.RequestTimeout;
        }

        public override HttpRequestMessage CreateRequest(ServerNode node, out string url)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                Content = new BlittableJsonContent(stream =>
                {
                    using (var writer = new BlittableJsonTextWriter(_context, stream))
                    {
                        writer.WriteStartArray();
                        var first = true;
                        foreach (var command in _commands)
                        {
                            if (first == false)
                                writer.WriteComma();
                            first = false;

                            writer.WriteObject(command);
                        }
                        writer.WriteEndArray();
                    }
                })
            };

            var sb = new StringBuilder($"{node.Url}/databases/{node.Database}/bulk_docs");

            AppendOptions(sb);

            url = sb.ToString();

            return request;
        }

        public override void SetResponse(BlittableJsonReaderObject response, bool fromCache)
        {
            if (response == null)
                throw new InvalidOperationException("Got null response from the server after doing a batch, something is very wrong. Probably a garbled response.");

            Result = JsonDeserializationClient.BlittableArrayResult(response);
        }

        private void AppendOptions(StringBuilder sb)
        {
            if (_options == null)
                return;

            sb.AppendLine("?");

            if (_options.WaitForReplicas)
            {
                sb.Append("&waitForReplicasTimeout=").Append(_options.WaitForReplicasTimeout);
                if (_options.ThrowOnTimeoutInWaitForReplicas)
                {
                    sb.Append("&throwOnTimeoutInWaitForReplicas=true");
                }
                sb.Append("&numberOfReplicasToWaitFor=");

                sb.Append(_options.Majority
                    ? "majority"
                    : _options.NumberOfReplicasToWaitFor.ToString());
            }

            if (_options.WaitForIndexes)
            {
                sb.Append("&waitForIndexesTimeout=").Append(_options.WaitForIndexesTimeout);
                if (_options.ThrowOnTimeoutInWaitForIndexes)
                {
                    sb.Append("&waitForIndexThrow=true");
                }
                if (_options.WaitForSpecificIndexes != null)
                {
                    foreach (var specificIndex in _options.WaitForSpecificIndexes)
                    {
                        sb.Append("&waitForSpecificIndexs=").Append(specificIndex);
                    }
                }
            }
        }

        public override bool IsReadRequest => false;

        public void Dispose()
        {
            foreach (var command in _commands)
                command?.Dispose();
        }
    }
}