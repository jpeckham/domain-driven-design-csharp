# Visual Studio Docker Compose Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a conventional Visual Studio Docker Compose project to `SocialDDD.sln`.

**Architecture:** The root `docker-compose.yml` remains the Compose source of truth. A new root `docker-compose.dcproj` wraps that file for Visual Studio's Docker tooling and points F5 browser launch at the `client` service.

**Tech Stack:** Visual Studio 17, `Microsoft.Docker.Sdk`, Docker Compose, .NET 9.

---

### Task 1: Add Docker Compose Project

**Files:**
- Create: `docker-compose.dcproj`

**Step 1: Create the project file**

Add a `Microsoft.Docker.Sdk` project with `DockerTargetOS` set to `Linux`, `DockerServiceName` set to `client`, and `DockerServiceUrl` set to `http://localhost:5200`.

**Step 2: Include existing Docker assets**

Reference `docker-compose.yml`, `.dockerignore`, `Dockerfile.api`, and `Dockerfile.client` as `None` items so they appear under the Visual Studio compose project.

### Task 2: Add Project To Solution

**Files:**
- Modify: `SocialDDD.sln`

**Step 1: Add project entry**

Add `docker-compose.dcproj` to the solution using Visual Studio's Docker Compose project type GUID.

**Step 2: Add solution configurations**

Add `ActiveCfg` and `Build.0` entries for every existing solution configuration.

### Task 3: Verify

**Files:**
- Read: `SocialDDD.sln`
- Read: `docker-compose.dcproj`

**Step 1: Build the solution**

Run: `dotnet build SocialDDD.sln`

Expected: solution restore and build complete successfully.

**Step 2: Inspect git diff**

Run: `git diff -- docker-compose.dcproj SocialDDD.sln docs/plans/2026-05-19-visual-studio-docker-compose-design.md docs/plans/2026-05-19-visual-studio-docker-compose.md`

Expected: only the Docker Compose project, solution metadata, and plan docs changed.
