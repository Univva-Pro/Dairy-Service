#!/usr/bin/env bash
set -e

echo -e "\033[0;36m====================================================\033[0m"
echo -e "\033[0;36m Starting Dairy-Service on Linux (Port 8088)...\033[0m"
echo -e "\033[0;36m====================================================\033[0m"

# Restore & Build
dotnet restore ./Dairy.ServiceHub/Dairy.ServiceHub.csproj
dotnet build ./Dairy.ServiceHub/Dairy.ServiceHub.csproj -c Release

# Run Kestrel Server on Linux
export ASPNETCORE_URLS="http://0.0.0.0:8088"
export MongoDb__ConnectionString="${MongoDb__ConnectionString:-mongodb+srv://naikamit6773_db_user:F4VdIVZdTg2Myhcw@cluster0.pifrqwv.mongodb.net/?appName=Cluster0}"
export MongoDb__DatabaseName="DairyDB"

dotnet run --project ./Dairy.ServiceHub/Dairy.ServiceHub.csproj -c Release
