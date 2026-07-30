# Container Build Discipline

**Source:** the retired Conventions/BuildAndEnvironment.md (removed 2026-07-30)

This article applies only to LLM agents building inside the Docker container. Dan builds from Rider on the host and is unaffected.

The working tree of this repo is shared between Dan (in the host, using Rider) and LLM agents running in a Docker container that mounts the host's path.

## The rule

When running `dotnet build` from inside the container, always use:

```
dotnet build --artifacts-path /tmp/sonofleo-build
```

A bare `dotnet build` writes container paths into `obj/` on the bind mount and breaks Rider on the host.
