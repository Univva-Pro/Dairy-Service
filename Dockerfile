# Stage 1: Build Angular Frontend
FROM node:20 AS frontend-build
WORKDIR /app/frontend
COPY Dairy.Frontend/package*.json ./
RUN npm install
COPY Dairy.Frontend/ ./
RUN npm run build -- --configuration production

# Stage 2: Build .NET API Hub
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY ["nuget.config", "./"]
COPY ["nupkg/", "nupkg/"]
COPY ["Dairy.ServiceHub/Dairy.ServiceHub.csproj", "Dairy.ServiceHub/"]
COPY ["Dairy.Context/Dairy.Context.csproj", "Dairy.Context/"]
COPY ["Dairy.DMO/Dairy.DMO.csproj", "Dairy.DMO/"]
COPY ["Dairy.DTO/Dairy.DTO.csproj", "Dairy.DTO/"]
RUN dotnet restore "Dairy.ServiceHub/Dairy.ServiceHub.csproj"

COPY . .

# Copy compiled Angular app into wwwroot of Dairy.ServiceHub
COPY --from=frontend-build /app/frontend/dist/DairyFrontend/browser ./Dairy.ServiceHub/wwwroot

WORKDIR "/src/Dairy.ServiceHub"
RUN dotnet build "Dairy.ServiceHub.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Dairy.ServiceHub.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Dairy.ServiceHub.dll"]
