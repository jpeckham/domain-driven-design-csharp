# Visual Studio Docker Compose Project Design

## Goal

Make the existing Docker Compose setup visible to Visual Studio as a conventional Docker Compose project so a Visual Studio user can select it as the startup project and press F5.

## Approach

Add a root-level `docker-compose.dcproj` using `Microsoft.Docker.Sdk` and include the existing `docker-compose.yml`, `.dockerignore`, `Dockerfile.api`, and `Dockerfile.client`. Add the `.dcproj` to `SocialDDD.sln` with Visual Studio's Docker Compose project type GUID and solution build configuration entries.

## Tradeoffs

This keeps the current Compose file as the source of truth and avoids reshaping service definitions. Visual Studio may still generate its own intermediate debug compose files at run time, but the repository only needs the conventional project wrapper to integrate the existing compose stack into the solution.

## Testing

Validate that the solution loads/builds through `dotnet build SocialDDD.sln`. Visual Studio F5 behavior requires Visual Studio with Docker tooling installed, so local CLI validation covers solution/project compatibility rather than IDE launch behavior.
