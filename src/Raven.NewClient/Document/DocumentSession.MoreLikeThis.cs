﻿//-----------------------------------------------------------------------
// <copyright file="DocumentSession.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Raven.NewClient.Client.Commands;
using Raven.NewClient.Client.Data.Queries;
using Raven.NewClient.Client.Indexes;

namespace Raven.NewClient.Client.Document
{
    /// <summary>
    /// Implements Unit of Work for accessing the RavenDB server
    /// </summary>
    public partial class DocumentSession
    {
        public List<T> MoreLikeThis<T, TIndexCreator>(string documentId) where TIndexCreator : AbstractIndexCreationTask, new()
        {
            if (documentId == null)
                throw new ArgumentNullException(nameof(documentId));

            var index = new TIndexCreator();
            return MoreLikeThis<T>(new MoreLikeThisQuery(Conventions) { IndexName = index.IndexName, DocumentId = documentId });
        }

        public List<T> MoreLikeThis<T, TIndexCreator>(MoreLikeThisQuery query) where TIndexCreator : AbstractIndexCreationTask, new()
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            var index = new TIndexCreator();
            query.IndexName = index.IndexName;
            return MoreLikeThis<T>(query);
        }

        public List<T> MoreLikeThis<TTransformer, T, TIndexCreator>(string documentId, Dictionary<string, object> transformerParameters = null) where TTransformer : AbstractTransformerCreationTask, new() where TIndexCreator : AbstractIndexCreationTask, new()
        {
            if (documentId == null)
                throw new ArgumentNullException(nameof(documentId));

            var index = new TIndexCreator();
            var transformer = new TTransformer();

            return MoreLikeThis<T>(new MoreLikeThisQuery(Conventions)
            {
                IndexName = index.IndexName,
                Transformer = transformer.TransformerName,
                TransformerParameters = transformerParameters
            });
        }

        public List<T> MoreLikeThis<TTransformer, T, TIndexCreator>(MoreLikeThisQuery query) where TTransformer : AbstractTransformerCreationTask, new() where TIndexCreator : AbstractIndexCreationTask, new()
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            var index = new TIndexCreator();
            var transformer = new TTransformer();

            query.IndexName = index.IndexName;
            query.Transformer = transformer.TransformerName;

            return MoreLikeThis<T>(query);
        }

        public List<T> MoreLikeThis<T>(string index, string documentId, string transformer = null, Dictionary<string, object> transformerParameters = null)
        {
            return MoreLikeThis<T>(new MoreLikeThisQuery(Conventions)
            {
                IndexName = index,
                DocumentId = documentId,
                Transformer = transformer,
                TransformerParameters = transformerParameters
            });
        }

        public List<T> MoreLikeThis<T>(MoreLikeThisQuery query)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            var operation = new MoreLikeThisOperation<T>(this, query);

            var command = operation.CreateRequest();
            RequestExecuter.Execute(command, Context);

            var result = command.Result;
            operation.SetResult(result);

            return operation.Complete<T>();
        }
    }
}