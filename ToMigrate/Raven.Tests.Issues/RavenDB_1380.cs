// -----------------------------------------------------------------------
//  <copyright file="RavenDB_1380.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------
using Raven.Tests.Common;
using Raven.Tests.Helpers.Util;

namespace Raven.Tests.Issues
{
    using Raven.Abstractions.Data;
    using Raven.Json.Linq;

    using Xunit;

    public class RavenDB_1380 : RavenTest
    {
        [Fact]
        public void PatchingShouldBeDisabledForDocumentsWithDeleteMarkerWhenReplicationIsTurnedOn()
        {
            using (var store = NewDocumentStore())
            {
                store.DatabaseCommands.Put(
                    "docs/1", null, new RavenJObject(), new RavenJObject
                                                         {
                                                             { Constants.RavenDeleteMarker, "true" }
                                                         });

                store.DatabaseCommands.Put(
                    "docs/2", null, new RavenJObject(), new RavenJObject());

                var result = store.SystemDatabase.Patches.ApplyPatch("docs/1", null, new ScriptedPatchRequest { Script = "" });
                Assert.Equal(PatchResult.DocumentDoesNotExists, result.Item1.PatchResult);

                result = store.SystemDatabase.Patches.ApplyPatch("docs/2", null, new ScriptedPatchRequest { Script = @"this[""Test""] = 999;" });
                Assert.Equal(PatchResult.Patched, result.Item1.PatchResult);

                Assert.Equal(999, store.SystemDatabase.Documents.Get("docs/2").DataAsJson.Value<int>("Test"));
            }
        }

        protected override void ModifyConfiguration(ConfigurationModification configuration)
        {
            configuration.Modify(x => x.Core._ActiveBundlesString, "replication");
        }
    }
}