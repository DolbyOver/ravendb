﻿using FastTests;
using Raven.NewClient.Client.Data;
using Raven.NewClient.Client.Indexing;
using Raven.NewClient.Operations.Databases.Indexes;
using SlowTests.Utils;
using Xunit;

namespace SlowTests.Issues
{
    public class RavenDB_6278 : RavenNewTestBase
    {
        private class User
        {
            public string Id { get; set; }

            public string Name { get; set; }

            public string AddressId { get; set; }
        }

        [Fact]
        public void ShouldWork()
        {
            using (var store = GetDocumentStore())
            {
                store.Admin.Send(new PutIndexOperation("test", new IndexDefinition
                {
                    Maps =
                    {
                        "from user in docs.Users let address = LoadDocument(user.AddressId, \"Addresses\") select new { Name = user.Name, City = address.City }"
                    }
                }));

                using (var session = store.OpenSession())
                {
                    session.Store(new User
                    {
                        Name = "John",
                        AddressId = "abc"
                    });

                    session.SaveChanges();
                }

                WaitForIndexing(store);

                TestHelper.AssertNoIndexErrors(store);

                using (var commands = store.Commands())
                {
                    var result = commands.Query("test", new IndexQuery(store.Conventions), indexEntriesOnly: true);
                    Assert.Equal(1, result.Results.Length);
                }
            }
        }
    }
}