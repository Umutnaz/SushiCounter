#!/usr/bin/env pwsh

Write-Host "SushiCounter Git Push Script" -ForegroundColor Cyan
Write-Host "============================" -ForegroundColor Cyan
Write-Host ""

$repoPath = "C:\Users\umutn\RiderProjects\SushiCounter"
Set-Location $repoPath

Write-Host "Repository: $repoPath" -ForegroundColor Yellow
Write-Host ""

# Show current status
Write-Host "Current status:" -ForegroundColor Green
git status

Write-Host ""
Write-Host "Adding all changes..." -ForegroundColor Green
git add -A

Write-Host ""
Write-Host "Creating commit..." -ForegroundColor Green
git commit -m "Update: Add LastLogin tracking and refactor user deletion

- Added LastLogin field to User model
- Implemented UpdateLastLoginAsync() to track last login time
- Refactored DeleteUserAsync() to accept User object
- Moved 12-month inactivity check to controller
- Both manual and automatic user deletion use same DeleteUserAsync method
- Login endpoint updates LastLogin on successful authentication
- New users get LastLogin set to creation time
- Added create session buttons on Home and Explore pages
- SessionModal opens in create mode when selectedSession is null
- Updated MainLayout.css with .create-session-btn styling"

Write-Host ""
Write-Host "Pushing to GitHub..." -ForegroundColor Green
git push origin main

Write-Host ""
Write-Host "Done!" -ForegroundColor Cyan

