﻿//-----------------------------------------------------------------------
// <copyright file="AdvancedJsonPatcher.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Raven.Json.Linq;
using System.Reflection;
using System.IO;
using IronJS.Hosting;
using Newtonsoft.Json;
using Raven.Abstractions.Data;
using Raven.Abstractions.Exceptions;
using IronJS;

namespace Raven.Database.Json
{	
	public class AdvancedJsonPatcher
	{
		private RavenJObject document;
		private RavenJObject [] documents;
		private bool batchApply = false;
				
		public AdvancedJsonPatcher(RavenJObject document)
		{
			this.document = document;
		}

		public AdvancedJsonPatcher(RavenJObject [] documents)
		{
			this.documents = documents;
			this.batchApply = true;
		}

		public RavenJObject Apply(AdvancedPatchRequest patch)
		{			
            EnsurePreviousValueMatchCurrentValue(patch, document);
            if (document == null)
                return document;

            if (String.IsNullOrEmpty(patch.Script))
                throw new InvalidOperationException("Patch script must be non-null and not empty");

			ApplyImpl(patch.Script);
			return document;
		}

		private void ApplyImpl(string script)
		{
			var ctx = new CSharp.Context();
			ctx.CreatePrintFunction();
			
			var toJsonScript = GetFromResources("Raven.Database.Json.ToJson.js");
			ctx.Execute(toJsonScript);

			var mapScript = GetFromResources("Raven.Database.Json.Map.js");
			ctx.Execute(mapScript);

			if (batchApply)
			{
				//foreach (var doc in documents)
				//{
				//    ApplySingleScript(document, script, ctx);
				//}

				//Things to consider, if we get one failed do we stop the whole batch,
				//or do we stop it when we reach a threshold, i.e. 10% failures?
				//Where do failures get logged to, or do they just throw exceptions??
			}
			else
			{
				var resultDocument = ApplySingleScript(ctx, document, script);
				if (resultDocument != null)
					document = resultDocument;
			}
		}

		private RavenJObject ApplySingleScript(CSharp.Context ctx, RavenJObject doc, string script)
		{
			var wrapperScript = String.Format(@"
var doc = {0};

(function(doc){{
	{1}{2}
}}).apply(doc);

json_data = JSON.stringify(doc);", doc, script, script.EndsWith(";") ? String.Empty : ";");

			object result;
			try
			{
				result = ctx.Execute(wrapperScript);
				return RavenJObject.Parse(ctx.GetGlobalAs<string>("json_data"));
			}
			catch (UserError uEx)
			{
                throw;
			}
			catch (Error.Error errorEx)
			{
                throw;
			}			
			return null;
		}

		private string GetFromResources(string resourceName)
		{
			Assembly assem = this.GetType().Assembly;
			using (Stream stream = assem.GetManifestResourceStream(resourceName))
			{
				using (var reader = new StreamReader(stream))
				{
					return reader.ReadToEnd();
				}
			}
		}

        private static void EnsurePreviousValueMatchCurrentValue(AdvancedPatchRequest patchCmd, RavenJObject document)
        {
            var prevVal = patchCmd.PrevVal;
            if (prevVal == null)
                return;
            switch (prevVal.Type)
            {
                //case JsonToken.Undefined
                //    if (document != null)
                //        throw new ConcurrencyException();
                //    break;
                default:
                    if (document == null)
                        throw new ConcurrencyException();
                    if (RavenJObject.DeepEquals(document, prevVal) == false)
                        throw new ConcurrencyException();
                    break;
            }
        }
	}
}
