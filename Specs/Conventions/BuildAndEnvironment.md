# Build & Environment

As of this writing, there are 2 "environments" for this system: development and production. (The quotes around "environments" is here because, while the Docker container the agents run in is its own environment, Dan does his development activities in the host, which is where the production application and database lives. So it's really the vibe of environment rather than the isolation that is the reality of this system. We're in "dev" when Dan or BD are developing and we're in "prod" when Dan and Hobson are doing finance ops.)

The system must maintain entirely separate databases representing each environment.

Cross contamination of Entity data (see definitions) between environments is strictly prohibited.

Any layer of this system above the persistence layer must be explicitly aware of the environment it runs in and that will be managed through environment variables configured at the container or host.

The password for the production database must be distinct from the dev database password.

The container must never have access to the host's environment variables or secrets. 

Any executable configured to run in "debug" mode may NEVER access the production database. Read or write.

Only executables configured to run in "release" mode may access the production database. Read or write.

The working tree of this repo is shared between Dan (in the host) and LLM agents running in a Docker that mounts the host's path. Therefore, when running a dotnet build from inside the container, always run `dotnet build --artifacts-path /tmp/sonofleo-build`. A bare `dotnet build` writes container paths into `obj/` on the bind mount and breaks Rider on the host.



