module Tests.Integrated.ConnectionLeakGuard

open System.Reflection
open Xunit.Sdk

(* REQ-DAL-2.4 on every path, not only the ones with a dedicated leak test: every integrated test must hand back
   every connection it rented. Collections run one at a time (xunit.runner.json), so nothing else holds a connection
   between Before and After, and a count above the baseline afterwards is this test's leak. *)
let mutable private baseline = 0L

type ConnectionLeakGuardAttribute() =
    inherit BeforeAfterTestAttribute()

    override _.Before(_: MethodInfo) =
        baseline <- ConnectionPool.connectionsInUse ()

    override _.After(methodUnderTest: MethodInfo) =
        let inUse = ConnectionPool.connectionsInUse ()
        if inUse <> baseline then
            failwith
                $"REQ-DAL-2.4 connection leak: {methodUnderTest.DeclaringType.Name}.{methodUnderTest.Name} started with {baseline} connections in use and ended with {inUse}"

[<assembly: ConnectionLeakGuard>]
do ()
