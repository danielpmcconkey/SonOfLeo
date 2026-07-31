namespace Tests.Integrated

open Tests.Helpers
open Xunit

[<CollectionDefinition("SharedTestData")>]
type SharedTestDataCollection() =
    interface ICollectionFixture<TestDataFixture>
