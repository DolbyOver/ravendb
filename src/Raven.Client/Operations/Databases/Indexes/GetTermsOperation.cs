﻿using System;
using System.Linq;
using System.Net.Http;
using Raven.Client.Commands;
using Raven.Client.Document;
using Raven.Client.Http;
using Raven.Client.Json;
using Sparrow.Json;

namespace Raven.Client.Operations.Databases.Indexes
{
    public class GetTermsOperation : IAdminOperation<string[]>
    {
        private readonly string _indexName;
        private readonly string _field;
        private readonly string _fromValue;
        private readonly int? _pageSize;

        public GetTermsOperation(string indexName, string field, string fromValue, int? pageSize = null)
        {
            if (indexName == null)
                throw new ArgumentNullException(nameof(indexName));
            if (field == null)
                throw new ArgumentNullException(nameof(field));

            _indexName = indexName;
            _field = field;
            _fromValue = fromValue;
            _pageSize = pageSize;
        }

        public RavenCommand<string[]> GetCommand(DocumentConventions conventions, JsonOperationContext context)
        {
            return new GetTermsCommand(_indexName, _field, _fromValue, _pageSize);
        }

        private class GetTermsCommand : RavenCommand<string[]>
        {
            private readonly string _indexName;
            private readonly string _field;
            private readonly string _fromValue;
            private readonly int? _pageSize;

            public GetTermsCommand(string indexName, string field, string fromValue, int? pageSize)
            {
                if (indexName == null)
                    throw new ArgumentNullException(nameof(indexName));
                if (field == null)
                    throw new ArgumentNullException(nameof(field));

                _indexName = indexName;
                _field = field;
                _fromValue = fromValue;
                _pageSize = pageSize;
            }

            public override HttpRequestMessage CreateRequest(ServerNode node, out string url)
            {
                url = $"{node.Url}/databases/{node.Database}/indexes/terms?name={Uri.EscapeUriString(_indexName)}&field={Uri.EscapeUriString(_field)}&fromValue={_fromValue}&pageSize={_pageSize}";

                return new HttpRequestMessage
                {
                    Method = HttpMethod.Get
                };
            }

            public override void SetResponse(BlittableJsonReaderObject response, bool fromCache)
            {
                if (response == null)
                    ThrowInvalidResponse();

                var result = JsonDeserializationClient.TermsQueryResult(response);
                var terms = result.Terms;

                Result = terms.ToArray();
            }

            public override bool IsReadRequest => true;
        }
    }
}