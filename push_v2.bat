@echo off
setlocal enabledelayedexpansion
cd /d "C:\Users\umutn\RiderProjects\SushiCounter"

echo === Git Push Script ===
echo.

echo Step 1: Adding files...
for /f "tokens=*" %%A in ('git add -A') do echo %%A

echo Step 2: Checking status...
for /f "tokens=*" %%A in ('git status --porcelain') do echo %%A

echo Step 3: Committing...
for /f "tokens=*" %%A in ('git commit -m "Update: Add LastLogin tracking and refactor user deletion"') do echo %%A

echo Step 4: Pushing...
for /f "tokens=*" %%A in ('git push 2^>^&1') do echo %%A

echo.
echo === Complete ===

