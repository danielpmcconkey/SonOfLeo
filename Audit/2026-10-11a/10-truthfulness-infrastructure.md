# code-truthfulness-auditor: App.Utility / App.Operation / App.Session

## UTIL-JSON-1 — maintainability
- **Location:** /home/claude/SonOfLeo/Src/App.Utility/UtilityError.fs line 26 (JsonSerializationFailed message); raised from /home/claude/SonOfLeo/Src/App.Utility/Json.fs line 50 (Json.toJson)
- **Summary:** The JsonSerializationFailed error message describes the wrong operation. It says the system failed to turn a JSON string into a type, when the failure was turning a value into a JSON string.
- **Resolution:** fix-code

Json.toJson<'T> serializes a value of type 'T to a string. If that throws, it returns JsonSerializationFailed(typeof<'T>.ToString(), e.Message, e.StackTrace). UtilityError.ToMessage renders that case as `Failed to serialize JSON string into type {typeName}. ...`, which is word for word the deserialization message with 'de' removed. The text reads as if a JSON string was being parsed into {typeName}. In fact a {typeName} value was being written out as JSON. Every route returns its output through Json.toJson (for example the slice 2 routes in PositionsRoutes and the new NetWorthHistory report route), so any serialization failure reaches the operator with a message that points them in the wrong direction. The check-apperror-coverage output lists JsonSerializationFailed as untested, so no test pins the wording.

**Action:** Reword the JsonSerializationFailed case in UtilityError.ToMessage to something like `Failed to serialize a value of type {typeName} to JSON. {error}...`.

**Why:** An error message is the only thing the operator sees when a route fails. One that names the opposite direction of the failure sends whoever debugs it to look at input payload parsing instead of output serialization.

---


