# Build & Environment

- **Building in the BD container:** always `dotnet build --artifacts-path /tmp/sonofleo-build`. A bare `dotnet build` writes container paths into `obj/` on the bind mount and breaks Rider on the host.
