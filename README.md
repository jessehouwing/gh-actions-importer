# GitHub Actions Importer

[![.github/workflows/ci.yml](https://github.com/github/gh-actions-importer/actions/workflows/ci.yml/badge.svg)](https://github.com/github/gh-actions-importer/actions/workflows/ci.yml)

[GitHub Actions Importer](https://docs.github.com/en/actions/migrating-to-github-actions/automating-migration-with-github-actions-importer) helps plan, test, and automate your migration to GitHub Actions from the following platforms:

- Azure DevOps
- Bamboo
- Bitbucket
- CircleCI
- GitLab
- Jenkins
- Travis CI

## How to request support

If you need assistance, you can file a support ticket [here](https://support.github.com).

## Getting started

GitHub Actions Importer is distributed as a container image and this extension to the official [GitHub CLI](https://cli.github.com) to interact with that container.

### Prerequisites

The following requirements must be met to be able to use the GitHub Actions Importer:

- A supported container CLI must be installed and available:
  - [Docker](https://docs.docker.com/get-docker/) with the Docker daemon running, or
  - [Podman](https://podman.io/getting-started/installation), or
  - `wslc` with WSL containers available.
- The official [GitHub CLI](https://cli.github.com) must be installed.
- You must have credentials to [authenticate](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry#authenticating-to-the-container-registry) with the GitHub Container Registry.

### Installation

Next, the GitHub Actions Importer CLI extension can be installed via this command:

```bash
gh extension install github/gh-actions-importer
```

### Configuration

New versions of the GitHub Actions Importer are released on a regular basis. To ensure you're up to date, run the following command:

```bash
gh actions-importer update
```

In order for GitHub Actions Importer to communicate with your current CI/CD server and GitHub, various credentials must be available for the command. These can be configured using environment variables or a `.env.local` file. These environment variables can be configured in an interactive prompt by running the following command:

```bash
$ gh actions-importer configure
? Enter value for 'GITHUB_ACCESS_TOKEN' (leave empty to skip):
...
```

You can find detailed information about using environment variables in the platform-specific documentation.

#### Using a custom container CLI

Docker is used by default. To use a different supported container CLI, set `CONTAINER_CLI` in your `.env.local` file.

```bash
# .env.local
CONTAINER_CLI=podman
```

Supported values are:

- `podman`
- `wslc`

When `CONTAINER_CLI=wslc` is configured, GitHub Actions Importer automatically avoids Docker-only flags that `wslc` doesn't support, including `docker run --network=host` and `docker pull --quiet`.

#### Using a custom Docker registry

We highly recommend using the [official GitHub Container Registry to pull the GitHub Actions Importer Docker image](https://github.com/actions-importer/preview/pkgs/container/cli/). However, if you need to use a custom Docker registry, you can configure GitHub Actions Importer to use a custom Docker registry by setting the `CONTAINER_REGISTRY` environment variable in your `.env.local` file.

```bash
# .env.local
CONTAINER_REGISTRY=my-custom-registry.com
```

#### Using a TLS-inspecting proxy and custom root certificates

If your network uses a TLS-inspecting proxy, the GitHub Actions Importer container must trust your organization's certificate authority (CA). Certificates trusted by the host are not automatically trusted inside the container.

Set `CONTAINER_ARGS` in the shell running `gh` to pass additional container run arguments. This is an environment variable, not a `gh actions-importer` command-line option. For Docker, `DOCKER_ARGS` is also supported as a fallback when `CONTAINER_ARGS` is unset.

Prepare a PEM CA bundle containing both the normal public root certificates and your organization's CA certificates. Mount it read-only and explicitly set `SSL_CERT_FILE` to its path **inside the container**:

**Bash (Docker):**

```bash
CONTAINER_ARGS='--volume "/absolute/path/ca-bundle.pem:/certs/ca-bundle.pem:ro" --env SSL_CERT_FILE=/certs/ca-bundle.pem' \
  gh actions-importer audit azure-devops --output-dir ./output
```

**PowerShell (Docker Desktop with Linux containers):**

```powershell
$env:CONTAINER_ARGS = '--volume "C:\certs\ca-bundle.pem:/certs/ca-bundle.pem:ro" --env SSL_CERT_FILE=/certs/ca-bundle.pem'
gh actions-importer audit azure-devops --output-dir ./output
```

Replace the host path with an existing bundle accessible to your container runtime, and use your usual importer command and credentials. If you already use `CONTAINER_ARGS`, combine these arguments with your existing settings rather than replacing them.

Keep the following in mind:

- `SSL_CERT_FILE` can replace a client's default CA bundle, so include public roots as well as your corporate CA. This setting applies to clients that honor it; other runtimes may require their own trust configuration. Merely mounting a certificate does not install it into the container's system trust store.
- Setting `SSL_CERT_FILE` on the host alone does not forward it into the container; use `--env` as shown above.
- If an explicit proxy is required, set `HTTP_PROXY`, `HTTPS_PROXY`, and `NO_PROXY` as appropriate in your shell. GitHub Actions Importer forwards these variables into the container.
- Configure proxy access and CA trust separately for the host tools and container runtime. These container run arguments do not affect image pulls, including `gh actions-importer update`.
- The internal feature-discovery container does not currently receive `CONTAINER_ARGS` or `DOCKER_ARGS`.
- Avoid `--no-ssl-verify` as a solution: it disables certificate verification instead of establishing trust.

### Documentation

Detailed information about how to use GitHub Actions Importer can be found in the [documentation](https://docs.github.com/en/actions/migrating-to-github-actions/automating-migration-with-github-actions-importer).

### Recordings

You can access recorded demos of GitHub Actions Importer performing migrations to Actions from the following CI/CD platforms:

- [Azure DevOps](https://youtu.be/gG-2bkmBRlI)
- [CircleCI](https://youtu.be/YkFnNEyM9Hg)
- [GitLab](https://youtu.be/3t5ywu0_qk4)
- [Jenkins](https://youtu.be/WqiGP6h4fa0)
- [Travis CI](https://youtu.be/ndc-FNa_X3c)

### Self-guided learning

The GitHub Actions Importer labs repository contains platform-specific learning paths that teach you how to use GitHub Actions Importer and how to approach migrations to GitHub Actions. To learn more, see the [GitHub Actions Importer labs repository](https://github.com/actions/importer-labs/tree/main#readme).

## Product roadmap

To learn about new features coming to GitHub Actions Importer, see the [GitHub Public Roadmap](https://github.com/orgs/github/projects/4247).

## How to offer feedback or make a feature request

If you would like to offer feedback or make a feature request, please create a new discussion [here](https://github.com/github/gh-actions-importer/discussions/new/choose).
