@echo off
cd /d C:\Users\umutn\RiderProjects\SushiCounter
echo Current directory: %CD%
echo.
echo Adding all files...
git add -A
echo.
echo Current status:
git status
echo.
echo Committing changes...
git commit -m "Update: Add LastLogin tracking and refactor user deletion - Create session buttons on Home and Explore"
echo.
echo Pushing to GitHub...
git push origin main
echo.
echo Done!
pause

