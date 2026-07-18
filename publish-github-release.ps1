# Publishes a clean-history release snapshot to the public GitHub repo.
#
# Run this from the `github-release` branch. It pulls the current tracked tree
# from `main` (the GitLab dev branch), keeps this branch's own README.md and
# LICENSE untouched, commits the result as one squashed release commit, tags
# it, and pushes both to GitHub.
#
# Usage: powershell -ExecutionPolicy Bypass -File .\publish-github-release.ps1 -Version v1.1.0 -Message "v1.1.0"
param(
    [Parameter(Mandatory = $true)] [string]$Version,
    [string]$Message = $Version
)
$ErrorActionPreference = "Stop"

$currentBranch = git rev-parse --abbrev-ref HEAD
if ($currentBranch -ne "github-release") {
    throw "Switch to the github-release branch first (currently on '$currentBranch')."
}

$keep = @("README.md", "LICENSE", "publish-github-release.ps1")

# Bring every tracked file from main up to date on this branch, except the ones we keep as-is.
git ls-tree -r main --name-only | ForEach-Object {
    if ($keep -notcontains $_) {
        git checkout main -- $_
    }
}

# Remove anything this branch tracks that main no longer has (excluding kept files).
$mainFiles = git ls-tree -r main --name-only
git ls-tree -r HEAD --name-only | ForEach-Object {
    if (($keep -notcontains $_) -and ($mainFiles -notcontains $_)) {
        git rm -q -- "$_"
    }
}

git add -A
git commit -m $Message
git tag $Version
git push github github-release:main
git push github $Version

Write-Host "Pushed $Version to GitHub."
